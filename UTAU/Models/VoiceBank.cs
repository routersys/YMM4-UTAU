using System.IO;

namespace UTAU.Models;

internal sealed class VoiceBank
{
    readonly Dictionary<string, OtoEntry> aliases;

    public VoiceBank(
        string id,
        string rootDirectory,
        CharacterProfile character,
        CharacterYaml characterYaml,
        PrefixMap? prefixMap,
        IReadOnlyList<SubBank> subBanks,
        IReadOnlyList<OtoSet> otoSets,
        string? readme,
        string? portraitPath)
    {
        Id = id;
        RootDirectory = rootDirectory;
        Character = character;
        CharacterYaml = characterYaml;
        PrefixMap = prefixMap;
        SubBanks = subBanks;
        OtoSets = otoSets;
        Readme = readme;
        PortraitPath = portraitPath;

        aliases = new Dictionary<string, OtoEntry>(StringComparer.Ordinal);
        foreach (var entry in otoSets.SelectMany(x => x.Entries))
            aliases.TryAdd(entry.Alias, entry);

        Colors = subBanks
            .Select(x => x.Color)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    public string Id { get; }

    public string RootDirectory { get; }

    public CharacterProfile Character { get; }

    public CharacterYaml CharacterYaml { get; }

    public PrefixMap? PrefixMap { get; }

    public IReadOnlyList<SubBank> SubBanks { get; }

    public IReadOnlyList<OtoSet> OtoSets { get; }

    public string? Readme { get; }

    public string? PortraitPath { get; }

    public IReadOnlyList<string> Colors { get; }

    public string Name => Character.Name is { Length: > 0 } name ? name : Path.GetFileName(RootDirectory.TrimEnd(Path.DirectorySeparatorChar));

    public string? ImagePath => Character.ImagePath;

    public int AliasCount => aliases.Count;

    public IEnumerable<OtoEntry> Entries => aliases.Values;

    public bool Contains(string alias) => aliases.ContainsKey(alias);

    public OtoEntry? Find(string alias) => aliases.GetValueOrDefault(alias);

    public OtoEntry? Resolve(string lyric, int noteNumber, string? color)
    {
        foreach (var alias in EnumerateAliasCandidates(lyric, noteNumber, color))
            if (aliases.TryGetValue(alias, out var entry))
                return entry;
        return null;
    }

    public IEnumerable<string> EnumerateAliasCandidates(string lyric, int noteNumber, string? color)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (prefix, suffix) in EnumerateAffixes(noteNumber, color))
        {
            var candidate = prefix + lyric + suffix;
            if (seen.Add(candidate))
                yield return candidate;
        }
    }

    IEnumerable<(string Prefix, string Suffix)> EnumerateAffixes(int noteNumber, string? color)
    {
        foreach (var subBank in SubBanks)
        {
            if (!MatchesColor(subBank, color) || !subBank.Covers(noteNumber))
                continue;
            yield return (subBank.Prefix, subBank.Suffix);
        }

        if (PrefixMap is not null)
        {
            var mapped = PrefixMap.Resolve(noteNumber);
            if (mapped.Prefix.Length > 0 || mapped.Suffix.Length > 0)
                yield return mapped;
        }

        yield return (string.Empty, string.Empty);
    }

    static bool MatchesColor(SubBank subBank, string? color)
        => string.Equals(subBank.Color, color ?? string.Empty, StringComparison.Ordinal);
}

internal sealed record OtoSet(string DirectoryPath, IReadOnlyList<OtoEntry> Entries)
{
    public string Name => Path.GetFileName(DirectoryPath.TrimEnd(Path.DirectorySeparatorChar));
}
