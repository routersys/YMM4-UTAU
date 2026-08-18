using UTAU.Models;
using UTAU.Notes;
using YukkuriMovieMaker.Commons;

namespace UTAU.ViewModels;

internal sealed class NoteViewModel(UTAUNote note, NoteEditorViewModel owner) : Bindable
{
    bool isSelected;

    public UTAUNote Note => note;

    public double StartMilliseconds { get; set => Set(ref field, value); }

    public bool IsSelected
    {
        get => isSelected;
        set => Set(ref isSelected, value);
    }

    public bool IsRest => note.IsRest;

    public string Lyric
    {
        get => note.Lyric;
        set
        {
            note.Lyric = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRest));
            OnPropertyChanged(nameof(Display));
        }
    }

    public int Tone
    {
        get => note.Tone;
        set
        {
            note.Tone = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ToneName));
            OnPropertyChanged(nameof(Display));
            owner.InvalidateLayout();
        }
    }

    public double LengthMilliseconds
    {
        get => note.LengthMilliseconds;
        set
        {
            note.LengthMilliseconds = value;
            OnPropertyChanged();
            owner.InvalidateLayout();
        }
    }

    public double Velocity
    {
        get => note.Velocity;
        set
        {
            note.Velocity = value;
            OnPropertyChanged();
        }
    }

    public double Intensity
    {
        get => note.Intensity;
        set
        {
            note.Intensity = value;
            OnPropertyChanged();
        }
    }

    public double Modulation
    {
        get => note.Modulation;
        set
        {
            note.Modulation = value;
            OnPropertyChanged();
        }
    }

    public double StartPointMilliseconds
    {
        get => note.StartPointMilliseconds;
        set
        {
            note.StartPointMilliseconds = value;
            OnPropertyChanged();
        }
    }

    public double PreutteranceOverride
    {
        get => note.PreutteranceOverride ?? 0.0;
        set
        {
            note.PreutteranceOverride = value <= 0.0 ? null : value;
            OnPropertyChanged();
        }
    }

    public double OverlapOverride
    {
        get => note.OverlapOverride ?? 0.0;
        set
        {
            note.OverlapOverride = value <= 0.0 ? null : value;
            OnPropertyChanged();
        }
    }

    public double FadeInMilliseconds
    {
        get => note.FadeInMilliseconds;
        set
        {
            note.FadeInMilliseconds = value;
            OnPropertyChanged();
        }
    }

    public double FadeOutMilliseconds
    {
        get => note.FadeOutMilliseconds;
        set
        {
            note.FadeOutMilliseconds = value;
            OnPropertyChanged();
        }
    }

    public VibratoSettings Vibrato => note.Vibrato;

    public string ToneName => new MusicalTone(note.Tone).Name;

    public string Display => IsRest ? Texts.RestLabel : note.Lyric;

    public double Left => StartMilliseconds * owner.PixelsPerMillisecond;

    public double Width => Math.Max(note.LengthMilliseconds * owner.PixelsPerMillisecond, 1.0);

    public double Top => (owner.MaximumTone - note.Tone) * owner.SemitoneHeight;

    public double Height => owner.SemitoneHeight;

    public void RaiseLayoutChanged()
    {
        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Top));
        OnPropertyChanged(nameof(Height));
    }

    public void RaiseVibratoChanged() => OnPropertyChanged(nameof(Vibrato));
}
