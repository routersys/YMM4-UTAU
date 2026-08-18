using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Notes;

internal sealed class PitchPoint : UndoRedoable
{
    public const int MinimumTicks = -TimeBase.TicksPerWholeNote;
    public const int MaximumTicks = TimeBase.TicksPerWholeNote * 16;
    public const double MinimumCents = -2400.0;
    public const double MaximumCents = 2400.0;

    int ticks;
    double cents;
    PitchPointShape shape = PitchPointShape.SCurve;

    public PitchPoint()
    {
    }

    public PitchPoint(int ticks, double cents, PitchPointShape shape = PitchPointShape.SCurve)
    {
        this.ticks = Math.Clamp(ticks, MinimumTicks, MaximumTicks);
        this.cents = Math.Clamp(cents, MinimumCents, MaximumCents);
        this.shape = shape;
    }

    [Display(GroupName = nameof(Texts.NoteGroupPortamento), Name = nameof(Texts.PitchPointTime), Description = nameof(Texts.PitchPointTimeDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", nameof(Texts.UnitTick), 0.0, 1920.0, Delay = -1, ResourceType = typeof(Texts))]
    [Range(MinimumTicks, MaximumTicks)]
    [DefaultValue(0)]
    public int Ticks
    {
        get => ticks;
        set => Set(ref ticks, Math.Clamp(value, MinimumTicks, MaximumTicks));
    }

    [Display(GroupName = nameof(Texts.NoteGroupPortamento), Name = nameof(Texts.PitchPointCents), Description = nameof(Texts.PitchPointCentsDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "", -1200.0, 1200.0, Delay = -1)]
    [Range(MinimumCents, MaximumCents)]
    [DefaultValue(0.0)]
    public double Cents
    {
        get => cents;
        set => Set(ref cents, Math.Clamp(value, MinimumCents, MaximumCents));
    }

    [Display(GroupName = nameof(Texts.NoteGroupPortamento), Name = nameof(Texts.PitchPointShape), ResourceType = typeof(Texts))]
    [EnumComboBox]
    public PitchPointShape Shape
    {
        get => shape;
        set => Set(ref shape, value);
    }

    public PitchPoint Clone() => new(ticks, cents, shape);

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
