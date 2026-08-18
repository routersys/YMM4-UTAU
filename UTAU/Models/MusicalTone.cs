using System.Globalization;

namespace UTAU.Models;

internal readonly record struct MusicalTone(int NoteNumber)
{
    const int SemitonesPerOctave = 12;
    const int ReferenceNoteNumber = 69;
    const double ReferenceFrequency = 440.0;

    static readonly string[] SharpNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static MusicalTone MiddleC => new(60);

    public double Frequency => ReferenceFrequency * Math.Pow(2.0, (NoteNumber - ReferenceNoteNumber) / (double)SemitonesPerOctave);

    public int Octave => (int)Math.Floor(NoteNumber / (double)SemitonesPerOctave) - 1;

    public int PitchClass => ((NoteNumber % SemitonesPerOctave) + SemitonesPerOctave) % SemitonesPerOctave;

    public string Name => SharpNames[PitchClass] + Octave.ToString(CultureInfo.InvariantCulture);

    public override string ToString() => Name;

    public static double FrequencyOf(double noteNumber)
        => ReferenceFrequency * Math.Pow(2.0, (noteNumber - ReferenceNoteNumber) / SemitonesPerOctave);

    public static bool TryParse(ReadOnlySpan<char> text, out MusicalTone tone)
    {
        tone = default;
        text = text.Trim();
        if (text.IsEmpty)
            return false;

        var pitchClass = char.ToUpperInvariant(text[0]) switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1,
        };
        if (pitchClass < 0)
            return false;

        var index = 1;
        while (index < text.Length && (text[index] == '#' || text[index] == '♯' || text[index] == 'b' || text[index] == '♭'))
        {
            pitchClass += text[index] == '#' || text[index] == '♯' ? 1 : -1;
            index++;
        }

        if (index >= text.Length)
            return false;

        var octaveText = text[index..];
        if (!int.TryParse(octaveText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var octave))
            return false;

        var noteNumber = (octave + 1) * SemitonesPerOctave + pitchClass;
        if (noteNumber is < 0 or > 127)
            return false;

        tone = new MusicalTone(noteNumber);
        return true;
    }
}
