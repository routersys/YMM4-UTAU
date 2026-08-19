using UTAU.Models;
using YukkuriMovieMaker.Commons;

namespace UTAU.ViewModels;

internal sealed record VoiceColorItem(string Name, string Value);

internal sealed record ToneItem(string Name, int Value, bool IsAccidental);

internal static class ComboBoxItems
{
    public static IReadOnlyList<VoiceColorItem> CreateVoiceColors(IEnumerable<string> colors)
    {
        var items = new List<VoiceColorItem> { new(Texts.DefaultColor, string.Empty) };
        items.AddRange(colors.Where(x => x.Length > 0).Select(x => new VoiceColorItem(x, x)));
        return items;
    }

    public static IReadOnlyList<ToneItem> CreateTones(int minimum, int maximum)
    {
        var items = new List<ToneItem>(maximum - minimum + 1);
        for (var noteNumber = minimum; noteNumber <= maximum; noteNumber++)
            items.Add(new ToneItem(new MusicalTone(noteNumber).Name, noteNumber, new MusicalTone(noteNumber).Name.Contains('#')));
        return items;
    }
}

internal sealed class KeyRowViewModel : Bindable
{
    string name = string.Empty;
    bool isAccidental;
    int noteNumber;
    double height;
    double rollWidth;

    public string Name
    {
        get => name;
        set
        {
            if (name == value)
                return;
            name = value;
            OnPropertyChanged();
        }
    }

    public bool IsAccidental
    {
        get => isAccidental;
        set
        {
            if (isAccidental == value)
                return;
            isAccidental = value;
            OnPropertyChanged();
        }
    }

    public int NoteNumber
    {
        get => noteNumber;
        set
        {
            if (noteNumber == value)
                return;
            noteNumber = value;
            OnPropertyChanged();
        }
    }

    public double Height
    {
        get => height;
        set
        {
            if (height == value)
                return;
            height = value;
            OnPropertyChanged();
        }
    }

    public double RollWidth
    {
        get => rollWidth;
        set
        {
            if (rollWidth == value)
                return;
            rollWidth = value;
            OnPropertyChanged();
        }
    }
}

internal sealed class GridLineViewModel : Bindable
{
    double left;
    double height;
    bool isBar;

    public double Left
    {
        get => left;
        set
        {
            if (left == value)
                return;
            left = value;
            OnPropertyChanged();
        }
    }

    public double Height
    {
        get => height;
        set
        {
            if (height == value)
                return;
            height = value;
            OnPropertyChanged();
        }
    }

    public bool IsBar
    {
        get => isBar;
        set
        {
            if (isBar == value)
                return;
            isBar = value;
            OnPropertyChanged();
        }
    }
}
