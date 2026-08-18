using WorldNet;

namespace UTAU.Synthesis;

internal static class WorldAnalyzer
{
    public const double FramePeriod = 5.0;
    public const double DetectionF0Floor = 55.0;
    public const double DetectionF0Ceil = 1100.0;

    public static WorldFeatures Analyze(AudioSample sample, F0Estimator estimator, WorldArena arena, double startMilliseconds = 0.0)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(arena);

        var fs = sample.SampleRate;
        var x = sample.Samples;
        var cheapTrickOption = CheapTrickOption.Create(fs);
        var fftSize = cheapTrickOption.FftSize;
        var spectrumSize = fftSize / 2 + 1;

        if (x.Length == 0)
            return new WorldFeatures(fs, FramePeriod, fftSize, startMilliseconds, [], [], []);

        using var scope = arena.BeginScope();

        var frameCount = estimator == F0Estimator.Dio
            ? Dio.GetSamplesForDio(fs, x.Length, FramePeriod)
            : Harvest.GetSamplesForHarvest(fs, x.Length, FramePeriod);
        frameCount = Math.Max(frameCount, 1);

        var temporalPositions = arena.AllocateDouble(frameCount);
        var rawF0 = arena.AllocateDouble(frameCount);
        var f0 = arena.AllocateDouble(frameCount);

        if (estimator == F0Estimator.Dio)
        {
            var option = DioOption.Default with
            {
                FramePeriod = FramePeriod,
                F0Floor = DetectionF0Floor,
                F0Ceil = DetectionF0Ceil,
            };
            Dio.Estimate(x, fs, option, temporalPositions, rawF0, arena);
            StoneMask.Refine(x, fs, temporalPositions, rawF0, f0, arena);
        }
        else
        {
            var option = HarvestOption.Default with
            {
                FramePeriod = FramePeriod,
                F0Floor = DetectionF0Floor,
                F0Ceil = DetectionF0Ceil,
            };
            Harvest.Estimate(x, fs, option, temporalPositions, f0, arena);
        }

        var spectrogram = arena.AllocateDouble(frameCount * spectrumSize);
        CheapTrick.Estimate(x, fs, cheapTrickOption, temporalPositions, f0, spectrogram, arena);

        var aperiodicity = arena.AllocateDouble(frameCount * spectrumSize);
        D4C.Estimate(x, fs, D4COption.Default, temporalPositions, f0, fftSize, aperiodicity, arena);

        return new WorldFeatures(
            fs,
            FramePeriod,
            fftSize,
            startMilliseconds,
            f0.ToArray(),
            spectrogram.ToArray(),
            aperiodicity.ToArray());
    }
}
