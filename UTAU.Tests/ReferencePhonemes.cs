using UTAU.Models;

namespace UTAU.Tests;

internal static class ReferenceAliasResolver
{
    public static IEnumerable<string> EnumerateCandidates(string lyric, string previousVowel)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in Generate(AliasNormalizer.Normalize(lyric), previousVowel))
            if (candidate.Length > 0 && seen.Add(candidate))
                yield return candidate;
    }

    static IEnumerable<string> Generate(string lyric, string previousVowel)
    {
        var hiragana = ReferenceKana.ToHiragana(lyric);
        var katakana = ReferenceKana.ToKatakana(lyric);
        var mora = ReferenceKana.ToMora(lyric);
        var moraKatakana = ReferenceKana.ToKatakana(mora);
        ReferenceKana.TryGetRomaji(lyric, out var romaji);

        if (previousVowel.Length > 0)
        {
            yield return previousVowel + " " + lyric;
            yield return previousVowel + " " + hiragana;
            yield return previousVowel + " " + katakana;
            yield return previousVowel + " " + mora;
            yield return previousVowel + " " + moraKatakana;
            if (romaji is not null)
                yield return previousVowel + " " + romaji;
            if (previousVowel != ReferenceKana.StartVowel)
            {
                yield return ReferenceKana.AnyVowel + " " + lyric;
                yield return ReferenceKana.AnyVowel + " " + mora;
            }
        }

        yield return lyric;
        yield return hiragana;
        yield return katakana;
        yield return mora;
        yield return moraKatakana;
        if (romaji is not null)
            yield return romaji;
    }

}

internal static class ReferenceKana
{
    public const string StartVowel = "-";
    public const string SilenceConsonant = "-";
    public const string AnyVowel = "*";
    public const char AliasSeparator = ' ';

    static readonly Dictionary<string, string> TableEntries = new(StringComparer.Ordinal)
    {
        ["あ"] = "a", ["い"] = "i", ["う"] = "u", ["え"] = "e", ["お"] = "o",
        ["か"] = "ka", ["き"] = "ki", ["く"] = "ku", ["け"] = "ke", ["こ"] = "ko",
        ["が"] = "ga", ["ぎ"] = "gi", ["ぐ"] = "gu", ["げ"] = "ge", ["ご"] = "go",
        ["さ"] = "sa", ["し"] = "shi", ["す"] = "su", ["せ"] = "se", ["そ"] = "so",
        ["ざ"] = "za", ["じ"] = "ji", ["ず"] = "zu", ["ぜ"] = "ze", ["ぞ"] = "zo",
        ["た"] = "ta", ["ち"] = "chi", ["つ"] = "tsu", ["て"] = "te", ["と"] = "to",
        ["だ"] = "da", ["ぢ"] = "ji", ["づ"] = "zu", ["で"] = "de", ["ど"] = "do",
        ["な"] = "na", ["に"] = "ni", ["ぬ"] = "nu", ["ね"] = "ne", ["の"] = "no",
        ["は"] = "ha", ["ひ"] = "hi", ["ふ"] = "fu", ["へ"] = "he", ["ほ"] = "ho",
        ["ば"] = "ba", ["び"] = "bi", ["ぶ"] = "bu", ["べ"] = "be", ["ぼ"] = "bo",
        ["ぱ"] = "pa", ["ぴ"] = "pi", ["ぷ"] = "pu", ["ぺ"] = "pe", ["ぽ"] = "po",
        ["ま"] = "ma", ["み"] = "mi", ["む"] = "mu", ["め"] = "me", ["も"] = "mo",
        ["や"] = "ya", ["ゆ"] = "yu", ["よ"] = "yo",
        ["ら"] = "ra", ["り"] = "ri", ["る"] = "ru", ["れ"] = "re", ["ろ"] = "ro",
        ["わ"] = "wa", ["を"] = "o", ["ん"] = "n",
        ["ゔ"] = "vu",
        ["きゃ"] = "kya", ["きゅ"] = "kyu", ["きょ"] = "kyo", ["きぇ"] = "kye",
        ["ぎゃ"] = "gya", ["ぎゅ"] = "gyu", ["ぎょ"] = "gyo", ["ぎぇ"] = "gye",
        ["しゃ"] = "sha", ["しゅ"] = "shu", ["しょ"] = "sho", ["しぇ"] = "she",
        ["じゃ"] = "ja", ["じゅ"] = "ju", ["じょ"] = "jo", ["じぇ"] = "je",
        ["ちゃ"] = "cha", ["ちゅ"] = "chu", ["ちょ"] = "cho", ["ちぇ"] = "che",
        ["ぢゃ"] = "ja", ["ぢゅ"] = "ju", ["ぢょ"] = "jo",
        ["にゃ"] = "nya", ["にゅ"] = "nyu", ["にょ"] = "nyo", ["にぇ"] = "nye",
        ["ひゃ"] = "hya", ["ひゅ"] = "hyu", ["ひょ"] = "hyo", ["ひぇ"] = "hye",
        ["びゃ"] = "bya", ["びゅ"] = "byu", ["びょ"] = "byo", ["びぇ"] = "bye",
        ["ぴゃ"] = "pya", ["ぴゅ"] = "pyu", ["ぴょ"] = "pyo", ["ぴぇ"] = "pye",
        ["みゃ"] = "mya", ["みゅ"] = "myu", ["みょ"] = "myo", ["みぇ"] = "mye",
        ["りゃ"] = "rya", ["りゅ"] = "ryu", ["りょ"] = "ryo", ["りぇ"] = "rye",
        ["ふぁ"] = "fa", ["ふぃ"] = "fi", ["ふぇ"] = "fe", ["ふぉ"] = "fo", ["ふゅ"] = "fyu",
        ["ゔぁ"] = "va", ["ゔぃ"] = "vi", ["ゔぇ"] = "ve", ["ゔぉ"] = "vo",
        ["うぃ"] = "wi", ["うぇ"] = "we", ["うぉ"] = "wo",
        ["つぁ"] = "tsa", ["つぃ"] = "tsi", ["つぇ"] = "tse", ["つぉ"] = "tso",
        ["てぃ"] = "ti", ["てゅ"] = "tyu", ["とぅ"] = "tu",
        ["でぃ"] = "di", ["でゅ"] = "dyu", ["どぅ"] = "du",
        ["すぃ"] = "si", ["ずぃ"] = "zi",
        ["いぇ"] = "ye",
        ["くぁ"] = "kwa", ["くぃ"] = "kwi", ["くぇ"] = "kwe", ["くぉ"] = "kwo",
        ["ぐぁ"] = "gwa", ["ぐぃ"] = "gwi", ["ぐぇ"] = "gwe", ["ぐぉ"] = "gwo",
    };

    static readonly Dictionary<string, string> RomajiAliases = new(StringComparer.Ordinal)
    {
        ["si"] = "し", ["ti"] = "ち", ["tu"] = "つ", ["zi"] = "じ", ["hu"] = "ふ",
        ["di"] = "ぢ", ["du"] = "づ", ["wo"] = "を",
        ["sya"] = "しゃ", ["syu"] = "しゅ", ["syo"] = "しょ", ["sye"] = "しぇ",
        ["shya"] = "しゃ", ["shyu"] = "しゅ", ["shyo"] = "しょ", ["shye"] = "しぇ",
        ["tya"] = "ちゃ", ["tyu"] = "ちゅ", ["tyo"] = "ちょ", ["tye"] = "ちぇ",
        ["cya"] = "ちゃ", ["cyu"] = "ちゅ", ["cyo"] = "ちょ", ["cye"] = "ちぇ",
        ["chya"] = "ちゃ", ["chyu"] = "ちゅ", ["chyo"] = "ちょ", ["chye"] = "ちぇ",
        ["zya"] = "じゃ", ["zyu"] = "じゅ", ["zyo"] = "じょ", ["zye"] = "じぇ",
        ["jya"] = "じゃ", ["jyu"] = "じゅ", ["jyo"] = "じょ", ["jye"] = "じぇ",
        ["dya"] = "ぢゃ", ["dyu"] = "ぢゅ", ["dyo"] = "ぢょ",
    };

    static readonly char[] Vowels = ['a', 'i', 'u', 'e', 'o'];

    static readonly Dictionary<string, string> Kana = BuildKana();

    public static IEnumerable<string> Keys() => TableEntries.Keys;

    public static IReadOnlyDictionary<string, string> Table() => TableEntries;

    public static IEnumerable<string> AliasKeys() => RomajiAliases.Keys;

    public static bool TryGetRomaji(string mora, out string romaji)
        => TableEntries.TryGetValue(ToMora(mora), out romaji!);

    public static string ToMora(string lyric)
    {
        var normalized = AliasNormalizer.Normalize(lyric);
        var separator = normalized.LastIndexOf(AliasSeparator);
        var hiragana = ToHiragana(separator >= 0 ? normalized[(separator + 1)..] : normalized);
        if (TableEntries.ContainsKey(hiragana))
            return hiragana;

        return Kana.TryGetValue(hiragana, out var mapped) ? mapped : hiragana;
    }

    static Dictionary<string, string> BuildKana()
    {
        var kana = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in TableEntries)
        {
            if (kana.TryGetValue(pair.Value, out var existing) && string.CompareOrdinal(existing, pair.Key) <= 0)
                continue;
            kana[pair.Value] = pair.Key;
        }

        foreach (var pair in RomajiAliases)
            kana[pair.Key] = pair.Value;
        return kana;
    }

    public static string? GetVowel(string mora)
    {
        if (!TryGetRomaji(mora, out var romaji))
            return null;
        if (romaji == "n")
            return "n";

        var last = romaji[^1];
        return Array.IndexOf(Vowels, last) >= 0 ? last.ToString() : null;
    }

    public static string? GetConsonant(string mora)
    {
        if (!TryGetRomaji(mora, out var romaji))
            return null;
        if (romaji == "n")
            return "n";

        var last = romaji[^1];
        if (Array.IndexOf(Vowels, last) < 0)
            return null;

        var consonant = romaji[..^1];
        return consonant.Length == 0 ? null : consonant;
    }

    public static string ToHiragana(string text)
    {
        Span<char> buffer = text.Length <= 64 ? stackalloc char[text.Length] : new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            buffer[i] = c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c;
        }
        return new string(buffer);
    }

    public static string ToKatakana(string text)
    {
        Span<char> buffer = text.Length <= 64 ? stackalloc char[text.Length] : new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            buffer[i] = c is >= 'ぁ' and <= 'ゖ' ? (char)(c + 0x60) : c;
        }
        return new string(buffer);
    }
}
