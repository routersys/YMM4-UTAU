using System.IO;
using UTAU.Models;
using UTAU.Phonemes;
using WorldNet;
using WorldSynthesis = WorldNet.Synthesis;

namespace UTAU.Synthesis;

internal sealed record RenderResult(double[] Samples, int SampleRate, IReadOnlyList<UnitTiming> Timings, double OffsetMilliseconds);

internal sealed class UtauRenderer(RenderSettings settings, AnalysisCache cache)
{
    sealed record UnitSource(
        WorldFeatures Features,
        double RegionStart,
        double RegionEnd,
        double ConsonantEnd,
        double MeanF0);

    const long MaximumFrameElements = 1L << 27;

    readonly Dictionary<string, AudioSample> loadedSamples = new(StringComparer.OrdinalIgnoreCase);

    public RenderResult Render(IReadOnlyList<PhonemeUnit> units, WorldArena arena)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(arena);

        var timings = UnitTimingBuilder.Build(units);
        if (timings.Count == 0)
            return new RenderResult([], DetermineSampleRate(units), timings, 0.0);

        var sampleRate = DetermineSampleRate(units);
        var framePeriod = WorldAnalyzer.FramePeriod;
        var fftSize = CheapTrickOption.Create(sampleRate).FftSize;
        var spectrumSize = fftSize / 2 + 1;

        var offset = timings.Min(x => x.AudioStartMilliseconds);
        var totalMilliseconds = timings.Max(x => x.AudioEndMilliseconds) - offset;
        var frameCount = Math.Max((int)Math.Ceiling(totalMilliseconds / framePeriod) + 1, 2);
        if ((long)frameCount * spectrumSize > MaximumFrameElements)
            throw new InvalidOperationException(Texts.TextTooLongMessage);

        var f0 = new double[frameCount];
        var spectrogram = new double[(long)frameCount * spectrumSize];
        var aperiodicity = new double[(long)frameCount * spectrumSize];
        var weightSum = new double[frameCount];
        var voicedWeight = new double[frameCount];
        var logF0Sum = new double[frameCount];

        var frameSpectrum = new double[spectrumSize];
        var warpedSpectrum = new double[spectrumSize];
        var frameAperiodicity = new double[spectrumSize];

        foreach (var timing in timings)
        {
            var source = LoadSource(timing.Unit, sampleRate, arena);
            if (source is null)
                continue;

            AccumulateUnit(
                timing,
                source,
                offset,
                framePeriod,
                frameCount,
                spectrumSize,
                frameSpectrum,
                warpedSpectrum,
                frameAperiodicity,
                spectrogram,
                aperiodicity,
                weightSum,
                voicedWeight,
                logF0Sum);
        }

        Finalize(frameCount, spectrumSize, spectrogram, aperiodicity, weightSum, voicedWeight, logF0Sum, f0);

        var outputLength = (int)((frameCount - 1) * framePeriod / 1000.0 * sampleRate) + 1;
        var samples = new double[outputLength];
        WorldSynthesis.Synthesize(f0, spectrogram, aperiodicity, fftSize, framePeriod, sampleRate, samples, arena);

        return new RenderResult(samples, sampleRate, timings, offset);
    }

    void AccumulateUnit(
        UnitTiming timing,
        UnitSource source,
        double offset,
        double framePeriod,
        int frameCount,
        int spectrumSize,
        double[] frameSpectrum,
        double[] warpedSpectrum,
        double[] frameAperiodicity,
        double[] spectrogram,
        double[] aperiodicity,
        double[] weightSum,
        double[] voicedWeight,
        double[] logF0Sum)
    {
        var unit = timing.Unit;
        var note = unit.Note;
        var startFrame = Math.Max((int)Math.Floor((timing.AudioStartMilliseconds - offset) / framePeriod), 0);
        var endFrame = Math.Min((int)Math.Ceiling((timing.AudioEndMilliseconds - offset) / framePeriod), frameCount - 1);
        var gain = settings.Gain * Math.Clamp(note.Intensity, 0.0, 200.0) / 100.0;
        var map = TimeMap.Create(source.RegionStart, source.ConsonantEnd, source.RegionEnd, timing.RenderLengthMilliseconds, note.Velocity, settings.StretchMode);
        var modulation = Math.Clamp(note.Modulation, -200.0, 200.0) / 100.0;

        for (var frame = startFrame; frame <= endFrame; frame++)
        {
            var absolute = offset + frame * framePeriod;
            var elapsed = absolute - timing.AudioStartMilliseconds;
            var weight = timing.GetWeight(elapsed);
            if (weight <= 0.0)
                continue;

            var sourceMilliseconds = map.Map(elapsed);
            var frameIndex = source.Features.GetFrameIndex(sourceMilliseconds);
            SampleFeatures(source.Features, frameIndex, frameSpectrum, frameAperiodicity, out var sourceF0);

            SpectrumTransform.WarpFormant(frameSpectrum, warpedSpectrum, settings.FormantRatio);
            SpectrumTransform.ApplyBrightness(warpedSpectrum, settings.Brightness);
            SpectrumTransform.ApplyGain(warpedSpectrum, gain);
            SpectrumTransform.ApplyBreathiness(frameAperiodicity, settings.Breathiness);

            var baseIndex = (long)frame * spectrumSize;
            for (var k = 0; k < spectrumSize; k++)
            {
                spectrogram[baseIndex + k] += weight * warpedSpectrum[k];
                aperiodicity[baseIndex + k] += weight * frameAperiodicity[k];
            }
            weightSum[frame] += weight;

            if (sourceF0 <= 0.0)
                continue;

            var cents = note.EvaluatePitchOffsetCents(absolute - unit.NoteStartMilliseconds);
            var targetF0 = MusicalTone.FrequencyOf(unit.Tone + cents / 100.0);
            var ratio = source.MeanF0 > 0.0 ? sourceF0 / source.MeanF0 : 1.0;
            var value = targetF0 * Math.Pow(ratio, modulation);
            if (!double.IsFinite(value) || value <= 0.0)
                continue;

            logF0Sum[frame] += weight * Math.Log(value);
            voicedWeight[frame] += weight;
        }
    }

    static void Finalize(
        int frameCount,
        int spectrumSize,
        double[] spectrogram,
        double[] aperiodicity,
        double[] weightSum,
        double[] voicedWeight,
        double[] logF0Sum,
        double[] f0)
    {
        for (var frame = 0; frame < frameCount; frame++)
        {
            var baseIndex = (long)frame * spectrumSize;
            var total = weightSum[frame];

            if (total <= 0.0)
            {
                for (var k = 0; k < spectrumSize; k++)
                {
                    spectrogram[baseIndex + k] = SpectrumTransform.MinimumPower;
                    aperiodicity[baseIndex + k] = SpectrumTransform.MaximumAperiodicity;
                }
                f0[frame] = 0.0;
                continue;
            }

            for (var k = 0; k < spectrumSize; k++)
            {
                spectrogram[baseIndex + k] = Math.Max(spectrogram[baseIndex + k], SpectrumTransform.MinimumPower);
                aperiodicity[baseIndex + k] = SpectrumTransform.Clamp(aperiodicity[baseIndex + k] / total);
            }

            f0[frame] = voicedWeight[frame] > 0.0 ? Math.Exp(logF0Sum[frame] / voicedWeight[frame]) : 0.0;
        }
    }

    static void SampleFeatures(
        WorldFeatures features,
        double frameIndex,
        double[] spectrum,
        double[] aperiodicity,
        out double f0)
    {
        var count = features.FrameCount;
        if (count == 0)
        {
            Array.Fill(spectrum, SpectrumTransform.MinimumPower);
            Array.Fill(aperiodicity, SpectrumTransform.MaximumAperiodicity);
            f0 = 0.0;
            return;
        }

        var position = Math.Clamp(frameIndex, 0.0, count - 1);
        var low = (int)position;
        var high = Math.Min(low + 1, count - 1);
        var fraction = position - low;
        var spectrumSize = features.SpectrumSize;
        var lowBase = (long)low * spectrumSize;
        var highBase = (long)high * spectrumSize;

        for (var k = 0; k < spectrumSize; k++)
        {
            spectrum[k] = SpectrumTransform.InterpolatePower(
                features.Spectrogram[lowBase + k],
                features.Spectrogram[highBase + k],
                fraction);
            aperiodicity[k] = features.Aperiodicity[lowBase + k] * (1.0 - fraction) + features.Aperiodicity[highBase + k] * fraction;
        }

        var lowF0 = features.F0[low];
        var highF0 = features.F0[high];
        f0 = lowF0 <= 0.0 || highF0 <= 0.0
            ? (fraction < 0.5 ? lowF0 : highF0)
            : Math.Exp(Math.Log(lowF0) * (1.0 - fraction) + Math.Log(highF0) * fraction);
    }

    UnitSource? LoadSource(PhonemeUnit unit, int sampleRate, WorldArena arena)
    {
        if (unit.Entry is not { } entry)
            return null;

        var sample = LoadSample(entry.SamplePath, sampleRate);
        if (sample is null || sample.Samples.Length == 0)
            return null;

        var duration = sample.DurationMilliseconds;
        var regionStart = Math.Clamp(entry.Offset + unit.Note.StartPointMilliseconds, 0.0, duration);
        var regionEnd = Math.Clamp(entry.GetEndMilliseconds(duration), regionStart, duration);
        if (regionEnd - regionStart < 1.0)
            return null;

        var consonantEnd = Math.Clamp(entry.Offset + entry.Consonant, regionStart, regionEnd);
        var analysisStart = Math.Max(regionStart - RenderSettings.AnalysisMarginMilliseconds, 0.0);
        var analysisEnd = Math.Min(regionEnd + RenderSettings.AnalysisMarginMilliseconds, duration);
        var startSample = sample.MillisecondsToSamples(analysisStart);
        var endSample = Math.Min(sample.MillisecondsToSamples(analysisEnd), sample.Samples.Length);
        if (endSample - startSample < 2)
            return null;

        var writeTimeTicks = GetWriteTimeTicks(entry.SamplePath);
        var features = cache.GetOrAdd(
            entry.SamplePath,
            writeTimeTicks,
            startSample,
            endSample,
            settings.Estimator,
            () => WorldAnalyzer.Analyze(
                new AudioSample(sample.Samples[startSample..endSample], sample.SampleRate),
                settings.Estimator,
                arena,
                startSample * 1000.0 / sample.SampleRate));

        return new UnitSource(features, regionStart, regionEnd, consonantEnd, ComputeMeanF0(features, regionStart, regionEnd));
    }

    static double ComputeMeanF0(WorldFeatures features, double regionStart, double regionEnd)
    {
        var sum = 0.0;
        var count = 0;
        var first = Math.Max((int)Math.Floor(features.GetFrameIndex(regionStart)), 0);
        var last = Math.Min((int)Math.Ceiling(features.GetFrameIndex(regionEnd)), features.FrameCount - 1);

        for (var i = first; i <= last; i++)
        {
            var value = features.F0[i];
            if (value <= 0.0)
                continue;
            sum += Math.Log(value);
            count++;
        }

        return count == 0 ? features.GetVoicedGeometricMeanF0() : Math.Exp(sum / count);
    }

    AudioSample? LoadSample(string path, int sampleRate)
    {
        if (!loadedSamples.TryGetValue(path, out var sample))
        {
            if (!File.Exists(path))
                return null;

            try
            {
                sample = WaveIo.Read(path);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or FormatException or NotSupportedException or ArgumentException)
            {
                return null;
            }
        }

        if (sample.SampleRate != sampleRate)
            sample = AudioResampler.Resample(sample, sampleRate);

        loadedSamples[path] = sample;
        return sample;
    }

    int DetermineSampleRate(IReadOnlyList<PhonemeUnit> units)
    {
        foreach (var unit in units)
        {
            if (unit.Entry is not { } entry || !File.Exists(entry.SamplePath))
                continue;

            try
            {
                var sample = WaveIo.Read(entry.SamplePath);
                loadedSamples[entry.SamplePath] = sample;
                return sample.SampleRate;
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or FormatException or NotSupportedException or ArgumentException)
            {
            }
        }

        return 44100;
    }

    static long GetWriteTimeTicks(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
