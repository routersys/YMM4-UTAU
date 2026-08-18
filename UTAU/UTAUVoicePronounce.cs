using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using UTAU.Notes;
using UTAU.Views;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.UndoRedo;

namespace UTAU;

internal sealed class UTAUVoicePronounce : UndoRedoable, IVoicePronounce
{
    string sourceText = string.Empty;
    double tempo = TimeBase.DefaultTempo;
    double speed = 1.0;
    LipSyncFrame[]? lipSyncFrames;

    public UTAUVoicePronounce()
    {
        SubscribeObservableCollectionChangedAndChild(Notes);
        SubscribeChildUndoRedoable(FormantCurve);
        SubscribeChildUndoRedoable(BreathinessCurve);
    }

    [Display(Name = nameof(Texts.PronounceNotes), Description = nameof(Texts.PronounceNotesDescription), ResourceType = typeof(Texts))]
    [NoteEditor]
    public ObservableCollection<UTAUNote> Notes { get; } = [];

    [Browsable(false)]
    public ExpressionCurve FormantCurve { get; } = new();

    [Browsable(false)]
    public ExpressionCurve BreathinessCurve { get; } = new();

    [Browsable(false)]
    public double Tempo
    {
        get => tempo;
        set => Set(ref tempo, Math.Clamp(value, TimeBase.MinimumTempo, TimeBase.MaximumTempo));
    }

    [Browsable(false)]
    public double Speed
    {
        get => speed;
        set => Set(ref speed, Math.Clamp(value, TimeBase.MinimumSpeed, TimeBase.MaximumSpeed));
    }

    [Browsable(false)]
    public TimeBase TimeBase => new(Tempo, Speed);

    [Browsable(false)]
    public string SourceText
    {
        get => sourceText;
        set => Set(ref sourceText, value ?? string.Empty);
    }

    [Browsable(false)]
    public LipSyncFrame[]? LipSyncFrames
    {
        get => lipSyncFrames;
        set => Set(ref lipSyncFrames, value);
    }

    public void BeginEdit()
    {
    }

    public ValueTask EndEditAsync() => ValueTask.CompletedTask;

    public static UTAUVoicePronounce FromText(string normalizedText, UTAUVoiceParameter parameter)
    {
        var options = NoteBuildOptions.Create(parameter.BaseTone);
        var pronounce = new UTAUVoicePronounce
        {
            SourceText = normalizedText,
            Tempo = parameter.Tempo,
            Speed = parameter.Speed,
        };
        foreach (var note in NoteSequenceBuilder.Build(normalizedText, options))
        {
            note.Modulation = parameter.Modulation;
            pronounce.Notes.Add(note);
        }
        return pronounce;
    }
}
