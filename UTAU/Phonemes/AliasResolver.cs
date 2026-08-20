using UTAU.Models;

namespace UTAU.Phonemes;

internal static class AliasResolver
{
    public static OtoEntry? Resolve(VoiceBank bank, string lyric, string previousVowel, int tone, string? color, out string alias)
    {
        foreach (var candidate in EnumerateCandidates(lyric, previousVowel))
        {
            var entry = bank.Resolve(candidate, tone, color);
            if (entry is null)
                continue;
            alias = candidate;
            return entry;
        }

        alias = lyric;
        return null;
    }

    public static IEnumerable<string> EnumerateCandidates(string lyric, string previousVowel)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in Generate(lyric, previousVowel))
            if (candidate.Length > 0 && seen.Add(candidate))
                yield return candidate;
    }

    static IEnumerable<string> Generate(string lyric, string previousVowel)
    {
        var hiragana = KanaRomanization.ToHiragana(lyric);
        var katakana = KanaRomanization.ToKatakana(lyric);
        var mora = KanaRomanization.ToMora(lyric);
        var moraKatakana = KanaRomanization.ToKatakana(mora);
        KanaRomanization.TryGetRomaji(lyric, out var romaji);

        if (previousVowel.Length > 0)
        {
            yield return previousVowel + " " + lyric;
            yield return previousVowel + " " + hiragana;
            yield return previousVowel + " " + katakana;
            yield return previousVowel + " " + mora;
            yield return previousVowel + " " + moraKatakana;
            if (romaji is not null)
                yield return previousVowel + " " + romaji;
        }

        yield return lyric;
        yield return hiragana;
        yield return katakana;
        yield return mora;
        yield return moraKatakana;
        if (romaji is not null)
            yield return romaji;
    }

    public static OtoEntry? ResolveTransition(VoiceBank bank, string vowel, string consonant, int tone, string? color, out string alias)
    {
        alias = vowel + " " + consonant;
        return bank.Resolve(alias, tone, color);
    }
}
