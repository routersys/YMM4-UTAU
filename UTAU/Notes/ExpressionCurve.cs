using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Notes;

internal enum NoteExpression
{
    [Display(Name = nameof(Texts.NoteVelocity), ResourceType = typeof(Texts))]
    Velocity,

    [Display(Name = nameof(Texts.NoteIntensity), ResourceType = typeof(Texts))]
    Intensity,

    [Display(Name = nameof(Texts.NoteModulation), ResourceType = typeof(Texts))]
    Modulation,
}

internal enum CurveExpression
{
    [Display(Name = nameof(Texts.ParameterFormant), ResourceType = typeof(Texts))]
    Formant,

    [Display(Name = nameof(Texts.ParameterBreathiness), ResourceType = typeof(Texts))]
    Breathiness,
}

internal sealed class ExpressionCurve : UndoRedoable
{
    public const int IntervalTicks = TimeBase.TicksPerQuarterNote / 16;

    double[] values = [];

    public double[] Values
    {
        get => values;
        set => Set(ref values, value ?? []);
    }

    public bool IsEmpty => values.Length == 0;

    public int LengthTicks => values.Length * IntervalTicks;

    public static int ToIndex(double ticks) => (int)Math.Floor(ticks / IntervalTicks);

    public double Evaluate(double ticks)
    {
        if (values.Length == 0)
            return 0.0;

        var position = ticks / IntervalTicks;
        if (position <= 0.0)
            return values[0];
        if (position >= values.Length - 1)
            return values[^1];

        var index = (int)position;
        var fraction = position - index;
        return values[index] * (1.0 - fraction) + values[index + 1] * fraction;
    }

    public double[] CreateWorkingCopy(int requiredTicks)
    {
        var length = Math.Max(ToIndex(requiredTicks) + 2, 2);
        var copy = new double[length];
        Array.Copy(values, copy, Math.Min(values.Length, length));
        return copy;
    }

    public void Commit(double[] working)
    {
        ArgumentNullException.ThrowIfNull(working);
        Values = IsAllZero(working) ? [] : working;
    }

    public ExpressionCurve Clone() => new() { Values = [.. values] };

    static bool IsAllZero(double[] samples)
    {
        foreach (var value in samples)
            if (Math.Abs(value) > double.Epsilon)
                return false;
        return true;
    }
}
