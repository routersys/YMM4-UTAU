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
        set => Set(ref name, value);
    }

    public bool IsAccidental
    {
        get => isAccidental;
        set => Set(ref isAccidental, value);
    }

    public int NoteNumber
    {
        get => noteNumber;
        set => Set(ref noteNumber, value);
    }

    public double Height
    {
        get => height;
        set => Set(ref height, value);
    }

    public double RollWidth
    {
        get => rollWidth;
        set => Set(ref rollWidth, value);
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
        set => Set(ref left, value);
    }

    public double Height
    {
        get => height;
        set => Set(ref height, value);
    }

    public bool IsBar
    {
        get => isBar;
        set => Set(ref isBar, value);
    }
}
