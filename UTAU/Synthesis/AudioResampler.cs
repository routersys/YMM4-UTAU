namespace UTAU.Synthesis;

internal static class AudioResampler
{
    const int HalfWindowTaps = 24;
    const double KaiserBeta = 9.0;

    public static AudioSample Resample(AudioSample source, int targetSampleRate)
    {
        if (source.SampleRate == targetSampleRate || source.Samples.Length == 0)
            return source.SampleRate == targetSampleRate ? source : new AudioSample(source.Samples, targetSampleRate);

        return new AudioSample(Resample(source.Samples, source.SampleRate, targetSampleRate), targetSampleRate);
    }

    public static double[] Resample(ReadOnlySpan<double> input, int fromSampleRate, int toSampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toSampleRate);

        if (fromSampleRate == toSampleRate)
            return input.ToArray();
        if (input.IsEmpty)
            return [];

        var ratio = toSampleRate / (double)fromSampleRate;
        var cutoff = Math.Min(1.0, ratio);
        var halfWidth = HalfWindowTaps / cutoff;
        var outputLength = (int)Math.Ceiling(input.Length * ratio);
        var output = new double[Math.Max(outputLength, 1)];
        var bessel = BesselI0(KaiserBeta);

        for (var n = 0; n < output.Length; n++)
        {
            var center = n / ratio;
            var first = (int)Math.Ceiling(center - halfWidth);
            var last = (int)Math.Floor(center + halfWidth);
            var sum = 0.0;

            for (var k = Math.Max(first, 0); k <= Math.Min(last, input.Length - 1); k++)
            {
                var distance = center - k;
                var window = distance / halfWidth;
                if (Math.Abs(window) > 1.0)
                    continue;

                var kaiser = BesselI0(KaiserBeta * Math.Sqrt(Math.Max(1.0 - window * window, 0.0))) / bessel;
                sum += input[k] * cutoff * Sinc(cutoff * distance) * kaiser;
            }

            output[n] = sum;
        }

        return output;
    }

    static double Sinc(double x)
    {
        if (Math.Abs(x) < 1e-12)
            return 1.0;
        var argument = Math.PI * x;
        return Math.Sin(argument) / argument;
    }

    static double BesselI0(double x)
    {
        var sum = 1.0;
        var term = 1.0;
        var half = x / 2.0;
        for (var i = 1; i < 64; i++)
        {
            term *= half / i;
            var contribution = term * term;
            sum += contribution;
            if (contribution < sum * 1e-16)
                break;
        }
        return sum;
    }
}
