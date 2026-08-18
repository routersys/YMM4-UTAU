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
    ObservableCollection<UTAUNote> notes = [];
    LipSyncFrame[]? lipSyncFrames;

    [Display(Name = nameof(Texts.PronounceNotes), Description = nameof(Texts.PronounceNotesDescription), ResourceType = typeof(Texts))]
    [NoteEditor]
    public ObservableCollection<UTAUNote> Notes
    {
        get => notes;
        set => Set(ref notes, value ?? []);
    }

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
        var pronounce = new UTAUVoicePronounce { SourceText = normalizedText };
        foreach (var note in NoteSequenceBuilder.Build(normalizedText, options))
        {
            note.Modulation = parameter.Modulation;
            pronounce.Notes.Add(note);
        }
        return pronounce;
    }
}
