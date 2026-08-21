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
    string importMessage = string.Empty;
    string renderMessage = string.Empty;
    double tempo = TimeBase.DefaultTempo;
    double speed = 1.0;
    LipSyncFrame[]? lipSyncFrames;
    UstPhraseRange importedRange;

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
        set => SetWithoutUndoRedo(ref sourceText, value ?? string.Empty);
    }

    [Browsable(false)]
    public string ImportMessage
    {
        get => importMessage;
        set => SetWithoutUndoRedo(ref importMessage, value ?? string.Empty);
    }

    [Browsable(false)]
    public string RenderMessage
    {
        get => renderMessage;
        set => SetWithoutUndoRedo(ref renderMessage, value ?? string.Empty);
    }

    [Browsable(false)]
    public UstPhraseRange ImportedRange
    {
        get => importedRange;
        set => SetWithoutUndoRedo(ref importedRange, value);
    }

    [Browsable(false)]
    public LipSyncFrame[]? LipSyncFrames
    {
        get => lipSyncFrames;
        set => SetWithoutUndoRedo(ref lipSyncFrames, value);
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

    public static UTAUVoicePronounce FromUst(string path, UTAUVoiceParameter parameter)
    {
        if (UstParser.ParseFile(path) is not { } document)
            throw new InvalidOperationException(Texts.UstImportFailed);

        var imported = UstImporter.Import(document, parameter.UstRange);
        if (imported.Notes.Count == 0)
            throw new InvalidOperationException(Texts.UstImportEmpty);

        var pronounce = new UTAUVoicePronounce
        {
            SourceText = path,
            Tempo = imported.Tempo,
            Speed = parameter.Speed,
            ImportedRange = parameter.UstRange,
            ImportMessage = BuildImportMessage(imported, parameter.UstRange),
        };
        foreach (var note in imported.Notes)
            pronounce.Notes.Add(note);
        return pronounce;
    }

    static string BuildImportMessage(UstImportResult imported, UstPhraseRange range)
    {
        var parts = new List<string> { string.Format(Texts.UstImportedFormat, imported.Notes.Count) };

        if (range.CoversEverything)
            parts.Add(string.Format(Texts.UstPhraseTotalFormat, imported.TotalPhrases));
        else
        {
            var taken = range.Count > 0 ? range.Count : imported.TotalPhrases - range.Start + 1;
            parts.Add(string.Format(Texts.UstPhraseRangeFormat, range.Start, Math.Max(taken, 0), imported.TotalPhrases));
        }

        if (!range.CoversEverything && imported.StartTicks > 0)
            parts.Add(string.Format(Texts.UstPhraseOffsetFormat, imported.StartTicks));
        if (imported.TrimmedRestTicks > 0)
            parts.Add(string.Format(Texts.UstRestTrimmedFormat, imported.TrimmedRestTicks));
        if (imported.LegacyPitchNoteCount > 0)
            parts.Add(Texts.UstLegacyPitchIgnored);
        return string.Join("  ", parts);
    }
}
