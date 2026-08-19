namespace UTAU.Notes;

internal sealed record ResampledCurves(double[] Formant, double[] Breathiness, double IntervalMilliseconds);

internal static class ExpressionCurveResampler
{
    public static ResampledCurves Resample(ExpressionCurve formant, ExpressionCurve breathiness, TempoMap tempoMap)
    {
        ArgumentNullException.ThrowIfNull(formant);
        ArgumentNullException.ThrowIfNull(breathiness);
        ArgumentNullException.ThrowIfNull(tempoMap);

        var interval = tempoMap.MinimumMillisecondsPerTick * ExpressionCurve.IntervalTicks;
        if (interval <= 0.0)
            return new ResampledCurves([], [], 1.0);

        var count = (int)Math.Ceiling(tempoMap.TotalMilliseconds / interval) + 1;
        return new ResampledCurves(
            Sample(formant, tempoMap, interval, count),
            Sample(breathiness, tempoMap, interval, count),
            interval);
    }

    static double[] Sample(ExpressionCurve curve, TempoMap tempoMap, double intervalMilliseconds, int count)
    {
        if (curve.IsEmpty)
            return [];

        var samples = new double[count];
        for (var index = 0; index < count; index++)
            samples[index] = curve.Evaluate(tempoMap.ToTicks(index * intervalMilliseconds));
        return samples;
    }
}
