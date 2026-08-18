namespace UTAU.Synthesis;

internal sealed class WorldFeatures(
    int sampleRate,
    double framePeriod,
    int fftSize,
    double startMilliseconds,
    double[] f0,
    double[] spectrogram,
    double[] aperiodicity)
{
    public int SampleRate { get; } = sampleRate;

    public double FramePeriod { get; } = framePeriod;

    public int FftSize { get; } = fftSize;

    public int SpectrumSize { get; } = fftSize / 2 + 1;

    public double StartMilliseconds { get; } = startMilliseconds;

    public double[] F0 { get; } = f0;

    public double[] Spectrogram { get; } = spectrogram;

    public double[] Aperiodicity { get; } = aperiodicity;

    public int FrameCount => F0.Length;

    public long EstimatedBytes => ((long)Spectrogram.Length + Aperiodicity.Length + F0.Length) * sizeof(double);

    public double GetFrameIndex(double milliseconds)
        => (milliseconds - StartMilliseconds) / FramePeriod;

    public double GetVoicedGeometricMeanF0()
    {
        var sum = 0.0;
        var count = 0;
        foreach (var value in F0)
        {
            if (value <= 0.0)
                continue;
            sum += Math.Log(value);
            count++;
        }
        return count == 0 ? 0.0 : Math.Exp(sum / count);
    }
}
