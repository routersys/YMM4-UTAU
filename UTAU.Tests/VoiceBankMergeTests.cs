using System.IO;
using System.Text;
using UTAU.Models;

namespace UTAU.Tests;

public sealed class VoiceBankMergeTests : IDisposable
{
    readonly string directory = TestVoiceBank.CreateTemporaryDirectory();

    public void Dispose() => TestVoiceBank.DeleteDirectory(directory);

    VoiceBank Load() => VoiceBankLoader.Load("id", directory);

    [Fact]
    public void CharacterYamlSuppliesTheProfileWhenThereIsNoCharacterFile()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, "a.wav=あ,0,0,0,0,0");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.CharacterYamlFileName,
            "name: 歌音\r\nauthor: 作者\r\nweb: https://example.invalid/\r\nversion: 1.2\r\n",
            Encoding.UTF8);

        var bank = Load();
        Assert.Equal("歌音", bank.Character.Name);
        Assert.Equal("作者", bank.Character.Author);
        Assert.Equal("https://example.invalid/", bank.Character.Web);
        Assert.Equal("1.2", bank.Character.Version);
    }

    [Fact]
    public void CharacterFileTakesPrecedenceOverCharacterYaml()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, "a.wav=あ,0,0,0,0,0");
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name: ignored\r\nname=本体\r\nauthor=本体作者\r\n");
        TestVoiceBank.WriteText(
            directory,
            VoiceBankLoader.CharacterYamlFileName,
            "name: yaml名\r\nauthor: yaml作者\r\nweb: https://example.invalid/\r\n",
            Encoding.UTF8);

        var bank = Load();
        Assert.Equal("本体", bank.Character.Name);
        Assert.Equal("本体作者", bank.Character.Author);
        Assert.Equal("https://example.invalid/", bank.Character.Web);
    }

    [Fact]
    public void TheVoiceKeyOfCharacterYamlActsAsTheAuthor()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, "a.wav=あ,0,0,0,0,0");
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterYamlFileName, "voice: 声の人\r\n", Encoding.UTF8);

        Assert.Equal("声の人", Load().Character.Author);
    }

    [Fact]
    public void ThePortraitIsResolvedOnlyWhenTheFileExists()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, "a.wav=あ,0,0,0,0,0");
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterYamlFileName, "portrait: portrait.png\r\n", Encoding.UTF8);
        Assert.Null(Load().PortraitPath);

        File.WriteAllBytes(Path.Combine(directory, "portrait.png"), [0]);
        Assert.Equal(Path.Combine(directory, "portrait.png"), Load().PortraitPath);
    }

    [Fact]
    public void TheDeclaredEncodingOfCharacterYamlIsUsedForTheOtherFiles()
    {
        TestVoiceBank.WriteText(directory, VoiceBankLoader.OtoFileName, "a.wav=あ,0,0,0,0,0", Encoding.UTF8);
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterFileName, "name=歌音\r\n", Encoding.UTF8);
        TestVoiceBank.WriteText(directory, VoiceBankLoader.CharacterYamlFileName, "text_file_encoding: utf-8\r\n", Encoding.UTF8);

        var bank = Load();
        Assert.Equal("歌音", bank.Character.Name);
        Assert.NotNull(bank.Resolve("あ", 60, null));
    }
}
