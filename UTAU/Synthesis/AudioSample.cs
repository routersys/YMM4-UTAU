namespace UTAU.Synthesis;

internal sealed class AudioSample(double[] samples, int sampleRate)
{
    public double[] Samples { get; } = samples;

    public int SampleRate { get; } = sampleRate;

    public double DurationMilliseconds => Samples.Length * 1000.0 / SampleRate;

    public int MillisecondsToSamples(double milliseconds)
        => (int)Math.Round(milliseconds * SampleRate / 1000.0, MidpointRounding.AwayFromZero);
}
