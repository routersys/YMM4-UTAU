using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Notes;

internal sealed class VibratoSettings : UndoRedoable
{
    double lengthPercent;
    double periodMilliseconds = 175.0;
    double depthCents = 25.0;
    double fadeInPercent = 20.0;
    double fadeOutPercent = 20.0;
    double phasePercent;
    double offsetPercent;

    [Display(GroupName = nameof(Texts.NoteGroupVibrato), Name = nameof(Texts.VibratoLength), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", 0.0, 100.0, Delay = -1)]
    [Range(0.0, 100.0)]
    [DefaultValue(0.0)]
    public double LengthPercent
    {
        get => lengthPercent;
        set => Set(ref lengthPercent, Math.Clamp(value, 0.0, 100.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupVibrato), Name = nameof(Texts.VibratoPeriod), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "ms", 10.0, 1000.0, Delay = -1)]
    [Range(10.0, 1000.0)]
    [DefaultValue(175.0)]
    public double PeriodMilliseconds
    {
        get => periodMilliseconds;
        set => Set(ref periodMilliseconds, Math.Clamp(value, 10.0, 1000.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupVibrato), Name = nameof(Texts.VibratoDepth), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "", 0.0, 400.0, Delay = -1)]
    [Range(0.0, 400.0)]
    [DefaultValue(25.0)]
    public double DepthCents
    {
        get => depthCents;
        set => Set(ref depthCents, Math.Clamp(value, 0.0, 400.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupVibrato), Name = nameof(Texts.VibratoFadeIn), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", 0.0, 100.0, Delay = -1)]
    [Range(0.0, 100.0)]
    [DefaultValue(20.0)]
    public double FadeInPercent
    {
        get => fadeInPercent;
        set => Set(ref fadeInPercent, Math.Clamp(value, 0.0, 100.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupVibrato), Name = nameof(Texts.VibratoFadeOut), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", 0.0, 100.0, Delay = -1)]
    [Range(0.0, 100.0)]
    [DefaultValue(20.0)]
    public double FadeOutPercent
    {
        get => fadeOutPercent;
        set => Set(ref fadeOutPercent, Math.Clamp(value, 0.0, 100.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupVibrato), Name = nameof(Texts.VibratoPhase), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", -100.0, 100.0, Delay = -1)]
    [Range(-100.0, 100.0)]
    [DefaultValue(0.0)]
    public double PhasePercent
    {
        get => phasePercent;
        set => Set(ref phasePercent, Math.Clamp(value, -100.0, 100.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupVibrato), Name = nameof(Texts.VibratoOffset), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", -100.0, 100.0, Delay = -1)]
    [Range(-100.0, 100.0)]
    [DefaultValue(0.0)]
    public double OffsetPercent
    {
        get => offsetPercent;
        set => Set(ref offsetPercent, Math.Clamp(value, -100.0, 100.0));
    }

    [Browsable(false)]
    public bool IsEnabled => LengthPercent > 0.0 && DepthCents > 0.0;

    public VibratoSettings Clone() => new()
    {
        LengthPercent = LengthPercent,
        PeriodMilliseconds = PeriodMilliseconds,
        DepthCents = DepthCents,
        FadeInPercent = FadeInPercent,
        FadeOutPercent = FadeOutPercent,
        PhasePercent = PhasePercent,
        OffsetPercent = OffsetPercent,
    };

    public double Evaluate(double millisecondsFromNoteStart, double noteLengthMilliseconds)
    {
        if (!IsEnabled || noteLengthMilliseconds <= 0.0)
            return 0.0;

        var vibratoLength = noteLengthMilliseconds * LengthPercent / 100.0;
        if (vibratoLength <= 0.0)
            return 0.0;

        var start = noteLengthMilliseconds - vibratoLength;
        var elapsed = millisecondsFromNoteStart - start;
        if (elapsed < 0.0 || elapsed > vibratoLength)
            return 0.0;

        var progress = elapsed / vibratoLength;
        var fadeIn = FadeInPercent / 100.0;
        var fadeOut = FadeOutPercent / 100.0;
        var envelope = 1.0;
        if (fadeIn > 0.0 && progress < fadeIn)
            envelope *= progress / fadeIn;
        if (fadeOut > 0.0 && progress > 1.0 - fadeOut)
            envelope *= (1.0 - progress) / fadeOut;

        var phase = elapsed / PeriodMilliseconds + PhasePercent / 100.0;
        return DepthCents * envelope * (Math.Sin(2.0 * Math.PI * phase) + OffsetPercent / 100.0);
    }
}
