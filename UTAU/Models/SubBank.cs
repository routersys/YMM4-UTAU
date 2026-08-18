namespace UTAU.Models;

internal readonly record struct ToneRange(int Low, int High)
{
    public bool Contains(int noteNumber) => noteNumber >= Low && noteNumber <= High;

    public override string ToString()
        => Low == High ? new MusicalTone(Low).Name : $"{new MusicalTone(Low).Name}-{new MusicalTone(High).Name}";

    public static bool TryParse(ReadOnlySpan<char> text, out ToneRange range)
    {
        range = default;
        text = text.Trim();
        if (text.IsEmpty)
            return false;

        if (MusicalTone.TryParse(text, out var single))
        {
            range = new ToneRange(single.NoteNumber, single.NoteNumber);
            return true;
        }

        for (var i = 1; i < text.Length - 1; i++)
        {
            if (text[i] != '-')
                continue;
            if (!MusicalTone.TryParse(text[..i], out var low) || !MusicalTone.TryParse(text[(i + 1)..], out var high))
                continue;

            range = low.NoteNumber <= high.NoteNumber
                ? new ToneRange(low.NoteNumber, high.NoteNumber)
                : new ToneRange(high.NoteNumber, low.NoteNumber);
            return true;
        }

        return false;
    }
}

internal sealed class SubBank
{
    public string Color { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public string Suffix { get; init; } = string.Empty;

    public IReadOnlyList<ToneRange> ToneRanges { get; init; } = [];

    public bool Covers(int noteNumber)
        => ToneRanges.Count == 0 || ToneRanges.Any(x => x.Contains(noteNumber));
}
