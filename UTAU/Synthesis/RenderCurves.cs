namespace UTAU.Synthesis;

internal sealed class RenderCurves(double[] formantSemitones, double[] breathiness, double intervalMilliseconds)
{
    public static RenderCurves Empty { get; } = new([], [], 1.0);

    public bool HasFormant => formantSemitones.Length > 0;

    public bool HasBreathiness => breathiness.Length > 0;

    public double IntervalMilliseconds => intervalMilliseconds;

    public double Formant(double milliseconds) => Sample(formantSemitones, milliseconds);

    public double Breathiness(double milliseconds) => Sample(breathiness, milliseconds);

    double Sample(double[] values, double milliseconds)
    {
        if (values.Length == 0)
            return 0.0;
        if (intervalMilliseconds <= 0.0)
            return values[0];

        var position = milliseconds / intervalMilliseconds;
        if (position <= 0.0)
            return values[0];
        if (position >= values.Length - 1)
            return values[^1];

        var index = (int)position;
        var fraction = position - index;
        return values[index] * (1.0 - fraction) + values[index + 1] * fraction;
    }
}
