using System.IO;
using System.Text;

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
            aliases.TryAdd(NormalizeKey(entry.Alias), entry);

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

    public bool Contains(string alias) => aliases.ContainsKey(NormalizeKey(alias));

    public OtoEntry? Find(string alias) => aliases.GetValueOrDefault(NormalizeKey(alias));

    public OtoEntry? Resolve(string lyric, int noteNumber, string? color, bool ignorePrefixMap = false)
    {
        foreach (var alias in EnumerateAliasCandidates(lyric, noteNumber, color, ignorePrefixMap))
            if (aliases.TryGetValue(NormalizeKey(alias), out var entry))
                return entry;
        return null;
    }

    public static string NormalizeKey(string alias)
    {
        if (!NeedsNormalization(alias))
            return alias;

        var source = SafeCompose(alias);
        var builder = new StringBuilder(source.Length);
        var pendingSpace = false;

        foreach (var c in source)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(c);
        }

        return builder.ToString();
    }

    static bool NeedsNormalization(string alias)
    {
        var previousWasSpace = true;
        var composed = true;

        foreach (var c in alias)
        {
            if (char.IsWhiteSpace(c))
            {
                if (previousWasSpace || c != ' ')
                    return true;
                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            if (!char.IsAscii(c))
                composed = false;
        }

        if (previousWasSpace && alias.Length > 0)
            return true;

        return !composed && !SafeIsComposed(alias);
    }

    static bool SafeIsComposed(string alias)
    {
        try
        {
            return alias.IsNormalized(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    static string SafeCompose(string alias)
    {
        try
        {
            return alias.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return alias;
        }
    }

    public IEnumerable<string> EnumerateAliasCandidates(string lyric, int noteNumber, string? color, bool ignorePrefixMap = false)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (prefix, suffix) in EnumerateAffixes(noteNumber, color, ignorePrefixMap))
        {
            var candidate = prefix + lyric + suffix;
            if (seen.Add(candidate))
                yield return candidate;
        }
    }

    IEnumerable<(string Prefix, string Suffix)> EnumerateAffixes(int noteNumber, string? color, bool ignorePrefixMap)
    {
        foreach (var subBank in SubBanks)
        {
            if (!MatchesColor(subBank, color) || !subBank.Covers(noteNumber))
                continue;
            yield return (subBank.Prefix, subBank.Suffix);
        }

        if (PrefixMap is not null && !ignorePrefixMap)
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
