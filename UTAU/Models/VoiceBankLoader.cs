using System.IO;
using System.Text;

namespace UTAU.Models;

internal static class VoiceBankLoader
{
    public const string CharacterFileName = "character.txt";
    public const string CharacterYamlFileName = "character.yaml";
    public const string OtoFileName = "oto.ini";
    public const string PrefixMapFileName = "prefix.map";
    public const string ReadmeFileName = "readme.txt";

    static readonly EnumerationOptions RecursiveOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple,
        AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint,
    };

    public static VoiceBank Load(string id, string rootDirectory)
    {
        var yaml = ReadCharacterYaml(rootDirectory);
        var declaredEncoding = VoiceBankTextReader.ResolveDeclaredEncoding(yaml.Find("text_file_encoding"));
        var character = ReadCharacterProfile(rootDirectory, yaml, declaredEncoding);
        var prefixMap = ReadPrefixMap(rootDirectory, declaredEncoding);
        var readme = ReadOptionalText(Path.Combine(rootDirectory, ReadmeFileName), declaredEncoding);
        var portrait = CharacterProfileParser.ResolveRelativePath(rootDirectory, yaml.Find("portrait"));

        return new VoiceBank(
            id,
            rootDirectory,
            character,
            yaml,
            prefixMap,
            yaml.SubBanks,
            ReadOtoSets(rootDirectory, declaredEncoding),
            readme,
            portrait);
    }

    public static IReadOnlyList<OtoSet> ReadOtoSets(string rootDirectory, Encoding? declaredEncoding)
    {
        var sets = new List<OtoSet>();
        foreach (var otoPath in EnumerateOtoFiles(rootDirectory))
        {
            var directory = Path.GetDirectoryName(otoPath);
            if (directory is null)
                continue;

            var content = VoiceBankTextReader.ReadAllText(otoPath, declaredEncoding);
            if (content is null)
                continue;

            var entries = OtoIniParser.Parse(directory, content);
            if (entries.Count > 0)
                sets.Add(new OtoSet(directory, entries));
        }
        return sets;
    }

    public static IEnumerable<string> EnumerateOtoFiles(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            return [];

        try
        {
            return Directory
                .EnumerateFiles(rootDirectory, OtoFileName, RecursiveOptions)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    static CharacterYaml ReadCharacterYaml(string rootDirectory)
    {
        var content = ReadOptionalText(Path.Combine(rootDirectory, CharacterYamlFileName), null);
        return content is null ? CharacterYaml.Empty : CharacterYamlReader.Parse(content);
    }

    static CharacterProfile ReadCharacterProfile(string rootDirectory, CharacterYaml yaml, Encoding? declaredEncoding)
    {
        var content = ReadOptionalText(Path.Combine(rootDirectory, CharacterFileName), declaredEncoding);
        var profile = content is null
            ? CharacterProfile.Empty
            : CharacterProfileParser.Parse(content, rootDirectory);

        var yamlName = yaml.Find("name");
        var yamlImage = CharacterProfileParser.ResolveRelativePath(rootDirectory, yaml.Find("image"));
        if (yamlName is null && yamlImage is null)
            return profile;

        return new CharacterProfile
        {
            Name = profile.Name ?? yamlName,
            Author = profile.Author,
            Web = profile.Web,
            Version = profile.Version,
            ImagePath = profile.ImagePath ?? yamlImage,
            SamplePath = profile.SamplePath,
            AdditionalEntries = profile.AdditionalEntries,
        };
    }

    static PrefixMap? ReadPrefixMap(string rootDirectory, Encoding? declaredEncoding)
    {
        var content = ReadOptionalText(Path.Combine(rootDirectory, PrefixMapFileName), declaredEncoding);
        if (content is null)
            return null;

        var map = PrefixMap.Parse(content);
        return map.Count > 0 ? map : null;
    }

    static string? ReadOptionalText(string path, Encoding? declaredEncoding)
        => File.Exists(path) ? VoiceBankTextReader.ReadAllText(path, declaredEncoding) : null;
}
