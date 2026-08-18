namespace UTAU.Models;

internal sealed class PrefixMap
{
    readonly Dictionary<int, (string Prefix, string Suffix)> entries;

    PrefixMap(Dictionary<int, (string Prefix, string Suffix)> entries) => this.entries = entries;

    public int Count => entries.Count;

    public static PrefixMap Parse(string content)
    {
        var entries = new Dictionary<int, (string, string)>();
        foreach (var line in VoiceBankTextReader.ReadLines(content))
        {
            if (line.Trim().Length == 0)
                continue;

            var fields = line.Split('\t');
            if (fields.Length < 2)
                fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || !MusicalTone.TryParse(fields[0], out var tone))
                continue;

            var prefix = fields.Length > 1 ? fields[1].Trim() : string.Empty;
            var suffix = fields.Length > 2 ? fields[2].Trim() : string.Empty;
            entries[tone.NoteNumber] = (prefix, suffix);
        }
        return new PrefixMap(entries);
    }

    public (string Prefix, string Suffix) Resolve(int noteNumber)
        => entries.TryGetValue(noteNumber, out var value) ? value : (string.Empty, string.Empty);

    public IEnumerable<(MusicalTone Tone, string Prefix, string Suffix)> Enumerate()
        => entries
            .OrderBy(x => x.Key)
            .Select(x => (new MusicalTone(x.Key), x.Value.Prefix, x.Value.Suffix));
}
