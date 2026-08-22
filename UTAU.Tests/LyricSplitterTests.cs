using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UTAU.Notes;

namespace UTAU.Tests;

public sealed class LyricSplitterTests
{
    static class Reference
    {
        static readonly Regex whitespace = new(@"\s");
        static readonly Regex standalone = new(
            @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}|\p{IsHangulSyllables}");
        static readonly Regex contracted = new(@"[ゃゅょぁぃぅぇぉャュョァィゥェォ]");

        public static List<string> Split(string? text)
        {
            if (text == null)
            {
                return new List<string>();
            }
            var lyrics = new List<string>();
            var builder = new StringBuilder();
            var etor = StringInfo.GetTextElementEnumerator(text);
            while (etor.MoveNext())
            {
                string ele = etor.GetTextElement();
                if (whitespace.IsMatch(ele))
                {
                    if (builder.Length > 0)
                    {
                        lyrics.Add(builder.ToString());
                        builder.Clear();
                    }
                }
                else if (contracted.IsMatch(ele))
                {
                    builder.Append(ele);
                    lyrics.Add(builder.ToString());
                    builder.Clear();
                }
                else if (standalone.IsMatch(ele))
                {
                    if (builder.Length > 0)
                    {
                        lyrics.Add(builder.ToString());
                        builder.Clear();
                    }
                    builder.Append(ele);
                }
                else if (ele == "\"")
                {
                    while (etor.MoveNext())
                    {
                        string ele1 = etor.GetTextElement();
                        if (ele1 == "\"")
                        {
                            lyrics.Add(builder.ToString());
                            builder.Clear();
                            break;
                        }
                        else
                        {
                            builder.Append(ele1);
                        }
                    }
                }
                else
                {
                    if (builder.Length > 0 && standalone.IsMatch(builder.ToString()))
                    {
                        lyrics.Add(builder.ToString());
                        builder.Clear();
                    }
                    builder.Append(ele);
                }
            }
            if (builder.Length > 0)
            {
                lyrics.Add(builder.ToString());
                builder.Clear();
            }
            return lyrics;
        }

        public static string Join(IEnumerable<string> lyrics)
        {
            var builder = new StringBuilder();
            foreach (string lyric in lyrics)
            {
                if (builder.Length != 0)
                {
                    builder.Append(" ");
                }
                if (lyric.Length == 0)
                {
                    builder.Append("\"\"");
                }
                else if (whitespace.IsMatch(lyric))
                {
                    builder.Append($"\"{lyric}\"");
                }
                else if (standalone.IsMatch(lyric))
                {
                    if (lyric.Length == 1)
                    {
                        builder.Append(lyric);
                    }
                    else if (lyric.Length == 2 && contracted.IsMatch(lyric.Substring(1)))
                    {
                        builder.Append(lyric);
                    }
                    else
                    {
                        builder.Append($"\"{lyric}\"");
                    }
                }
                else
                {
                    builder.Append(lyric);
                }
            }
            return builder.ToString();
        }
    }

    static readonly string[] Alphabet =
    [
        "a", "b", "z", "R", "-", "+", "_", "1", "#", "[", "]",
        " ", "\t", "\n", "\r", "　", "\"",
        "あ", "か", "し", "ん", "ゃ", "ゅ", "ょ", "ぁ",
        "ア", "カ", "ャ", "ヴ", "ー",
        "中", "文", "русский", "가", "각", "𠮷", "é",
    ];

    static string RandomText(Random random)
    {
        var builder = new StringBuilder();
        var length = random.Next(0, 12);
        for (var index = 0; index < length; index++)
            builder.Append(Alphabet[random.Next(Alphabet.Length)]);
        return builder.ToString();
    }

    [Fact]
    public void SplittingSeparatesWordsAndStandaloneCharacters()
    {
        Assert.Equal(
            ["a", "word", "中", "文", "русский", "가", "각", "갂", "ひ", "ら", "が", "な", "がな", "", "two words"],
            LyricSplitter.Split("a word中文русский가각갂ひらがな \"がな\" \"\" \"two words\""));
    }

    [Fact]
    public void JoiningQuotesOnlyWhatSplittingWouldNotRecover()
    {
        Assert.Equal(
            "a word 中 文 русский 가 각 갂 ひ ら が な \"がな\" \"\" \"two words\"",
            LyricSplitter.Join(["a", "word", "中", "文", "русский", "가", "각", "갂", "ひ", "ら", "が", "な", "がな", "", "two words"]));
    }

    [Fact]
    public void ASmallKanaStaysWithTheKanaBeforeIt()
    {
        Assert.Equal(["きゃ", "ら", "ば", "ん"], LyricSplitter.Split("きゃらばん"));
    }

    [Fact]
    public void ASmallKanaAfterALatinWordKeepsThatWord()
    {
        Assert.Equal(["aゃ"], LyricSplitter.Split("aゃ"));
    }

    [Fact]
    public void AnUnclosedQuoteSwallowsTheRest()
    {
        Assert.Equal(["ab"], LyricSplitter.Split("\"ab"));
    }

    [Fact]
    public void AQuoteAfterLatinCharactersKeepsThemInTheSameLyric()
    {
        Assert.Equal(["abcdef"], LyricSplitter.Split("abc\"def\""));
    }

    [Fact]
    public void EveryKindOfWhitespaceSeparates()
    {
        Assert.Equal(["a", "b", "c", "d", "e"], LyricSplitter.Split("a b\tc\nd\re"));
    }

    [Fact]
    public void SplittingNothingGivesNothing()
    {
        Assert.Empty(LyricSplitter.Split(null));
        Assert.Empty(LyricSplitter.Split(string.Empty));
        Assert.Empty(LyricSplitter.Split("   "));
    }

    [Fact]
    public void JoiningAndSplittingRestoresEveryLyric()
    {
        string[] lyrics =
        [
            "a", "word", "中", "文", "中文", " ", "    ", "-", "12 3# $%^",
            "中русский", "русский", "ひ", "ら", "が", "な", "がな", "two words", "가", "각", "갂", "갃간",
        ];

        Assert.Equal(lyrics, LyricSplitter.Split(LyricSplitter.Join(lyrics)));
    }

    [Fact]
    public void SplittingMatchesTheOpenUtauImplementation()
    {
        var random = new Random(20260822);
        var mismatch = 0;
        var first = string.Empty;
        for (var trial = 0; trial < 100000; trial++)
        {
            var text = RandomText(random);
            if (LyricSplitter.Split(text).SequenceEqual(Reference.Split(text)))
                continue;

            if (mismatch == 0)
                first = text;
            mismatch++;
        }

        Assert.True(mismatch == 0, $"mismatch={mismatch} first={first}");
    }

    [Fact]
    public void JoiningMatchesTheOpenUtauImplementation()
    {
        var random = new Random(20260823);
        var mismatch = 0;
        var first = string.Empty;
        for (var trial = 0; trial < 100000; trial++)
        {
            var lyrics = Reference.Split(RandomText(random));
            if (LyricSplitter.Join(lyrics) == Reference.Join(lyrics))
                continue;

            if (mismatch == 0)
                first = string.Join("|", lyrics);
            mismatch++;
        }

        Assert.True(mismatch == 0, $"mismatch={mismatch} first={first}");
    }
}
