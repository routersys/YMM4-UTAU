using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Notes;

internal sealed class PitchPoint : UndoRedoable
{
    double milliseconds;
    double cents;
    PitchPointShape shape = PitchPointShape.SCurve;

    public PitchPoint()
    {
    }

    public PitchPoint(double milliseconds, double cents, PitchPointShape shape = PitchPointShape.SCurve)
    {
        this.milliseconds = milliseconds;
        this.cents = cents;
        this.shape = shape;
    }

    public double Milliseconds
    {
        get => milliseconds;
        set => Set(ref milliseconds, value);
    }

    public double Cents
    {
        get => cents;
        set => Set(ref cents, value);
    }

    public PitchPointShape Shape
    {
        get => shape;
        set => Set(ref shape, value);
    }

    public PitchPoint Clone() => new(milliseconds, cents, shape);

    public static double Interpolate(double from, double to, double progress, PitchPointShape shape)
    {
        var t = Math.Clamp(progress, 0.0, 1.0);
        var weight = shape switch
        {
            PitchPointShape.Linear => t,
            PitchPointShape.RCurve => Math.Sin(Math.PI * t / 2.0),
            PitchPointShape.JCurve => 1.0 - Math.Cos(Math.PI * t / 2.0),
            _ => (1.0 - Math.Cos(Math.PI * t)) / 2.0,
        };
        return from + (to - from) * weight;
    }
}
