using System.Windows.Input;
using UTAU.Notes;
using YukkuriMovieMaker.Commons;

namespace UTAU.ViewModels;

internal sealed class LyricsBulkEditViewModel : Bindable
{
    readonly string original;
    string text;

    public LyricsBulkEditViewModel(string lyrics, int noteCount)
    {
        original = lyrics ?? string.Empty;
        text = original;
        NoteCount = noteCount;
        ResetCommand = new ActionCommand(_ => Text != original, _ => Text = original);
    }

    public string Text
    {
        get => text;
        set => Set(ref text, value ?? string.Empty, nameof(Text), nameof(LyricCount), nameof(CountText), nameof(HasMismatch));
    }

    public int NoteCount { get; }

    public int LyricCount => LyricSplitter.Split(text).Count;

    public string CountText => string.Format(Texts.LyricCountFormat, LyricCount, NoteCount);

    public bool HasMismatch => LyricCount != NoteCount;

    public ICommand ResetCommand { get; }

    public IReadOnlyList<string> Lyrics => LyricSplitter.Split(text);
}
