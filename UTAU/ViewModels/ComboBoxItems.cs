using UTAU.Models;
using UTAU.Notes;

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

internal sealed record KeyRowViewModel(string Name, bool IsAccidental, int NoteNumber, double Height, double RollWidth);

internal sealed record GridLineViewModel(double Left, double Height, bool IsBar);

internal sealed record PitchShapeItem(string Name, PitchPointShape Value);
