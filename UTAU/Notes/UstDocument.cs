namespace UTAU.Notes;

internal enum UstSectionKind
{
    Unknown,
    Version,
    Setting,
    Note,
    Deleted,
    TrackEnd,
}

internal sealed class UstSection
{
    readonly Dictionary<string, string> entries;
    readonly List<string> bareLines;

    public UstSection(string header, Dictionary<string, string> entries, List<string> bareLines)
    {
        Header = header;
        this.entries = entries;
        this.bareLines = bareLines;
        Kind = Classify(header);
    }

    public string Header { get; }

    public UstSectionKind Kind { get; }

    public IReadOnlyList<string> BareLines => bareLines;

    public IReadOnlyDictionary<string, string> Entries => entries;

    public string? Find(string key) => entries.TryGetValue(key, out var value) ? value : null;

    static UstSectionKind Classify(string header)
    {
        if (header.Equals(UstKeys.VersionHeader, StringComparison.OrdinalIgnoreCase))
            return UstSectionKind.Version;
        if (header.Equals(UstKeys.SettingHeader, StringComparison.OrdinalIgnoreCase))
            return UstSectionKind.Setting;
        if (header.Equals(UstKeys.TrackEndHeader, StringComparison.OrdinalIgnoreCase))
            return UstSectionKind.TrackEnd;
        if (header.Equals(UstKeys.DeleteHeader, StringComparison.OrdinalIgnoreCase))
            return UstSectionKind.Deleted;
        if (header.Equals(UstKeys.PreviousHeader, StringComparison.OrdinalIgnoreCase)
            || header.Equals(UstKeys.NextHeader, StringComparison.OrdinalIgnoreCase)
            || header.Equals(UstKeys.InsertHeader, StringComparison.OrdinalIgnoreCase))
            return UstSectionKind.Note;

        var inner = header.AsSpan(1, header.Length - 2);
        if (inner.Length < 2 || inner[0] != '#')
            return UstSectionKind.Unknown;

        var digits = inner[1..];
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c))
                return UstSectionKind.Unknown;
        }
        return UstSectionKind.Note;
    }
}

internal sealed class UstDocument
{
    readonly List<UstSection> sections;

    public UstDocument(List<UstSection> sections) => this.sections = sections;

    public IReadOnlyList<UstSection> Sections => sections;

    public UstSection? Setting => sections.FirstOrDefault(x => x.Kind == UstSectionKind.Setting);

    public IEnumerable<UstSection> NoteSections => sections.Where(x => x.Kind == UstSectionKind.Note);

    public string? Version
    {
        get
        {
            foreach (var section in sections)
            {
                if (section.Kind != UstSectionKind.Version)
                    continue;
                if (section.BareLines.Count > 0)
                    return section.BareLines[0];
            }
            return Setting?.Find(UstKeys.UstVersion);
        }
    }
}
