using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using UTAU.Models;
using UTAU.Views;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU.Notes;

internal sealed class UTAUNote : UndoRedoable
{
    public const int DefaultLengthTicks = TimeBase.TicksPerQuarterNote / 2;
    public const int MinimumLengthTicks = 15;
    public const int MaximumLengthTicks = TimeBase.TicksPerWholeNote * 16;
    public const double DefaultFadeInMilliseconds = 5.0;
    public const double DefaultFadeOutMilliseconds = 35.0;
    public const double FollowOtoValue = 0.0;
    public const double FollowScoreValue = 0.0;
    public const string RestLyric = "R";

    string lyric = string.Empty;
    int tone = MusicalTone.MiddleC.NoteNumber;
    int lengthTicks = DefaultLengthTicks;
    double velocity = 100.0;
    double intensity = 100.0;
    double modulation;
    double startPointMilliseconds;
    double tempoOverride;
    double preutteranceOverride;
    double overlapOverride;
    double fadeInMilliseconds = DefaultFadeInMilliseconds;
    double fadeOutMilliseconds = DefaultFadeOutMilliseconds;

    public UTAUNote()
    {
        SubscribeChildUndoRedoable(Vibrato);
        SubscribeObservableCollectionChangedAndChild(PitchPoints);
    }

    [Display(GroupName = nameof(Texts.NoteGroupBasic), Name = nameof(Texts.NoteLyric), Description = nameof(Texts.NoteLyricDescription), ResourceType = typeof(Texts))]
    [TextEditor]
    public string Lyric
    {
        get => lyric;
        set => Set(ref lyric, value ?? string.Empty);
    }

    [Display(GroupName = nameof(Texts.NoteGroupBasic), Name = nameof(Texts.NoteTone), ResourceType = typeof(Texts))]
    [ToneComboBox]
    public int Tone
    {
        get => tone;
        set => Set(ref tone, ClampTone(value));
    }

    [Display(GroupName = nameof(Texts.NoteGroupBasic), Name = nameof(Texts.NoteLength), Description = nameof(Texts.NoteLengthDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", nameof(Texts.UnitTick), MinimumLengthTicks, TimeBase.TicksPerWholeNote, Delay = -1, ResourceType = typeof(Texts))]
    [Range(MinimumLengthTicks, MaximumLengthTicks)]
    [DefaultValue(DefaultLengthTicks)]
    public int LengthTicks
    {
        get => lengthTicks;
        set => Set(ref lengthTicks, ClampLength(value));
    }

    public static int ClampTone(int value) => Math.Clamp(value, 0, 127);

    public static int ClampLength(int value) => Math.Clamp(value, MinimumLengthTicks, MaximumLengthTicks);

    public void PreviewTone(int value) => SetWithoutUndoRedo(ref tone, ClampTone(value), nameof(Tone));

    public void PreviewLength(int value) => SetWithoutUndoRedo(ref lengthTicks, ClampLength(value), nameof(LengthTicks));

    [Display(GroupName = nameof(Texts.NoteGroupBasic), Name = nameof(Texts.NoteTempo), Description = nameof(Texts.NoteTempoDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "BPM", 0.0, TimeBase.MaximumTempo, Delay = -1)]
    [Range(0.0, TimeBase.MaximumTempo)]
    [DefaultValue(FollowScoreValue)]
    public double TempoOverride
    {
        get => tempoOverride;
        set => Set(ref tempoOverride, value <= FollowScoreValue
            ? FollowScoreValue
            : Math.Clamp(value, TimeBase.MinimumTempo, TimeBase.MaximumTempo));
    }

    [Display(GroupName = nameof(Texts.NoteGroupExpression), Name = nameof(Texts.NoteVelocity), Description = nameof(Texts.NoteVelocityDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "", 0.0, 200.0, Delay = -1)]
    [Range(0.0, 200.0)]
    [DefaultValue(100.0)]
    public double Velocity
    {
        get => velocity;
        set => Set(ref velocity, Math.Clamp(value, 0.0, 200.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupExpression), Name = nameof(Texts.NoteIntensity), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", 0.0, 200.0, Delay = -1)]
    [Range(0.0, 200.0)]
    [DefaultValue(100.0)]
    public double Intensity
    {
        get => intensity;
        set => Set(ref intensity, Math.Clamp(value, 0.0, 200.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupExpression), Name = nameof(Texts.NoteModulation), Description = nameof(Texts.ParameterModulationDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "%", -200.0, 200.0, Delay = -1)]
    [Range(-200.0, 200.0)]
    [DefaultValue(0.0)]
    public double Modulation
    {
        get => modulation;
        set => Set(ref modulation, Math.Clamp(value, -200.0, 200.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupTiming), Name = nameof(Texts.NotePreutterance), Description = nameof(Texts.NoteFollowOtoDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "ms", 0.0, 500.0, Delay = -1)]
    [Range(0.0, 5000.0)]
    [DefaultValue(FollowOtoValue)]
    public double PreutteranceOverride
    {
        get => preutteranceOverride;
        set => Set(ref preutteranceOverride, Math.Clamp(value, 0.0, 5000.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupTiming), Name = nameof(Texts.NoteOverlap), Description = nameof(Texts.NoteFollowOtoDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "ms", 0.0, 500.0, Delay = -1)]
    [Range(0.0, 5000.0)]
    [DefaultValue(FollowOtoValue)]
    public double OverlapOverride
    {
        get => overlapOverride;
        set => Set(ref overlapOverride, Math.Clamp(value, 0.0, 5000.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupTiming), Name = nameof(Texts.NoteStartPoint), Description = nameof(Texts.NoteStartPointDescription), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "ms", 0.0, 500.0, Delay = -1)]
    [Range(0.0, 5000.0)]
    [DefaultValue(0.0)]
    public double StartPointMilliseconds
    {
        get => startPointMilliseconds;
        set => Set(ref startPointMilliseconds, Math.Clamp(value, 0.0, 5000.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupTiming), Name = nameof(Texts.NoteFadeIn), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "ms", 0.0, 200.0, Delay = -1)]
    [Range(0.0, 5000.0)]
    [DefaultValue(DefaultFadeInMilliseconds)]
    public double FadeInMilliseconds
    {
        get => fadeInMilliseconds;
        set => Set(ref fadeInMilliseconds, Math.Clamp(value, 0.0, 5000.0));
    }

    [Display(GroupName = nameof(Texts.NoteGroupTiming), Name = nameof(Texts.NoteFadeOut), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "ms", 0.0, 200.0, Delay = -1)]
    [Range(0.0, 5000.0)]
    [DefaultValue(DefaultFadeOutMilliseconds)]
    public double FadeOutMilliseconds
    {
        get => fadeOutMilliseconds;
        set => Set(ref fadeOutMilliseconds, Math.Clamp(value, 0.0, 5000.0));
    }

    [Display(AutoGenerateField = true)]
    public VibratoSettings Vibrato { get; } = new();

    [Browsable(false)]
    public ObservableCollection<PitchPoint> PitchPoints { get; } = [];

    [Browsable(false)]
    public bool IsRest => Lyric.Length == 0 || Lyric == RestLyric || Lyric == "-";

    [Browsable(false)]
    public MusicalTone MusicalTone => new(Tone);

    public UTAUNote Clone()
    {
        var clone = new UTAUNote
        {
            Lyric = Lyric,
            Tone = Tone,
            LengthTicks = LengthTicks,
            TempoOverride = TempoOverride,
            Velocity = Velocity,
            Intensity = Intensity,
            Modulation = Modulation,
            StartPointMilliseconds = StartPointMilliseconds,
            PreutteranceOverride = PreutteranceOverride,
            OverlapOverride = OverlapOverride,
            FadeInMilliseconds = FadeInMilliseconds,
            FadeOutMilliseconds = FadeOutMilliseconds,
        };
        Vibrato.CopyTo(clone.Vibrato);
        foreach (var point in PitchPoints)
            clone.PitchPoints.Add(point.Clone());
        return clone;
    }

    public double EvaluatePitchOffsetCents(double progress, double noteLengthMilliseconds)
        => EvaluatePortamentoCents(progress * LengthTicks)
            + Vibrato.Evaluate(progress * noteLengthMilliseconds, noteLengthMilliseconds);

    public double EvaluatePortamentoCents(double ticksFromNoteStart)
    {
        if (PitchPoints.Count == 0)
            return 0.0;
        if (PitchPoints.Count == 1)
            return PitchPoints[0].Cents;

        if (ticksFromNoteStart < PitchPoints[0].Ticks)
            return PitchPoints[0].Cents;

        for (var i = 0; i < PitchPoints.Count - 1; i++)
        {
            var current = PitchPoints[i];
            var next = PitchPoints[i + 1];
            if (ticksFromNoteStart > next.Ticks)
                continue;

            var span = (double)next.Ticks - current.Ticks;
            var progress = span <= 0.0 ? 1.0 : (ticksFromNoteStart - current.Ticks) / span;
            return PitchPoint.Interpolate(current.Cents, next.Cents, progress, current.Shape);
        }

        return PitchPoints[^1].Cents;
    }
}
