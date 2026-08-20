using UTAU.Models;

namespace UTAU.Phonemes;

internal static class KanaRomanization
{
    public const string StartVowel = "-";
    public const string SilenceConsonant = "-";
    public const string AnyVowel = "*";
    public const string SyllabicNasal = "n";
    public const char AliasSeparator = ' ';
    public const int StackTextLength = 128;

    static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
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

    static readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> TableLookup
        = Table.GetAlternateLookup<ReadOnlySpan<char>>();

    static readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> KanaLookup
        = Kana.GetAlternateLookup<ReadOnlySpan<char>>();

    public static bool TryGetRomaji(string mora, out string romaji)
    {
        Span<char> buffer = stackalloc char[StackTextLength];
        return TryGetRomaji(mora.AsSpan(), buffer, out romaji);
    }

    public static bool TryGetRomaji(ReadOnlySpan<char> mora, Span<char> buffer, out string romaji)
        => TableLookup.TryGetValue(ToMora(mora, buffer), out romaji!);

    public static string ToMora(string lyric)
    {
        Span<char> buffer = stackalloc char[StackTextLength];
        var mora = ToMora(lyric.AsSpan(), buffer);
        return mora.SequenceEqual(lyric) ? lyric : mora.ToString();
    }

    public static ReadOnlySpan<char> ToMora(ReadOnlySpan<char> lyric, Span<char> buffer)
    {
        var half = buffer.Length / 2;
        var normalized = AliasNormalizer.Normalize(lyric, buffer[..half]);
        var separator = normalized.LastIndexOf(AliasSeparator);
        var tail = separator >= 0 ? normalized[(separator + 1)..] : normalized;
        var hiragana = ToHiragana(tail, buffer[half..]);

        if (TableLookup.ContainsKey(hiragana))
            return hiragana;

        return KanaLookup.TryGetValue(hiragana, out var mapped) ? mapped : hiragana;
    }

    static Dictionary<string, string> BuildKana()
    {
        var kana = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in Table)
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
        Span<char> buffer = stackalloc char[StackTextLength];
        var vowel = GetVowel(mora.AsSpan(), buffer);
        return vowel.IsEmpty ? null : vowel.ToString();
    }

    public static ReadOnlySpan<char> GetVowel(ReadOnlySpan<char> mora, Span<char> buffer)
    {
        if (!TryGetRomaji(mora, buffer, out var romaji))
            return default;
        if (romaji == SyllabicNasal)
            return SyllabicNasal;

        var last = romaji.Length - 1;
        return Array.IndexOf(Vowels, romaji[last]) >= 0 ? romaji.AsSpan(last) : default;
    }

    public static string? GetConsonant(string mora)
    {
        Span<char> buffer = stackalloc char[StackTextLength];
        var consonant = GetConsonant(mora.AsSpan(), buffer);
        return consonant.IsEmpty ? null : consonant.ToString();
    }

    public static ReadOnlySpan<char> GetConsonant(ReadOnlySpan<char> mora, Span<char> buffer)
    {
        if (!TryGetRomaji(mora, buffer, out var romaji))
            return default;
        if (romaji == SyllabicNasal)
            return SyllabicNasal;

        var last = romaji.Length - 1;
        if (Array.IndexOf(Vowels, romaji[last]) < 0)
            return default;

        return last == 0 ? default : romaji.AsSpan(0, last);
    }

    public static string ToHiragana(string text)
    {
        Span<char> buffer = stackalloc char[StackTextLength];
        var hiragana = ToHiragana(text.AsSpan(), buffer);
        return hiragana.SequenceEqual(text) ? text : hiragana.ToString();
    }

    public static ReadOnlySpan<char> ToHiragana(ReadOnlySpan<char> text, Span<char> buffer)
        => Shift(text, buffer, 'ァ', 'ヶ', -0x60);

    public static string ToKatakana(string text)
    {
        Span<char> buffer = stackalloc char[StackTextLength];
        var katakana = ToKatakana(text.AsSpan(), buffer);
        return katakana.SequenceEqual(text) ? text : katakana.ToString();
    }

    public static ReadOnlySpan<char> ToKatakana(ReadOnlySpan<char> text, Span<char> buffer)
        => Shift(text, buffer, 'ぁ', 'ゖ', 0x60);

    static ReadOnlySpan<char> Shift(ReadOnlySpan<char> text, Span<char> buffer, char low, char high, int delta)
    {
        var first = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] < low || text[i] > high)
                continue;
            first = i;
            break;
        }

        if (first < 0)
            return text;
        if (buffer.Length < text.Length)
            return new string(ShiftToNew(text, low, high, delta));

        text.CopyTo(buffer);
        var target = buffer[..text.Length];
        for (var i = first; i < target.Length; i++)
            if (target[i] >= low && target[i] <= high)
                target[i] = (char)(target[i] + delta);
        return target;
    }

    static char[] ShiftToNew(ReadOnlySpan<char> text, char low, char high, int delta)
    {
        var copy = text.ToArray();
        for (var i = 0; i < copy.Length; i++)
            if (copy[i] >= low && copy[i] <= high)
                copy[i] = (char)(copy[i] + delta);
        return copy;
    }
}
