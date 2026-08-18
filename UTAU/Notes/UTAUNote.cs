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
    public const double DefaultLengthMilliseconds = 200.0;
    public const double DefaultFadeInMilliseconds = 5.0;
    public const double DefaultFadeOutMilliseconds = 35.0;
    public const double FollowOtoValue = 0.0;
    public const string RestLyric = "R";

    string lyric = string.Empty;
    int tone = MusicalTone.MiddleC.NoteNumber;
    double lengthMilliseconds = DefaultLengthMilliseconds;
    double velocity = 100.0;
    double intensity = 100.0;
    double modulation;
    double startPointMilliseconds;
    double preutteranceOverride;
    double overlapOverride;
    double fadeInMilliseconds = DefaultFadeInMilliseconds;
    double fadeOutMilliseconds = DefaultFadeOutMilliseconds;
    VibratoSettings vibrato = new();
    ObservableCollection<PitchPoint> pitchPoints = [];

    [Display(GroupName = nameof(Texts.NoteGroupBasic), Name = nameof(Texts.NoteLyric), Description = nameof(Texts.NoteLyricDescription), ResourceType = typeof(Texts))]
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
        set => Set(ref tone, Math.Clamp(value, 0, 127));
    }

    [Display(GroupName = nameof(Texts.NoteGroupBasic), Name = nameof(Texts.NoteLength), ResourceType = typeof(Texts))]
    [TextBoxSlider("F0", "ms", 50.0, 2000.0, Delay = -1)]
    [Range(1.0, 60000.0)]
    [DefaultValue(DefaultLengthMilliseconds)]
    public double LengthMilliseconds
    {
        get => lengthMilliseconds;
        set => Set(ref lengthMilliseconds, Math.Clamp(value, 1.0, 60000.0));
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
    public VibratoSettings Vibrato
    {
        get => vibrato;
        set => Set(ref vibrato, value ?? new VibratoSettings());
    }

    [Browsable(false)]
    public ObservableCollection<PitchPoint> PitchPoints
    {
        get => pitchPoints;
        set => Set(ref pitchPoints, value ?? []);
    }

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
            LengthMilliseconds = LengthMilliseconds,
            Velocity = Velocity,
            Intensity = Intensity,
            Modulation = Modulation,
            StartPointMilliseconds = StartPointMilliseconds,
            PreutteranceOverride = PreutteranceOverride,
            OverlapOverride = OverlapOverride,
            FadeInMilliseconds = FadeInMilliseconds,
            FadeOutMilliseconds = FadeOutMilliseconds,
            Vibrato = Vibrato.Clone(),
        };
        foreach (var point in PitchPoints)
            clone.PitchPoints.Add(point.Clone());
        return clone;
    }

    public double EvaluatePitchOffsetCents(double millisecondsFromNoteStart)
        => EvaluatePortamentoCents(millisecondsFromNoteStart) + Vibrato.Evaluate(millisecondsFromNoteStart, LengthMilliseconds);

    public double EvaluatePortamentoCents(double millisecondsFromNoteStart)
    {
        if (PitchPoints.Count == 0)
            return 0.0;
        if (PitchPoints.Count == 1)
            return PitchPoints[0].Cents;

        if (millisecondsFromNoteStart < PitchPoints[0].Milliseconds)
            return PitchPoints[0].Cents;

        for (var i = 0; i < PitchPoints.Count - 1; i++)
        {
            var current = PitchPoints[i];
            var next = PitchPoints[i + 1];
            if (millisecondsFromNoteStart > next.Milliseconds)
                continue;

            var span = next.Milliseconds - current.Milliseconds;
            var progress = span <= 0.0 ? 1.0 : (millisecondsFromNoteStart - current.Milliseconds) / span;
            return PitchPoint.Interpolate(current.Cents, next.Cents, progress, current.Shape);
        }

        return PitchPoints[^1].Cents;
    }
}
