using System.IO;
using UTAU.Models;
using UTAU.Phonemes;
using WorldNet;
using WorldSynthesis = WorldNet.Synthesis;

namespace UTAU.Synthesis;

internal sealed record RenderResult(double[] Samples, int SampleRate, IReadOnlyList<UnitTiming> Timings, double OffsetMilliseconds);

internal sealed class UtauRenderer(RenderSettings settings, RenderCurves curves, AnalysisCache cache, SegmentCache segmentCache)
{
    sealed record UnitSource(
        WorldFeatures Features,
        double RegionStart,
        double RegionEnd,
        double ConsonantEnd,
        double MeanF0);

    sealed record Segment(int StartFrame, int FrameCount, IReadOnlyList<int> TimingIndices);

    sealed record SegmentBuffers(
        double[] F0,
        double[] Spectrogram,
        double[] Aperiodicity,
        double[] WeightSum,
        double[] VoicedWeight,
        double[] LogF0Sum,
        double[] Output);

    readonly record struct AnalysisRequest(
        AudioSample Sample,
        string Path,
        long WriteTimeTicks,
        int StartSample,
        int EndSample,
        double RegionStart,
        double RegionEnd,
        double ConsonantEnd);

    sealed class RenderState : IDisposable
    {
        public required WorldArena Arena { get; init; }

        public required SegmentBuffers Buffers { get; init; }

        public required double[] FrameSpectrum { get; init; }

        public required double[] WarpedSpectrum { get; init; }

        public required double[] FrameAperiodicity { get; init; }

        public static RenderState Create(int widest, int spectrumSize, int outputSamples) => new()
        {
            Arena = new WorldArena(),
            Buffers = new SegmentBuffers(
                new double[widest],
                new double[(long)widest * spectrumSize],
                new double[(long)widest * spectrumSize],
                new double[widest],
                new double[widest],
                new double[widest],
                new double[outputSamples]),
            FrameSpectrum = new double[spectrumSize],
            WarpedSpectrum = new double[spectrumSize],
            FrameAperiodicity = new double[spectrumSize],
        };

        public void Dispose() => Arena.Dispose();
    }

    const long RenderBufferBudgetBytes = 768L * 1024 * 1024;
    const long MaximumFrameElements = 1L << 24;
    const double FrameCountEpsilon = 1e-9;
    const int SegmentGapFrames = 10;

    readonly Dictionary<string, AudioSample> loadedSamples = new(StringComparer.OrdinalIgnoreCase);

    public RenderResult Render(IReadOnlyList<PhonemeUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        var timings = UnitTimingBuilder.Build(units);
        if (timings.Count == 0)
            return new RenderResult([], DetermineSampleRate(units), timings, 0.0);

        var sampleRate = DetermineSampleRate(units);
        var framePeriod = WorldAnalyzer.FramePeriod;
        var fftSize = CheapTrickOption.Create(sampleRate).FftSize;
        var spectrumSize = fftSize / 2 + 1;

        var offset = timings.Min(x => x.AudioStartMilliseconds);
        var totalMilliseconds = timings.Max(x => x.AudioEndMilliseconds) - offset;
        var frameCount = Math.Max((int)Math.Ceiling(totalMilliseconds / framePeriod - FrameCountEpsilon) + 1, 2);

        var segments = BuildSegments(timings, offset, framePeriod, frameCount);
        foreach (var segment in segments)
        {
            if ((long)segment.FrameCount * spectrumSize > MaximumFrameElements)
                throw new InvalidOperationException(Texts.TextTooLongMessage);
        }

        var requests = timings
            .Select(x => ResolveRequest(x.Unit, sampleRate))
            .ToArray();
        AnalyzeInParallel(requests);

        var outputLength = ToSampleIndex(frameCount - 1, framePeriod, sampleRate) + 1;
        var samples = new double[outputLength];
        var widest = segments.Max(x => x.FrameCount);
        var cacheable = ReferenceEquals(curves, RenderCurves.Empty);
        var outputSamples = ToSampleIndex(widest - 1, framePeriod, sampleRate) + 1;

        Parallel.For(
            0,
            segments.Count,
            new ParallelOptions { MaxDegreeOfParallelism = DetermineParallelism(widest, spectrumSize, segments.Count) },
            () => RenderState.Create(widest, spectrumSize, outputSamples),
            (index, _, state) =>
            {
                var limit = index + 1 < segments.Count
                    ? ToSampleIndex(segments[index + 1].StartFrame, framePeriod, sampleRate)
                    : samples.Length;
                RenderOne(segments[index], limit, timings, requests, state, offset, framePeriod, sampleRate, fftSize, spectrumSize, samples, cacheable);
                return state;
            },
            state => state.Dispose());

        return new RenderResult(samples, sampleRate, timings, offset);
    }

    static int DetermineParallelism(int widest, int spectrumSize, int segmentCount)
    {
        var perWorker = (long)widest * spectrumSize * sizeof(double) * 2;
        if (perWorker <= 0)
            return 1;

        var affordable = (int)Math.Clamp(RenderBufferBudgetBytes / perWorker, 1, Environment.ProcessorCount);
        return Math.Min(affordable, Math.Max(segmentCount, 1));
    }

    void RenderOne(
        Segment segment,
        int outputLimit,
        IReadOnlyList<UnitTiming> timings,
        IReadOnlyList<AnalysisRequest?> requests,
        RenderState state,
        double offset,
        double framePeriod,
        int sampleRate,
        int fftSize,
        int spectrumSize,
        double[] samples,
        bool cacheable)
    {
        var produced = ToSampleIndex(segment.FrameCount - 1, framePeriod, sampleRate) + 1;
        var start = ToSampleIndex(segment.StartFrame, framePeriod, sampleRate);
        var length = Math.Min(Math.Min(produced, samples.Length - start), outputLimit - start);
        if (length <= 0)
            return;

        var key = cacheable
            ? BuildSegmentKey(segment, timings, requests, offset, framePeriod, sampleRate)
            : null;

        if (key is not null && segmentCache.TryCopyInto(key, samples.AsSpan(start, length)))
            return;

        RenderSegment(
            segment,
            start,
            length,
            timings,
            requests,
            state.Arena,
            offset,
            framePeriod,
            sampleRate,
            fftSize,
            spectrumSize,
            state.FrameSpectrum,
            state.WarpedSpectrum,
            state.FrameAperiodicity,
            state.Buffers,
            samples);

        if (key is not null)
            segmentCache.Store(key, samples.AsSpan(start, length));
    }

    static int ToSampleIndex(int frame, double framePeriod, int sampleRate)
        => (int)(frame * framePeriod / 1000.0 * sampleRate);

    SegmentKey BuildSegmentKey(
        Segment segment,
        IReadOnlyList<UnitTiming> timings,
        IReadOnlyList<AnalysisRequest?> requests,
        double offset,
        double framePeriod,
        int sampleRate)
    {
        var units = new UnitKey[segment.TimingIndices.Count];

        for (var i = 0; i < units.Length; i++)
        {
            var index = segment.TimingIndices[i];
            var timing = timings[index];
            var unit = timing.Unit;
            var note = unit.Note;

            units[i] = new UnitKey(
                requests[index] is { } request
                    ? new SourceKey(
                        request.Path,
                        request.WriteTimeTicks,
                        request.StartSample,
                        request.EndSample,
                        request.RegionStart,
                        request.RegionEnd,
                        request.ConsonantEnd)
                    : null,
                timing.AudioStartMilliseconds,
                timing.RenderLengthMilliseconds,
                timing.FadeInMilliseconds,
                timing.FadeOutMilliseconds,
                unit.NoteStartMilliseconds,
                unit.NoteLengthMilliseconds,
                unit.Tone,
                note.LengthTicks,
                note.Velocity,
                note.Intensity,
                note.Modulation,
                VibratoKey.From(note.Vibrato),
                [.. note.PitchPoints]);
        }

        return new SegmentKey(settings, segment.StartFrame, segment.FrameCount, sampleRate, offset, framePeriod, units);
    }

    static IReadOnlyList<Segment> BuildSegments(
        IReadOnlyList<UnitTiming> timings,
        double offset,
        double framePeriod,
        int frameCount)
    {
        var groups = new List<(int Start, int End, List<int> Indices)>();

        foreach (var index in Enumerable.Range(0, timings.Count).OrderBy(x => timings[x].AudioStartMilliseconds))
        {
            var start = Math.Clamp((int)Math.Floor((timings[index].AudioStartMilliseconds - offset) / framePeriod), 0, frameCount - 1);
            var end = Math.Clamp((int)Math.Ceiling((timings[index].AudioEndMilliseconds - offset) / framePeriod), start, frameCount - 1);

            if (groups.Count > 0 && start <= groups[^1].End + SegmentGapFrames)
            {
                var last = groups[^1];
                last.Indices.Add(index);
                groups[^1] = (last.Start, Math.Max(last.End, end), last.Indices);
                continue;
            }

            groups.Add((start, end, [index]));
        }

        var segments = new List<Segment>(groups.Count);
        for (var index = 0; index < groups.Count; index++)
        {
            var start = index == 0 ? 0 : groups[index].Start;
            var end = index + 1 < groups.Count ? groups[index + 1].Start : frameCount - 1;
            segments.Add(new Segment(start, end - start + 1, groups[index].Indices));
        }
        return segments;
    }

    void RenderSegment(
        Segment segment,
        int outputStart,
        int outputLength,
        IReadOnlyList<UnitTiming> timings,
        IReadOnlyList<AnalysisRequest?> requests,
        WorldArena arena,
        double offset,
        double framePeriod,
        int sampleRate,
        int fftSize,
        int spectrumSize,
        double[] frameSpectrum,
        double[] warpedSpectrum,
        double[] frameAperiodicity,
        SegmentBuffers buffers,
        double[] samples)
    {
        var frameCount = segment.FrameCount;
        var elements = frameCount * spectrumSize;
        var f0 = buffers.F0;
        var spectrogram = buffers.Spectrogram;
        var aperiodicity = buffers.Aperiodicity;
        var weightSum = buffers.WeightSum;
        var voicedWeight = buffers.VoicedWeight;
        var logF0Sum = buffers.LogF0Sum;

        Array.Clear(f0, 0, frameCount);
        Array.Clear(spectrogram, 0, elements);
        Array.Clear(aperiodicity, 0, elements);
        Array.Clear(weightSum, 0, frameCount);
        Array.Clear(voicedWeight, 0, frameCount);
        Array.Clear(logF0Sum, 0, frameCount);

        foreach (var index in segment.TimingIndices)
        {
            var source = BuildSource(requests[index], arena);
            if (source is null)
                continue;

            AccumulateUnit(
                timings[index],
                source,
                offset,
                framePeriod,
                segment.StartFrame,
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

        var produced = ToSampleIndex(frameCount - 1, framePeriod, sampleRate) + 1;
        var output = buffers.Output.AsSpan(0, produced);
        output.Clear();
        WorldSynthesis.Synthesize(
            f0.AsSpan(0, frameCount),
            spectrogram.AsSpan(0, elements),
            aperiodicity.AsSpan(0, elements),
            fftSize,
            framePeriod,
            sampleRate,
            output,
            arena);

        Array.Copy(buffers.Output, 0, samples, outputStart, outputLength);
    }

    void AccumulateUnit(
        UnitTiming timing,
        UnitSource source,
        double offset,
        double framePeriod,
        int segmentStartFrame,
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
        var startFrame = Math.Max((int)Math.Floor((timing.AudioStartMilliseconds - offset) / framePeriod) - segmentStartFrame, 0);
        var endFrame = Math.Min((int)Math.Ceiling((timing.AudioEndMilliseconds - offset) / framePeriod) - segmentStartFrame, frameCount - 1);
        var gain = settings.Gain * Math.Clamp(note.Intensity, 0.0, 200.0) / 100.0;
        var baseFormantRatio = settings.FormantRatio;
        var hasFormantCurve = curves.HasFormant;
        var hasBreathinessCurve = curves.HasBreathiness;
        var map = TimeMap.Create(source.RegionStart, source.ConsonantEnd, source.RegionEnd, timing.RenderLengthMilliseconds, note.Velocity, settings.StretchMode);
        var modulation = Math.Clamp(note.Modulation, -200.0, 200.0) / 100.0;

        for (var frame = startFrame; frame <= endFrame; frame++)
        {
            var absolute = offset + (segmentStartFrame + frame) * framePeriod;
            var elapsed = absolute - timing.AudioStartMilliseconds;
            var weight = timing.GetWeight(elapsed);
            if (weight <= 0.0)
                continue;

            var sourceMilliseconds = map.Map(elapsed);
            var frameIndex = source.Features.GetFrameIndex(sourceMilliseconds);
            SampleFeatures(source.Features, frameIndex, frameSpectrum, frameAperiodicity, out var sourceF0);

            var formantRatio = hasFormantCurve
                ? SpectrumTransform.FormantRatioFromSemitones(
                    Math.Clamp(settings.FormantSemitones + curves.Formant(absolute), RenderSettings.MinimumFormant, RenderSettings.MaximumFormant))
                : baseFormantRatio;
            var breathiness = hasBreathinessCurve
                ? Math.Clamp(settings.Breathiness + curves.Breathiness(absolute), RenderSettings.MinimumBreathiness, RenderSettings.MaximumBreathiness)
                : settings.Breathiness;

            SpectrumTransform.WarpFormant(frameSpectrum, warpedSpectrum, formantRatio);
            SpectrumTransform.ApplyBrightness(warpedSpectrum, settings.Brightness);
            SpectrumTransform.ApplyGain(warpedSpectrum, gain);
            SpectrumTransform.ApplyBreathiness(frameAperiodicity, breathiness);

            var baseIndex = (long)frame * spectrumSize;
            for (var k = 0; k < spectrumSize; k++)
            {
                spectrogram[baseIndex + k] += weight * warpedSpectrum[k];
                aperiodicity[baseIndex + k] += weight * frameAperiodicity[k];
            }
            weightSum[frame] += weight;

            if (sourceF0 <= 0.0)
                continue;

            var progress = unit.NoteLengthMilliseconds > 0.0
                ? (absolute - unit.NoteStartMilliseconds) / unit.NoteLengthMilliseconds
                : 0.0;
            var cents = note.EvaluatePitchOffsetCents(progress, unit.NoteLengthMilliseconds);
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

    AnalysisRequest? ResolveRequest(PhonemeUnit unit, int sampleRate)
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

        return new AnalysisRequest(
            sample,
            entry.SamplePath,
            GetWriteTimeTicks(entry.SamplePath),
            startSample,
            endSample,
            regionStart,
            regionEnd,
            consonantEnd);
    }

    void AnalyzeInParallel(IReadOnlyList<AnalysisRequest?> requests)
    {
        var pending = requests
            .OfType<AnalysisRequest>()
            .DistinctBy(x => (x.Path, x.WriteTimeTicks, x.StartSample, x.EndSample))
            .ToArray();
        if (pending.Length < 2)
            return;

        Parallel.ForEach(
            pending,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            () => new WorldArena(),
            (request, _, arena) =>
            {
                Analyze(request, arena);
                return arena;
            },
            arena => arena.Dispose());
    }

    WorldFeatures Analyze(AnalysisRequest request, WorldArena arena)
        => cache.GetOrAdd(
            request.Path,
            request.WriteTimeTicks,
            request.StartSample,
            request.EndSample,
            settings.Estimator,
            () => WorldAnalyzer.Analyze(
                new AudioSample(request.Sample.Samples[request.StartSample..request.EndSample], request.Sample.SampleRate),
                settings.Estimator,
                arena,
                request.StartSample * 1000.0 / request.Sample.SampleRate));

    UnitSource? BuildSource(AnalysisRequest? request, WorldArena arena)
    {
        if (request is not { } value)
            return null;

        var features = Analyze(value, arena);
        return new UnitSource(
            features,
            value.RegionStart,
            value.RegionEnd,
            value.ConsonantEnd,
            ComputeMeanF0(features, value.RegionStart, value.RegionEnd));
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
