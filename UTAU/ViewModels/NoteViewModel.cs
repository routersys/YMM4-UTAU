using System.ComponentModel;
using UTAU.Models;
using UTAU.Notes;
using YukkuriMovieMaker.Commons;

namespace UTAU.ViewModels;

internal sealed class NoteViewModel : Bindable, IDisposable
{
    readonly NoteEditorViewModel owner;
    bool isSelected;
    int startTicks;

    public NoteViewModel(UTAUNote note, NoteEditorViewModel owner)
    {
        Note = note;
        this.owner = owner;
        note.PropertyChanged += OnNotePropertyChanged;
    }

    public UTAUNote Note { get; }

    public int StartTicks
    {
        get => startTicks;
        set => Set(ref startTicks, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => Set(ref isSelected, value);
    }

    public bool IsRest => Note.IsRest;

    public int Tone
    {
        get => Note.Tone;
        set => Note.Tone = value;
    }

    public int LengthTicks
    {
        get => Note.LengthTicks;
        set => Note.LengthTicks = value;
    }

    public int EndTicks => StartTicks + Note.LengthTicks;

    public string ToneName => new MusicalTone(Note.Tone).Name;

    public string Display => IsRest ? Texts.RestLabel : Note.Lyric;

    public double Left => StartTicks * owner.PixelsPerTick;

    public double Width => Math.Max(Note.LengthTicks * owner.PixelsPerTick - 1.0, 1.0);

    public double Top => (owner.MaximumTone - Note.Tone) * owner.SemitoneHeight;

    public double Height => Math.Max(owner.SemitoneHeight - 1.0, 1.0);

    public void RaiseLayoutChanged()
    {
        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Top));
        OnPropertyChanged(nameof(Height));
    }

    public void Dispose() => Note.PropertyChanged -= OnNotePropertyChanged;

    void OnNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(UTAUNote.Lyric):
                OnPropertyChanged(nameof(Display));
                OnPropertyChanged(nameof(IsRest));
                break;
            case nameof(UTAUNote.Tone):
                OnPropertyChanged(nameof(Tone));
                OnPropertyChanged(nameof(ToneName));
                break;
            case nameof(UTAUNote.LengthTicks):
                OnPropertyChanged(nameof(LengthTicks));
                break;
        }

        owner.OnNoteChanged(e.PropertyName);
    }
}
