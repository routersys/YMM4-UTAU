using System.Buffers;
using UTAU.Models;

namespace UTAU.Phonemes;

internal static class AliasResolver
{
    public static OtoEntry? Resolve(VoiceBank bank, string lyric, ReadOnlySpan<char> previousVowel, int tone, string? color, bool ignorePrefixMap, out string alias)
    {
        var rented = ArrayPool<char>.Shared.Rent(AliasWorkspace.RequiredLength(lyric.Length));
        try
        {
            var work = new AliasWorkspace(rented, lyric.Length);
            var candidates = new AliasCandidates(
                AliasNormalizer.Normalize(lyric, work.Source),
                previousVowel,
                work.Pool,
                stackalloc int[AliasCandidates.BoundsLength],
                work.Scratch);

            while (candidates.MoveNext(work.Candidate, out var current))
            {
                var entry = bank.Resolve(current, tone, color, ignorePrefixMap);
                if (entry is null)
                    continue;

                alias = current.ToString();
                return entry;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }

        alias = AliasNormalizer.Normalize(lyric);
        return null;
    }

    public static IEnumerable<string> EnumerateCandidates(string lyric, string previousVowel)
    {
        var found = new List<string>(AliasCandidates.FormCount * 3);
        Collect(lyric, previousVowel, found);
        return found;
    }

    static void Collect(string lyric, ReadOnlySpan<char> previousVowel, List<string> found)
    {
        var rented = ArrayPool<char>.Shared.Rent(AliasWorkspace.RequiredLength(lyric.Length));
        try
        {
            var work = new AliasWorkspace(rented, lyric.Length);
            var candidates = new AliasCandidates(
                AliasNormalizer.Normalize(lyric, work.Source),
                previousVowel,
                work.Pool,
                stackalloc int[AliasCandidates.BoundsLength],
                work.Scratch);

            while (candidates.MoveNext(work.Candidate, out var current))
            {
                var text = current.ToString();
                if (!found.Contains(text, StringComparer.Ordinal))
                    found.Add(text);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    public static OtoEntry? ResolveTransition(VoiceBank bank, ReadOnlySpan<char> vowel, ReadOnlySpan<char> consonant, int tone, string? color, bool ignorePrefixMap, out string alias)
    {
        Span<char> buffer = stackalloc char[KanaRomanization.StackTextLength];
        var length = vowel.Length + 1 + consonant.Length;
        if (length > buffer.Length)
        {
            alias = string.Empty;
            return null;
        }

        vowel.CopyTo(buffer);
        buffer[vowel.Length] = KanaRomanization.AliasSeparator;
        consonant.CopyTo(buffer[(vowel.Length + 1)..]);

        var key = buffer[..length];
        var entry = bank.Resolve(key, tone, color, ignorePrefixMap);
        alias = entry is null ? string.Empty : key.ToString();
        return entry;
    }
}
