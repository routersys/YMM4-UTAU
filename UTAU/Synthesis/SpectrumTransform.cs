namespace UTAU.Synthesis;

internal static class SpectrumTransform
{
    public const double MinimumPower = 1e-30;
    public const double MinimumAperiodicity = 1e-6;
    public const double MaximumAperiodicity = 1.0 - 1e-6;

    public static void WarpFormant(ReadOnlySpan<double> source, Span<double> destination, double ratio)
    {
        if (source.Length < 2 || Math.Abs(ratio - 1.0) < 1e-9 || !double.IsFinite(ratio) || ratio <= 0.0)
        {
            source.CopyTo(destination);
            return;
        }

        var last = source.Length - 1;
        for (var k = 0; k <= last; k++)
        {
            var position = k / ratio;
            if (position <= 0.0)
            {
                destination[k] = source[0];
                continue;
            }
            if (position >= last)
            {
                destination[k] = source[last];
                continue;
            }

            var index = (int)position;
            destination[k] = InterpolatePower(source[index], source[index + 1], position - index);
        }
    }

    public static void ApplyBrightness(Span<double> spectrum, double decibelsAtNyquist)
    {
        if (spectrum.Length < 2 || Math.Abs(decibelsAtNyquist) < 1e-9)
            return;

        var last = spectrum.Length - 1;
        for (var k = 0; k <= last; k++)
        {
            var gainDecibels = decibelsAtNyquist * k / last;
            spectrum[k] = Math.Max(spectrum[k] * Math.Pow(10.0, gainDecibels / 10.0), MinimumPower);
        }
    }

    public static void ApplyGain(Span<double> spectrum, double gain)
    {
        if (Math.Abs(gain - 1.0) < 1e-12)
            return;

        var power = gain * gain;
        for (var k = 0; k < spectrum.Length; k++)
            spectrum[k] = Math.Max(spectrum[k] * power, MinimumPower);
    }

    public static void ApplyBreathiness(Span<double> aperiodicity, double breathiness)
    {
        var exponent = Math.Pow(2.0, -breathiness / 100.0);
        var isIdentity = Math.Abs(exponent - 1.0) < 1e-12;
        for (var k = 0; k < aperiodicity.Length; k++)
        {
            var value = Clamp(aperiodicity[k]);
            aperiodicity[k] = isIdentity ? value : Clamp(Math.Pow(value, exponent));
        }
    }

    public static double ToAmplitude(double power) => Math.Sqrt(power);

    public static double ToPower(double amplitude) => amplitude * amplitude;

    public static double Clamp(double aperiodicity)
        => double.IsNaN(aperiodicity)
            ? MaximumAperiodicity
            : Math.Clamp(aperiodicity, MinimumAperiodicity, MaximumAperiodicity);

    public static double InterpolatePower(double low, double high, double fraction)
    {
        if (fraction <= 0.0)
            return low;
        if (fraction >= 1.0)
            return high;

        var safeLow = Math.Max(low, MinimumPower);
        var safeHigh = Math.Max(high, MinimumPower);
        return Math.Exp(Math.Log(safeLow) * (1.0 - fraction) + Math.Log(safeHigh) * fraction);
    }

    public static double FormantRatioFromSemitones(double semitones)
        => Math.Pow(2.0, semitones / 12.0);
}
