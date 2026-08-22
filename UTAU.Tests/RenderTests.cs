using System.IO;
using System.Text;
using UTAU;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;
using UTAU.Synthesis;
using WorldNet;

namespace UTAU.Tests;

public sealed class WaveIoTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    [Fact]
    public void WrittenSamplesAreReadBackWithinQuantisationError()
    {
        var samples = new double[1000];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = Math.Sin(2.0 * Math.PI * 440.0 * i / 44100.0) * 0.5;

        var path = Path.Combine(directory, "a.wav");
        WaveIo.Write(path, samples, 44100);
        var read = WaveIo.Read(path);

        Assert.Equal(44100, read.SampleRate);
        Assert.Equal(samples.Length, read.Samples.Length);
        for (var i = 0; i < samples.Length; i++)
            Assert.Equal(samples[i], read.Samples[i], 1.0 / short.MaxValue);
    }

    [Fact]
    public void SamplesAreClippedInsteadOfWrappingAround()
    {
        var path = Path.Combine(directory, "clip.wav");
        WaveIo.Write(path, [2.0, -2.0], 44100);
        var read = WaveIo.Read(path);

        Assert.True(read.Samples[0] > 0.9);
        Assert.True(read.Samples[1] < -0.9);
    }

    [Fact]
    public void DurationFollowsTheSampleRate()
    {
        var path = Path.Combine(directory, "duration.wav");
        WaveIo.Write(path, new double[22050], 44100);
        Assert.Equal(500.0, WaveIo.Read(path).DurationMilliseconds, 6);
    }

    [Fact]
    public void MissingDirectoriesAreCreated()
    {
        var path = Path.Combine(directory, "nested", "deep", "a.wav");
        WaveIo.Write(path, new double[16], 44100);
        Assert.True(File.Exists(path));
    }
}

public sealed class WorldAnalyzerTests
{
    [Theory]
    [InlineData(F0Estimator.Dio)]
    [InlineData(F0Estimator.Harvest)]
    public void DetectsThePitchOfASyntheticVowel(F0Estimator estimator)
    {
        using var arena = new WorldArena();
        var sample = new AudioSample(TestVoiceBank.CreateVowel(220.0, 400.0), TestVoiceBank.SampleRate);
        var features = WorldAnalyzer.Analyze(sample, estimator, arena);

        Assert.True(features.FrameCount > 0);
        Assert.InRange(features.GetVoicedGeometricMeanF0(), 219.0, 221.0);
        Assert.Equal(features.FrameCount * features.SpectrumSize, features.Spectrogram.Length);
        Assert.Equal(features.FrameCount * features.SpectrumSize, features.Aperiodicity.Length);
    }

    [Fact]
    public void EmptyInputProducesEmptyFeatures()
    {
        using var arena = new WorldArena();
        var features = WorldAnalyzer.Analyze(new AudioSample([], 44100), F0Estimator.Dio, arena);

        Assert.Equal(0, features.FrameCount);
        Assert.Equal(0.0, features.GetVoicedGeometricMeanF0());
    }

    [Fact]
    public void FrameIndexAccountsForTheAnalysisOffset()
    {
        using var arena = new WorldArena();
        var sample = new AudioSample(TestVoiceBank.CreateVowel(220.0, 200.0), TestVoiceBank.SampleRate);
        var features = WorldAnalyzer.Analyze(sample, F0Estimator.Dio, arena, 100.0);

        Assert.Equal(0.0, features.GetFrameIndex(100.0), 9);
        Assert.Equal(1.0, features.GetFrameIndex(100.0 + WorldAnalyzer.FramePeriod), 9);
    }

    [Fact]
    public void SilenceIsReportedAsUnvoiced()
    {
        using var arena = new WorldArena();
        var features = WorldAnalyzer.Analyze(new AudioSample(new double[44100 / 4], 44100), F0Estimator.Dio, arena);
        Assert.All(features.F0, x => Assert.Equal(0.0, x));
    }
}

[Collection("Render")]
public sealed class UtauRendererTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();
    readonly AnalysisCache cache = new(AnalysisCache.DefaultBudgetBytes);

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    RenderResult Render(VoiceBank bank, string text, RenderSettings? settings = null, RenderCurves? curves = null, double tempo = 120.0, double speed = 1.0)
    {
        var notes = NoteSequenceBuilder.Build(text, NoteBuildOptions.Create(60));
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, new TimeBase(tempo, speed)), null, PhonemizeOptions.Default);
        return new UtauRenderer(settings ?? RenderSettings.Default with { Estimator = F0Estimator.Dio }, curves ?? RenderCurves.Empty, cache, new SegmentCache(SegmentCache.DefaultBudgetBytes)).Render(units);
    }

    static double Peak(double[] samples) => samples.Length == 0 ? 0.0 : samples.Max(Math.Abs);

    VoiceBank BankAtLevel(string name, double level)
    {
        var path = Path.Combine(directory, name);
        Directory.CreateDirectory(path);
        TestVoiceBank.WriteText(path, VoiceBankLoader.CharacterFileName, "name=試験音源\r\n");
        TestVoiceBank.WriteText(path, VoiceBankLoader.OtoFileName, "a.wav=あ,50,80,-500,100,40");
        TestVoiceBank.WriteSample(path, "a.wav", [.. TestVoiceBank.CreateVowel().Select(x => x * level)]);
        return VoiceBankLoader.Load(name, path);
    }

    const double QuietLevel = 0.25;
    const double PeakCompression = 0.86;

    [Fact]
    public void AQuietSampleIsLiftedTowardTheSamePeak()
    {
        var loud = Peak(Render(BankAtLevel("loud", 1.0), "<!C4:1/4>あ").Samples);
        var quiet = Peak(Render(BankAtLevel("quiet", QuietLevel), "<!C4:1/4>あ").Samples);

        Assert.Equal(Math.Pow(QuietLevel, 1.0 - PeakCompression), quiet / loud, 2);
    }

    static double MeasureFundamental(RenderResult result)
    {
        using var arena = new WorldArena();
        var features = WorldAnalyzer.Analyze(new AudioSample(result.Samples, result.SampleRate), F0Estimator.Harvest, arena);
        return features.GetVoicedGeometricMeanF0();
    }

    [Fact]
    public void ProducesAudibleOutputForASingleKanaBank()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var result = Render(bank, "あかさ");

        Assert.Equal(TestVoiceBank.SampleRate, result.SampleRate);
        Assert.True(result.Samples.Length > 0);
        Assert.True(Peak(result.Samples) > 0.01, $"peak={Peak(result.Samples)}");
        Assert.All(result.Samples, x => Assert.True(double.IsFinite(x)));
    }

    [Fact]
    public void OutputLengthFollowsTheScore()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var result = Render(bank, "あかさ");
        var expected = result.Timings.Max(x => x.AudioEndMilliseconds) - result.OffsetMilliseconds;

        Assert.Equal(expected, result.Samples.Length * 1000.0 / result.SampleRate, 0);
    }

    [Fact]
    public void RenderingIsDeterministic()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        Assert.Equal(Render(bank, "あか").Samples, Render(bank, "あか").Samples);
    }

    [Fact]
    public void RenderedPitchFollowsTheRequestedTone()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var low = Render(bank, "<!C3:1/2>あ");
        var high = Render(bank, "<!C5:1/2>あ");

        Assert.Equal(new MusicalTone(48).Frequency, MeasureFundamental(low), new MusicalTone(48).Frequency * 0.02);
        Assert.Equal(new MusicalTone(72).Frequency, MeasureFundamental(high), new MusicalTone(72).Frequency * 0.02);
    }

    [Fact]
    public void LongerNotesProduceLongerAudio()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var shortResult = Render(bank, "<!C4:1/8>あ");
        var longResult = Render(bank, "<!C4:1/1>あ");

        Assert.True(longResult.Samples.Length > shortResult.Samples.Length * 4);
        Assert.True(Peak(longResult.Samples) > 0.01);
    }

    [Fact]
    public void SustainedNotesStayAudibleBeyondTheSourceLength()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var result = Render(bank, "<!C4:2/1>あ");
        var tail = result.Samples.Skip(result.Samples.Length * 3 / 4).ToArray();

        Assert.True(Peak(tail) > 0.01, $"tail peak={Peak(tail)}");
    }

    [Fact]
    public void VolumeScalesTheOutputLinearly()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var full = Render(bank, "<!C4:1/4>あ");
        var half = Render(bank, "<!C4:1/4>あ", RenderSettings.Default with { Estimator = F0Estimator.Dio, Volume = 50.0 });

        Assert.Equal(Peak(full.Samples) * 0.5, Peak(half.Samples), 2);
    }

    [Fact]
    public void SilentVolumeProducesSilence()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var full = Render(bank, "<!C4:1/4>あ");
        var silent = Render(bank, "<!C4:1/4>あ", RenderSettings.Default with { Estimator = F0Estimator.Dio, Volume = 0.0 });

        Assert.True(Peak(silent.Samples) < Peak(full.Samples) * 1e-4, $"peak={Peak(silent.Samples)}");
        Assert.True(Peak(silent.Samples) < 1.0 / short.MaxValue, $"peak={Peak(silent.Samples)}");
    }

    [Fact]
    public void StretchModeAlsoRendersAudibleOutput()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var result = Render(bank, "<!C4:1/1>あ", RenderSettings.Default with { Estimator = F0Estimator.Dio, StretchMode = StretchMode.Stretch });

        Assert.True(Peak(result.Samples) > 0.01);
    }

    [Fact]
    public void FormantAndBreathinessDoNotBreakTheOutput()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var settings = RenderSettings.Default with
        {
            Estimator = F0Estimator.Dio,
            FormantSemitones = 4.0,
            Breathiness = 60.0,
            Brightness = 6.0,
        };
        var result = Render(bank, "<!C4:1/4>あ", settings);

        Assert.True(Peak(result.Samples) > 0.001);
        Assert.All(result.Samples, x => Assert.True(double.IsFinite(x)));
    }

    [Fact]
    public void RestsProduceNearSilenceBetweenNotes()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var result = Render(bank, "<!C4:1/4>あ<!R:1/2><!C4:1/4>か");
        var start = (int)((500.0 - result.OffsetMilliseconds + 200.0) * result.SampleRate / 1000.0);
        var end = (int)((1500.0 - result.OffsetMilliseconds - 200.0) * result.SampleRate / 1000.0);

        Assert.InRange(start, 0, result.Samples.Length - 1);
        Assert.InRange(end, start + 1, result.Samples.Length);
        Assert.True(Peak(result.Samples[start..end]) < 1e-6);
    }

    [Fact]
    public void ContinuousBanksRender()
    {
        var bank = TestVoiceBank.CreateVcvBank(directory);
        var result = Render(bank, "あか");
        Assert.True(Peak(result.Samples) > 0.01);
    }

    [Fact]
    public void CvvcBanksRenderEveryUnitIncludingTheTransition()
    {
        var bank = TestVoiceBank.CreateCvvcBank(directory);
        var result = Render(bank, "あか");

        Assert.Equal(4, result.Timings.Count);
        Assert.True(Peak(result.Samples) > 0.01);
    }

    [Fact]
    public void MissingSampleFilesAreSkippedWithoutThrowing()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        File.Delete(Path.Combine(directory, "ka.wav"));
        var result = Render(bank, "あか");

        Assert.True(Peak(result.Samples) > 0.01);
    }

    [Fact]
    public void UnknownLyricsAreSkippedWithoutStoppingTheRest()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var result = Render(bank, "<!C4:1/4>あ<!C4:1/4>ぱ<!C4:1/4>あ");

        Assert.Equal(2, result.Timings.Count);
        Assert.True(Peak(result.Samples) > 0.01);
    }

    [Fact]
    public void UnitsWithoutAnyRenderableEntryProduceAnEmptyResult()
    {
        var result = new UtauRenderer(RenderSettings.Default with { Estimator = F0Estimator.Dio }, RenderCurves.Empty, cache, new SegmentCache(SegmentCache.DefaultBudgetBytes)).Render([]);

        Assert.Empty(result.Samples);
        Assert.Empty(result.Timings);
    }

    [Fact]
    public void ParallelAnalysisProducesTheSameOutputAsASingleSample()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var combined = Render(bank, "あかさ");
        cache.Clear();
        var again = Render(bank, "あかさ");

        Assert.Equal(combined.Samples, again.Samples);
    }

    [Fact]
    public void SamplesRecordedAtAnotherRateAreResampled()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var resampled = AudioResampler.Resample(new AudioSample(TestVoiceBank.CreateVowel(), TestVoiceBank.SampleRate), 22050);
        WaveIo.Write(Path.Combine(directory, "ka.wav"), resampled.Samples, resampled.SampleRate);

        var result = Render(bank, "あか");
        Assert.Equal(TestVoiceBank.SampleRate, result.SampleRate);
        Assert.True(Peak(result.Samples) > 0.01);
    }
}

[Collection("Render")]
public sealed class SegmentedRenderTests : IDisposable
{
    const int TailSamples = 2205;

    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();
    readonly AnalysisCache cache = new(64L * 1024 * 1024);

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    double[] Render(VoiceBank bank, string text)
    {
        var notes = NoteSequenceBuilder.Build(text, NoteBuildOptions.Create(60));
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);
        using var arena = new WorldArena();
        var settings = RenderSettings.Default with { Estimator = F0Estimator.Dio };
        return new UtauRenderer(settings, RenderCurves.Empty, cache, new SegmentCache(SegmentCache.DefaultBudgetBytes)).Render(units).Samples;
    }

    [Fact]
    public void APhraseIsUnchangedByWhatFollowsAfterALongRest()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);

        var alone = Render(bank, "<!C4:1/4>あ");
        var followed = Render(bank, "<!C4:1/4>あ<!R:1920><!C4:1/4>か");

        Assert.True(followed.Length > alone.Length * 2);
        for (var index = 0; index < alone.Length - TailSamples; index++)
            Assert.Equal(alone[index], followed[index], 1e-12);
    }

    [Fact]
    public void TheGapBetweenPhrasesIsSilent()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var samples = Render(bank, "<!C4:1/4>あ<!R:1920><!C4:1/4>か");

        var middle = samples.Length / 2;
        var window = samples.Length / 20;
        for (var index = middle - window; index < middle + window; index++)
            Assert.Equal(0.0, samples[index], 1e-6);

        Assert.Contains(samples.Take(samples.Length / 4), x => Math.Abs(x) > 1e-3);
        Assert.Contains(samples.Skip(samples.Length * 3 / 4), x => Math.Abs(x) > 1e-3);
    }

    [Fact]
    public void ManyPhrasesSeparatedByRestsStillRender()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var builder = new StringBuilder();
        for (var index = 0; index < 24; index++)
            builder.Append("<!C4:1/4>あ<!R:960>");

        var samples = Render(bank, builder.ToString());

        Assert.True(samples.Length > 0);
        Assert.Contains(samples, x => Math.Abs(x) > 1e-3);
    }

    [Fact]
    public void ARestOpensAGapWideEnoughToSplitOn()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);

        Assert.True(GapFrames(bank, "<!C4:1/4>あ<!R:1920><!C4:1/4>か") > 10.0);
        Assert.True(GapFrames(bank, "<!C4:1/4>あ<!R:240><!C4:1/4>か") > 10.0);
        Assert.True(GapFrames(bank, "<!C4:1/4>あ<!C4:1/4>か") < 0.0);
    }

    static double GapFrames(VoiceBank bank, string text)
    {
        var notes = NoteSequenceBuilder.Build(text, NoteBuildOptions.Create(60));
        var units = Phonemizer.Phonemize(bank, TempoMap.Create(notes, TimeBase.Default), null, PhonemizeOptions.Default);
        var timings = UnitTimingBuilder.Build(units).OrderBy(x => x.AudioStartMilliseconds).ToArray();
        return (timings[1].AudioStartMilliseconds - timings[0].AudioEndMilliseconds) / WorldAnalyzer.FramePeriod;
    }

    [Fact]
    public void AScoreTooLongForOneBufferStillRendersWhenItHasRests()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var builder = new StringBuilder();
        for (var index = 0; index < 40; index++)
            builder.Append("<!C4:1/4>あ<!R:1920>");

        var samples = Render(bank, builder.ToString());

        Assert.True(samples.Length > 44100 * 90, $"length={samples.Length}");
        Assert.Contains(samples, x => Math.Abs(x) > 1e-3);
    }

    [Fact]
    public void OnePhraseWithoutAnyRestIsRefusedInsteadOfExhaustingMemory()
    {
        var bank = TestVoiceBank.CreateSingleKanaBank(directory);
        var builder = new StringBuilder();
        for (var index = 0; index < 400; index++)
            builder.Append("<!C4:1/1>あ");

        var error = Assert.Throws<InvalidOperationException>(() => Render(bank, builder.ToString()));

        Assert.Equal(Texts.TextTooLongMessage, error.Message);
    }
}
