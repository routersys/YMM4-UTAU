using System.IO;
using UTAU.Models;

namespace UTAU.Tests;

public sealed class CharacterProfileParserTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    [Fact]
    public void ReadsKnownKeys()
    {
        var profile = CharacterProfileParser.Parse("name=歌音\r\nauthor=作者\r\nweb=https://example.invalid/\r\nversion=1.2\r\n", directory);
        Assert.Equal("歌音", profile.Name);
        Assert.Equal("作者", profile.Author);
        Assert.Equal("https://example.invalid/", profile.Web);
        Assert.Equal("1.2", profile.Version);
    }

    [Fact]
    public void VoiceKeyIsTreatedAsTheAuthor()
        => Assert.Equal("声の人", CharacterProfileParser.Parse("voice=声の人", directory).Author);

    [Fact]
    public void FirstOccurrenceWins()
        => Assert.Equal("最初", CharacterProfileParser.Parse("name=最初\r\nname=あと", directory).Name);

    [Fact]
    public void UnknownKeysArePreserved()
    {
        var profile = CharacterProfileParser.Parse("独自=値\r\nname=x", directory);
        Assert.Equal(new KeyValuePair<string, string>("独自", "値"), Assert.Single(profile.AdditionalEntries));
    }

    [Fact]
    public void EmptyValuesAreIgnored()
        => Assert.Null(CharacterProfileParser.Parse("name=\r\n=値\r\nnovalue", directory).Name);

    [Fact]
    public void ImagePathIsResolvedWhenTheFileExists()
    {
        File.WriteAllBytes(Path.Combine(directory, "icon.bmp"), [0]);
        var profile = CharacterProfileParser.Parse("image=icon.bmp", directory);
        Assert.Equal(Path.Combine(directory, "icon.bmp"), profile.ImagePath);
    }

    [Fact]
    public void MissingImageFileResolvesToNull()
        => Assert.Null(CharacterProfileParser.Parse("image=absent.bmp", directory).ImagePath);

    [Fact]
    public void BackslashPathsAreAccepted()
    {
        Directory.CreateDirectory(Path.Combine(directory, "sub"));
        File.WriteAllBytes(Path.Combine(directory, "sub", "icon.png"), [0]);
        Assert.Equal(
            Path.Combine(directory, "sub", "icon.png"),
            CharacterProfileParser.Parse(@"image=sub\icon.png", directory).ImagePath);
    }

    [Fact]
    public void PathsEscapingTheBankAreRejected()
    {
        var outside = Path.Combine(Path.GetDirectoryName(directory)!, "outside.png");
        File.WriteAllBytes(outside, [0]);
        try
        {
            Assert.Null(CharacterProfileParser.Parse(@"image=..\outside.png", directory).ImagePath);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void AbsolutePathsOutsideTheBankAreRejected()
        => Assert.Null(CharacterProfileParser.ResolveRelativePath(directory, @"C:\Windows\explorer.exe"));
}

public sealed class CharacterYamlReaderTests
{
    [Fact]
    public void ReadsTopLevelScalars()
    {
        var yaml = CharacterYamlReader.Parse("name: 歌音\r\nsinger_type: utau\r\ntext_file_encoding: shift_jis\r\n");
        Assert.Equal("歌音", yaml.Find("name"));
        Assert.Equal("utau", yaml.Find("singer_type"));
        Assert.Equal("shift_jis", yaml.Find("text_file_encoding"));
    }

    [Fact]
    public void UnknownScalarsArePreserved()
        => Assert.Equal("0.67", CharacterYamlReader.Parse("portrait_opacity: 0.67").Find("portrait_opacity"));

    [Fact]
    public void StripsComments()
    {
        var yaml = CharacterYamlReader.Parse("# leading comment\r\nname: 歌音 # trailing\r\n");
        Assert.Equal("歌音", yaml.Find("name"));
    }

    [Fact]
    public void KeepsHashInsideQuotes()
        => Assert.Equal("a#b", CharacterYamlReader.Parse("name: \"a#b\"").Find("name"));

    [Fact]
    public void UnquotesSingleAndDoubleQuotedScalars()
    {
        Assert.Equal("値", CharacterYamlReader.Parse("name: '値'").Find("name"));
        Assert.Equal("値", CharacterYamlReader.Parse("name: \"値\"").Find("name"));
        Assert.Equal("a'b", CharacterYamlReader.Parse("name: 'a''b'").Find("name"));
        Assert.Equal("a\tb", CharacterYamlReader.Parse("name: \"a\\tb\"").Find("name"));
    }

    [Fact]
    public void ReadsSubBanksWithBlockToneRanges()
    {
        var yaml = CharacterYamlReader.Parse(
            """
            name: 歌音
            subbanks:
              - color: Power
                prefix: ""
                suffix: 強
                tone_ranges:
                  - C1-B7
              - color: ""
                suffix: _C5
                tone_ranges:
                  - C5-B7
            """);

        Assert.Equal(2, yaml.SubBanks.Count);
        Assert.Equal("Power", yaml.SubBanks[0].Color);
        Assert.Equal(string.Empty, yaml.SubBanks[0].Prefix);
        Assert.Equal("強", yaml.SubBanks[0].Suffix);
        Assert.Equal(new ToneRange(24, 107), Assert.Single(yaml.SubBanks[0].ToneRanges));
        Assert.Equal(string.Empty, yaml.SubBanks[1].Color);
        Assert.Equal("_C5", yaml.SubBanks[1].Suffix);
        Assert.Equal(new ToneRange(72, 107), Assert.Single(yaml.SubBanks[1].ToneRanges));
    }

    [Fact]
    public void ReadsFlowSequenceToneRanges()
    {
        var yaml = CharacterYamlReader.Parse(
            """
            subbanks:
              - color: A
                tone_ranges: [C1-B3, C4-B7]
            """);

        Assert.Equal(2, Assert.Single(yaml.SubBanks).ToneRanges.Count);
    }

    [Fact]
    public void SubBanksWithoutToneRangesCoverEveryTone()
    {
        var subBank = Assert.Single(CharacterYamlReader.Parse("subbanks:\r\n  - color: A\r\n    suffix: x\r\n").SubBanks);
        Assert.True(subBank.Covers(0));
        Assert.True(subBank.Covers(127));
    }

    [Fact]
    public void ScalarsAfterSubBanksAreStillRead()
    {
        var yaml = CharacterYamlReader.Parse(
            """
            subbanks:
              - color: A
                suffix: x
            image: icon.png
            """);

        Assert.Equal("icon.png", yaml.Find("image"));
        Assert.Single(yaml.SubBanks);
    }

    [Fact]
    public void MalformedDocumentsProduceNoSubBanksInsteadOfThrowing()
    {
        var yaml = CharacterYamlReader.Parse("subbanks:\r\n\tcolor: A\r\n  not a mapping\r\n");
        Assert.Empty(yaml.SubBanks);
    }

    [Fact]
    public void EmptyDocumentIsHandled()
    {
        var yaml = CharacterYamlReader.Parse(string.Empty);
        Assert.Empty(yaml.Scalars);
        Assert.Empty(yaml.SubBanks);
    }

    [Fact]
    public void EveryDocumentedTopLevelKeyIsRead()
    {
        var yaml = CharacterYamlReader.Parse(
            """
            name: 歌音
            singer_type: utau
            text_file_encoding: shift_jis
            image: icon.png
            portrait: portrait.png
            portrait_opacity: 0.67
            portrait_height: 0
            author: 作者
            voice: 声
            version: 1.2
            web: https://example.invalid/
            default_phonemizer: OpenUtau.Plugin.Builtin.JapaneseVCVPhonemizer
            """);

        Assert.Equal("歌音", yaml.Find("name"));
        Assert.Equal("utau", yaml.Find("singer_type"));
        Assert.Equal("shift_jis", yaml.Find("text_file_encoding"));
        Assert.Equal("icon.png", yaml.Find("image"));
        Assert.Equal("portrait.png", yaml.Find("portrait"));
        Assert.Equal("0.67", yaml.Find("portrait_opacity"));
        Assert.Equal("0", yaml.Find("portrait_height"));
        Assert.Equal("作者", yaml.Find("author"));
        Assert.Equal("声", yaml.Find("voice"));
        Assert.Equal("1.2", yaml.Find("version"));
        Assert.Equal("https://example.invalid/", yaml.Find("web"));
        Assert.Equal("OpenUtau.Plugin.Builtin.JapaneseVCVPhonemizer", yaml.Find("default_phonemizer"));
    }

    [Fact]
    public void KnownKeysAreNotRepeatedAsAdditionalScalars()
    {
        var yaml = CharacterYamlReader.Parse(
            """
            name: 歌音
            author: 作者
            portrait_opacity: 0.67
            """);
        var additional = yaml.EnumerateAdditionalScalars().Select(x => x.Key).ToArray();

        Assert.DoesNotContain("name", additional);
        Assert.DoesNotContain("author", additional);
        Assert.Contains("portrait_opacity", additional);
    }

    [Fact]
    public void NestedMapsDoNotLeakIntoTheScalars()
    {
        var yaml = CharacterYamlReader.Parse(
            """
            name: 歌音
            localized_names:
              en: Kaon
              ja: 歌音
            web: https://example.invalid/
            """);

        Assert.Equal("歌音", yaml.Find("name"));
        Assert.Equal("https://example.invalid/", yaml.Find("web"));
        Assert.DoesNotContain(yaml.Scalars, x => x.Key is "en" or "ja");
    }

    [Fact]
    public void ColonWithoutSpaceIsNotAMappingSeparator()
        => Assert.Equal("https://example.invalid/", CharacterYamlReader.Parse("web: https://example.invalid/").Find("web"));
}
