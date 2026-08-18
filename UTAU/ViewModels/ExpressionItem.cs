using UTAU.Notes;

namespace UTAU.ViewModels;

internal sealed record ExpressionItem(
    string Name,
    bool IsCurve,
    NoteExpression NoteExpression,
    CurveExpression CurveExpression,
    double Minimum,
    double Maximum)
{
    public double Range => Maximum - Minimum;

    public double Clamp(double value) => Math.Clamp(value, Minimum, Maximum);

    public double ToRatio(double value) => Range <= 0.0 ? 0.0 : (Clamp(value) - Minimum) / Range;

    public double FromRatio(double ratio) => Clamp(Minimum + Math.Clamp(ratio, 0.0, 1.0) * Range);

    public double Baseline => ToRatio(Math.Clamp(0.0, Minimum, Maximum));

    public static IReadOnlyList<ExpressionItem> All { get; } =
    [
        new(Texts.NoteVelocity, false, NoteExpression.Velocity, default, 0.0, 200.0),
        new(Texts.NoteIntensity, false, NoteExpression.Intensity, default, 0.0, 200.0),
        new(Texts.NoteModulation, false, NoteExpression.Modulation, default, -200.0, 200.0),
        new(Texts.ParameterFormant, true, default, CurveExpression.Formant, -12.0, 12.0),
        new(Texts.ParameterBreathiness, true, default, CurveExpression.Breathiness, -100.0, 100.0),
    ];
}

internal sealed record ExpressionBarViewModel(NoteViewModel Note, double Left, double Width, double Top, double Height);

internal sealed record PitchHandleViewModel(PitchPoint Point, double Left, double Top, double Size);
