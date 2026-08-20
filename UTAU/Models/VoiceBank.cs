using System.Buffers;
using System.IO;

namespace UTAU.Models;

internal sealed class VoiceBank
{
    const int StackAliasLength = 128;

    readonly Dictionary<string, OtoEntry> aliases;
    readonly Dictionary<string, OtoEntry>.AlternateLookup<ReadOnlySpan<char>> lookup;

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
            aliases.TryAdd(AliasNormalizer.Normalize(entry.Alias), entry);
        lookup = aliases.GetAlternateLookup<ReadOnlySpan<char>>();

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

    public bool Contains(string alias) => aliases.ContainsKey(AliasNormalizer.Normalize(alias));

    public OtoEntry? Find(string alias) => aliases.GetValueOrDefault(AliasNormalizer.Normalize(alias));

    public OtoEntry? Resolve(string lyric, int noteNumber, string? color, bool ignorePrefixMap = false)
        => Resolve(lyric.AsSpan(), noteNumber, color, ignorePrefixMap);

    public OtoEntry? Resolve(ReadOnlySpan<char> lyric, int noteNumber, string? color, bool ignorePrefixMap = false)
    {
        foreach (var subBank in SubBanks)
        {
            if (!MatchesColor(subBank, color) || !subBank.Covers(noteNumber))
                continue;
            if (FindAffixed(subBank.Prefix, lyric, subBank.Suffix) is { } coloured)
                return coloured;
        }

        if (PrefixMap is not null && !ignorePrefixMap)
        {
            var mapped = PrefixMap.Resolve(noteNumber);
            if ((mapped.Prefix.Length > 0 || mapped.Suffix.Length > 0)
                && FindAffixed(mapped.Prefix, lyric, mapped.Suffix) is { } tuned)
                return tuned;
        }

        return FindAffixed(string.Empty, lyric, string.Empty);
    }

    OtoEntry? FindAffixed(string prefix, ReadOnlySpan<char> lyric, string suffix)
    {
        var length = prefix.Length + lyric.Length + suffix.Length;
        char[]? rentedCandidate = null;
        char[]? rentedKey = null;

        try
        {
            var candidate = length <= StackAliasLength
                ? stackalloc char[StackAliasLength]
                : (rentedCandidate = ArrayPool<char>.Shared.Rent(length));

            prefix.CopyTo(candidate);
            lyric.CopyTo(candidate[prefix.Length..]);
            suffix.CopyTo(candidate[(prefix.Length + lyric.Length)..]);

            var key = length <= StackAliasLength
                ? stackalloc char[StackAliasLength]
                : (rentedKey = ArrayPool<char>.Shared.Rent(length));

            return lookup.TryGetValue(AliasNormalizer.Normalize(candidate[..length], key), out var entry) ? entry : null;
        }
        finally
        {
            if (rentedCandidate is not null)
                ArrayPool<char>.Shared.Return(rentedCandidate);
            if (rentedKey is not null)
                ArrayPool<char>.Shared.Return(rentedKey);
        }
    }

    static bool MatchesColor(SubBank subBank, string? color)
        => string.Equals(subBank.Color, color ?? string.Empty, StringComparison.Ordinal);
}

internal sealed record OtoSet(string DirectoryPath, IReadOnlyList<OtoEntry> Entries)
{
    public string Name => Path.GetFileName(DirectoryPath.TrimEnd(Path.DirectorySeparatorChar));
}
