using System.IO;
using UTAU.Models;
using UTAU.Notes;
using UTAU.Phonemes;
using UTAU.Synthesis;

namespace UTAU.Tests;

public sealed class TimeMapTests
{
    [Fact]
    public void VelocityOfOneHundredKeepsTheConsonantLength()
        => Assert.Equal(1.0, TimeMap.VelocityToConsonantScale(100.0), 12);

    [Fact]
    public void HigherVelocityShortensTheConsonant()
    {
        Assert.Equal(0.5, TimeMap.VelocityToConsonantScale(200.0), 12);
        Assert.Equal(2.0, TimeMap.VelocityToConsonantScale(0.0), 12);
    }

    [Fact]
    public void ConsonantIsMappedAtTheOriginalSpeed()
    {
        var map = TimeMap.Create(100.0, 200.0, 600.0, 500.0, 100.0, StretchMode.Loop);
        Assert.Equal(100.0, map.Map(0.0), 9);
        Assert.Equal(150.0, map.Map(50.0), 9);
        Assert.Equal(200.0, map.Map(100.0), 9);
    }

    [Fact]
    public void LoopModePingPongsThroughTheSustainedRegion()
    {
        var map = TimeMap.Create(0.0, 0.0, 100.0, 1000.0, 100.0, StretchMode.Loop);
        Assert.Equal(0.0, map.Map(0.0), 9);
        Assert.Equal(100.0, map.Map(100.0), 9);
        Assert.Equal(50.0, map.Map(150.0), 9);
        Assert.Equal(0.0, map.Map(200.0), 9);
        Assert.Equal(60.0, map.Map(260.0), 9);
    }

    [Fact]
    public void LoopModeStaysInsideTheSourceRegion()
    {
        var map = TimeMap.Create(10.0, 30.0, 90.0, 5000.0, 100.0, StretchMode.Loop);
        for (var elapsed = 0.0; elapsed <= 5000.0; elapsed += 3.0)
        {
            var mapped = map.Map(elapsed);
            Assert.InRange(mapped, 10.0, 90.0);
        }
    }

    [Fact]
    public void StretchModeSpreadsTheSustainedRegionOverTheWholeOutput()
    {
        var map = TimeMap.Create(0.0, 100.0, 200.0, 300.0, 100.0, StretchMode.Stretch);
        Assert.Equal(100.0, map.Map(100.0), 9);
        Assert.Equal(150.0, map.Map(200.0), 9);
        Assert.Equal(200.0, map.Map(300.0), 9);
    }

    [Fact]
    public void StretchModeNeverRunsFasterThanTheSource()
    {
        var map = TimeMap.Create(0.0, 0.0, 400.0, 100.0, 100.0, StretchMode.Stretch);
        Assert.Equal(50.0, map.Map(50.0), 9);
        Assert.Equal(100.0, map.Map(100.0), 9);
    }

    [Fact]
    public void ConsonantKeepsItsSpeedWhenTheOutputIsShorter()
    {
        var map = TimeMap.Create(0.0, 200.0, 400.0, 50.0, 100.0, StretchMode.Loop);
        Assert.Equal(200.0, map.ConsonantOutputLength, 9);
        Assert.Equal(50.0, map.Map(50.0), 9);
    }

    [Fact]
    public void TheConsonantVelocityScalesTheConsonantOutput()
    {
        Assert.Equal(100.0, TimeMap.Create(0.0, 200.0, 400.0, 1000.0, 200.0, StretchMode.Loop).ConsonantOutputLength, 9);
        Assert.Equal(400.0, TimeMap.Create(0.0, 200.0, 400.0, 1000.0, 0.0, StretchMode.Loop).ConsonantOutputLength, 9);
    }

    [Fact]
    public void DegenerateSustainedRegionHoldsTheEnd()
    {
        var map = TimeMap.Create(0.0, 100.0, 100.0, 500.0, 100.0, StretchMode.Loop);
        Assert.Equal(100.0, map.Map(400.0), 9);
    }

    [Fact]
    public void NegativeElapsedTimeClampsToTheRegionStart()
        => Assert.Equal(10.0, TimeMap.Create(10.0, 20.0, 30.0, 100.0, 100.0, StretchMode.Loop).Map(-5.0), 9);
}

public sealed class SpectrumTransformTests
{
    [Fact]
    public void AMixingWeightAndAGainScaleThePowerAlike()
    {
        var scaled = SpectrumTransform.ToPower(0.25 * SpectrumTransform.ToAmplitude(4.0));
        var gained = new[] { 4.0 };
        SpectrumTransform.ApplyGain(gained, 0.25);

        Assert.Equal(gained[0], scaled, 12);
    }

    [Fact]
    public void MixingWeightsThatSumToOneKeepTheLevel()
    {
        var sum = 0.4 * SpectrumTransform.ToAmplitude(9.0) + 0.6 * SpectrumTransform.ToAmplitude(9.0);

        Assert.Equal(9.0, SpectrumTransform.ToPower(sum), 12);
    }

    static double[] CreatePeak(int size, int index)
    {
        var spectrum = new double[size];
        Array.Fill(spectrum, 1e-8);
        spectrum[index] = 1.0;
        return spectrum;
    }

    [Fact]
    public void UnitRatioLeavesTheSpectrumUntouched()
    {
        var spectrum = CreatePeak(64, 10);
        var destination = new double[spectrum.Length];
        SpectrumTransform.WarpFormant(spectrum, destination, 1.0);
        Assert.Equal(spectrum, destination);
    }

    [Fact]
    public void WarpingUpwardMovesThePeakToAHigherBin()
    {
        var spectrum = CreatePeak(128, 20);
        var destination = new double[spectrum.Length];
        SpectrumTransform.WarpFormant(spectrum, destination, 2.0);
        Assert.Equal(40, Array.IndexOf(destination, destination.Max()));
    }

    [Fact]
    public void WarpingDownwardMovesThePeakToALowerBin()
    {
        var spectrum = CreatePeak(128, 40);
        var destination = new double[spectrum.Length];
        SpectrumTransform.WarpFormant(spectrum, destination, 0.5);
        Assert.Equal(20, Array.IndexOf(destination, destination.Max()));
    }

    [Fact]
    public void WarpingKeepsEveryValuePositive()
    {
        var spectrum = CreatePeak(128, 40);
        var destination = new double[spectrum.Length];
        SpectrumTransform.WarpFormant(spectrum, destination, 1.7);
        Assert.All(destination, x => Assert.True(x > 0.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidRatiosAreIgnored(double ratio)
    {
        var spectrum = CreatePeak(32, 5);
        var destination = new double[spectrum.Length];
        SpectrumTransform.WarpFormant(spectrum, destination, ratio);
        Assert.Equal(spectrum, destination);
    }

    [Fact]
    public void FormantRatioIsAnOctavePerTwelveSemitones()
    {
        Assert.Equal(2.0, SpectrumTransform.FormantRatioFromSemitones(12.0), 12);
        Assert.Equal(0.5, SpectrumTransform.FormantRatioFromSemitones(-12.0), 12);
        Assert.Equal(1.0, SpectrumTransform.FormantRatioFromSemitones(0.0), 12);
    }

    [Fact]
    public void BrightnessTiltsTheSpectrumByTheGivenDecibelsAtNyquist()
    {
        var spectrum = new double[9];
        Array.Fill(spectrum, 1.0);
        SpectrumTransform.ApplyBrightness(spectrum, 6.0);
        Assert.Equal(1.0, spectrum[0], 12);
        Assert.Equal(Math.Pow(10.0, 0.6), spectrum[^1], 12);
        Assert.Equal(Math.Pow(10.0, 0.3), spectrum[4], 12);
    }

    [Fact]
    public void GainScalesThePowerByItsSquare()
    {
        var spectrum = new double[] { 4.0, 9.0 };
        SpectrumTransform.ApplyGain(spectrum, 0.5);
        Assert.Equal(1.0, spectrum[0], 12);
        Assert.Equal(2.25, spectrum[1], 12);
    }

    [Fact]
    public void ZeroGainStillLeavesAPositiveFloor()
    {
        var spectrum = new double[] { 1.0 };
        SpectrumTransform.ApplyGain(spectrum, 0.0);
        Assert.Equal(SpectrumTransform.MinimumPower, spectrum[0]);
    }

    [Fact]
    public void BreathinessOfZeroKeepsTheAperiodicity()
    {
        var aperiodicity = new[] { 0.2, 0.5, 0.8 };
        SpectrumTransform.ApplyBreathiness(aperiodicity, 0.0);
        Assert.Equal([0.2, 0.5, 0.8], aperiodicity.Select(x => Math.Round(x, 9)));
    }

    [Fact]
    public void PositiveBreathinessRaisesTheAperiodicity()
    {
        var aperiodicity = new[] { 0.25 };
        SpectrumTransform.ApplyBreathiness(aperiodicity, 100.0);
        Assert.Equal(0.5, aperiodicity[0], 9);
    }

    [Fact]
    public void NegativeBreathinessLowersTheAperiodicity()
    {
        var aperiodicity = new[] { 0.5 };
        SpectrumTransform.ApplyBreathiness(aperiodicity, -100.0);
        Assert.Equal(0.25, aperiodicity[0], 9);
    }

    [Fact]
    public void AperiodicityStaysStrictlyInsideTheOpenUnitInterval()
    {
        var aperiodicity = new[] { 0.0, 1.0, -5.0, 5.0, double.NaN };
        SpectrumTransform.ApplyBreathiness(aperiodicity, 100.0);
        foreach (var value in aperiodicity)
            Assert.InRange(value, SpectrumTransform.MinimumAperiodicity, SpectrumTransform.MaximumAperiodicity);
    }

    [Fact]
    public void PowerInterpolationIsGeometric()
        => Assert.Equal(Math.Sqrt(2.0), SpectrumTransform.InterpolatePower(1.0, 2.0, 0.5), 12);
}

public sealed class AudioResamplerTests
{
    static double[] CreateSine(int sampleRate, double frequency, double seconds)
    {
        var samples = new double[(int)(sampleRate * seconds)];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = Math.Sin(2.0 * Math.PI * frequency * i / sampleRate);
        return samples;
    }

    static double EstimateFrequency(double[] samples, int sampleRate)
    {
        var margin = sampleRate / 20;
        var first = -1.0;
        var last = -1.0;
        var crossings = 0;

        for (var i = margin + 1; i < samples.Length - margin; i++)
        {
            if (samples[i - 1] > 0.0 || samples[i] <= 0.0)
                continue;

            var position = i - 1 + samples[i - 1] / (samples[i - 1] - samples[i]);
            if (first < 0.0)
                first = position;
            last = position;
            crossings++;
        }

        return crossings < 2 ? 0.0 : (crossings - 1) * sampleRate / (last - first);
    }

    [Fact]
    public void IdenticalRatesReturnTheSameSamples()
    {
        var input = CreateSine(8000, 100.0, 0.05);
        Assert.Equal(input, AudioResampler.Resample(input, 8000, 8000));
    }

    [Fact]
    public void OutputLengthFollowsTheRateRatio()
    {
        var input = new double[1000];
        Assert.Equal(2000, AudioResampler.Resample(input, 8000, 16000).Length);
        Assert.Equal(500, AudioResampler.Resample(input, 16000, 8000).Length);
    }

    [Fact]
    public void UpsamplingPreservesTheFrequency()
    {
        var input = CreateSine(16000, 440.0, 0.5);
        var output = AudioResampler.Resample(input, 16000, 44100);
        Assert.Equal(440.0, EstimateFrequency(output, 44100), 0);
    }

    [Fact]
    public void DownsamplingPreservesTheFrequency()
    {
        var input = CreateSine(44100, 440.0, 0.5);
        var output = AudioResampler.Resample(input, 44100, 16000);
        Assert.Equal(440.0, EstimateFrequency(output, 16000), 0);
    }

    [Fact]
    public void AmplitudeIsPreservedAwayFromTheEdges()
    {
        var input = CreateSine(16000, 440.0, 0.5);
        var output = AudioResampler.Resample(input, 16000, 44100);
        var interior = output.Skip(2000).Take(output.Length - 4000).ToArray();
        Assert.InRange(interior.Max(Math.Abs), 0.97, 1.03);
    }

    [Fact]
    public void EmptyInputProducesEmptyOutput()
        => Assert.Empty(AudioResampler.Resample([], 8000, 16000));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveRatesAreRejected(int rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.Resample(new double[8], rate, 8000));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioResampler.Resample(new double[8], 8000, rate));
    }
}

public sealed class VibratoAndPitchTests
{
    [Fact]
    public void DisabledVibratoProducesNoOffset()
    {
        var vibrato = new VibratoSettings { LengthPercent = 0.0, DepthCents = 50.0 };
        Assert.Equal(0.0, vibrato.Evaluate(500.0, 1000.0));
    }

    [Fact]
    public void VibratoOnlyAffectsTheTailOfTheNote()
    {
        var vibrato = new VibratoSettings { LengthPercent = 50.0, DepthCents = 50.0, FadeInPercent = 0.0, FadeOutPercent = 0.0 };
        Assert.Equal(0.0, vibrato.Evaluate(100.0, 1000.0));
        Assert.NotEqual(0.0, vibrato.Evaluate(700.0, 1000.0));
    }

    [Fact]
    public void VibratoStaysWithinItsDepth()
    {
        var vibrato = new VibratoSettings { LengthPercent = 100.0, DepthCents = 40.0, OffsetPercent = 0.0 };
        for (var elapsed = 0.0; elapsed <= 1000.0; elapsed += 1.0)
            Assert.InRange(vibrato.Evaluate(elapsed, 1000.0), -40.0, 40.0);
    }

    [Fact]
    public void VibratoOutsideTheNoteIsSilent()
    {
        var vibrato = new VibratoSettings { LengthPercent = 100.0, DepthCents = 40.0 };
        Assert.Equal(0.0, vibrato.Evaluate(-1.0, 1000.0));
        Assert.Equal(0.0, vibrato.Evaluate(1001.0, 1000.0));
    }

    [Fact]
    public void VibratoPropertiesAreClamped()
    {
        var vibrato = new VibratoSettings { LengthPercent = 500.0, DepthCents = -20.0, PeriodMilliseconds = 0.0 };
        Assert.Equal(100.0, vibrato.LengthPercent);
        Assert.Equal(0.0, vibrato.DepthCents);
        Assert.Equal(10.0, vibrato.PeriodMilliseconds);
    }

    [Fact]
    public void CurveShapesMatchTheirDefinitions()
    {
        Assert.Equal(50.0, PitchPoint.Interpolate(0.0, 100.0, 0.5, PitchPointShape.Linear), 9);
        Assert.Equal(50.0, PitchPoint.Interpolate(0.0, 100.0, 0.5, PitchPointShape.SCurve), 9);
        Assert.Equal(70.71067811865476, PitchPoint.Interpolate(0.0, 100.0, 0.5, PitchPointShape.RCurve), 9);
        Assert.Equal(29.289321881345245, PitchPoint.Interpolate(0.0, 100.0, 0.5, PitchPointShape.JCurve), 9);
        Assert.Equal(25.0, PitchPoint.Interpolate(0.0, 100.0, 0.25, PitchPointShape.Linear), 9);
    }

    [Fact]
    public void EveryCurveShapeConnectsBothEndpoints()
    {
        foreach (var shape in Enum.GetValues<PitchPointShape>())
        {
            Assert.Equal(10.0, PitchPoint.Interpolate(10.0, 90.0, 0.0, shape), 9);
            Assert.Equal(90.0, PitchPoint.Interpolate(10.0, 90.0, 1.0, shape), 9);
            Assert.Equal(10.0, PitchPoint.Interpolate(10.0, 90.0, -1.0, shape), 9);
            Assert.Equal(90.0, PitchPoint.Interpolate(10.0, 90.0, 2.0, shape), 9);
        }
    }

    [Fact]
    public void PortamentoHoldsTheFirstAndLastValueOutsideTheControlPoints()
    {
        var note = new UTAUNote();
        note.PitchPoints.Add(new PitchPoint(100, -200.0, PitchPointShape.Linear));
        note.PitchPoints.Add(new PitchPoint(300, 0.0, PitchPointShape.Linear));

        Assert.Equal(-200.0, note.EvaluatePortamentoCents(0), 9);
        Assert.Equal(-100.0, note.EvaluatePortamentoCents(200), 9);
        Assert.Equal(0.0, note.EvaluatePortamentoCents(400), 9);
    }

    [Fact]
    public void NoControlPointsMeansNoPortamento()
        => Assert.Equal(0.0, new UTAUNote().EvaluatePortamentoCents(50));

    [Fact]
    public void ASingleControlPointIsConstant()
    {
        var note = new UTAUNote();
        note.PitchPoints.Add(new PitchPoint(100, 25.0));
        Assert.Equal(25.0, note.EvaluatePortamentoCents(0));
        Assert.Equal(25.0, note.EvaluatePortamentoCents(1000));
    }

    [Fact]
    public void CoincidentControlPointsDoNotDivideByZero()
    {
        var note = new UTAUNote();
        note.PitchPoints.Add(new PitchPoint(100, 0.0, PitchPointShape.Linear));
        note.PitchPoints.Add(new PitchPoint(100, 100.0, PitchPointShape.Linear));
        Assert.Equal(100.0, note.EvaluatePortamentoCents(100), 9);
    }
}

public sealed class AnalysisCacheTests
{
    static WorldFeatures CreateFeatures(int frames)
        => new(44100, 5.0, 8, 0.0, new double[frames], new double[frames * 5], new double[frames * 5]);

    [Fact]
    public void SecondLookupReusesTheStoredFeatures()
    {
        var cache = new AnalysisCache(1024 * 1024);
        var calls = 0;
        var first = cache.GetOrAdd("a.wav", 1, 0, 10, F0Estimator.Dio, () => { calls++; return CreateFeatures(4); });
        var second = cache.GetOrAdd("a.wav", 1, 0, 10, F0Estimator.Dio, () => { calls++; return CreateFeatures(4); });

        Assert.Same(first, second);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("b.wav", 1L, 0, 10, F0Estimator.Dio)]
    [InlineData("a.wav", 2L, 0, 10, F0Estimator.Dio)]
    [InlineData("a.wav", 1L, 1, 10, F0Estimator.Dio)]
    [InlineData("a.wav", 1L, 0, 11, F0Estimator.Dio)]
    [InlineData("a.wav", 1L, 0, 10, F0Estimator.Harvest)]
    public void EveryKeyComponentSeparatesEntries(string path, long ticks, int start, int end, F0Estimator estimator)
    {
        var cache = new AnalysisCache(1024 * 1024);
        cache.GetOrAdd("a.wav", 1, 0, 10, F0Estimator.Dio, () => CreateFeatures(4));
        cache.GetOrAdd(path, ticks, start, end, estimator, () => CreateFeatures(4));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void ExceedingTheBudgetEvictsTheLeastRecentlyUsedEntry()
    {
        var cache = new AnalysisCache(CreateFeatures(100).EstimatedBytes * 2);
        cache.GetOrAdd("a.wav", 1, 0, 1, F0Estimator.Dio, () => CreateFeatures(100));
        cache.GetOrAdd("b.wav", 1, 0, 1, F0Estimator.Dio, () => CreateFeatures(100));
        cache.GetOrAdd("a.wav", 1, 0, 1, F0Estimator.Dio, () => CreateFeatures(100));
        cache.GetOrAdd("c.wav", 1, 0, 1, F0Estimator.Dio, () => CreateFeatures(100));

        Assert.Equal(2, cache.Count);
        var calls = 0;
        cache.GetOrAdd("b.wav", 1, 0, 1, F0Estimator.Dio, () => { calls++; return CreateFeatures(100); });
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AtLeastOneEntrySurvivesAnUndersizedBudget()
    {
        var cache = new AnalysisCache(1);
        cache.GetOrAdd("a.wav", 1, 0, 1, F0Estimator.Dio, () => CreateFeatures(100));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void ClearingResetsBothTheCountAndTheUsage()
    {
        var cache = new AnalysisCache(1024 * 1024);
        cache.GetOrAdd("a.wav", 1, 0, 1, F0Estimator.Dio, () => CreateFeatures(10));
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.UsedBytes);
    }
}
