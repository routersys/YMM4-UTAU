using System.IO;

namespace UTAU.Models;

internal static class CharacterProfileParser
{
    static readonly string[] AuthorKeys = ["author", "voice", "created by"];

    public static CharacterProfile Parse(string content, string rootDirectory)
    {
        string? name = null;
        string? author = null;
        string? web = null;
        string? version = null;
        string? image = null;
        string? sample = null;
        var additional = new List<KeyValuePair<string, string>>();

        foreach (var line in VoiceBankTextReader.ReadLines(content))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
                continue;

            if (Matches(key, "name"))
                name ??= value;
            else if (AuthorKeys.Any(x => Matches(key, x)))
                author ??= value;
            else if (Matches(key, "web") || Matches(key, "url"))
                web ??= value;
            else if (Matches(key, "version"))
                version ??= value;
            else if (Matches(key, "image"))
                image ??= value;
            else if (Matches(key, "sample"))
                sample ??= value;
            else
                additional.Add(new KeyValuePair<string, string>(key, value));
        }

        return new CharacterProfile
        {
            Name = name,
            Author = author,
            Web = web,
            Version = version,
            ImagePath = ResolveRelativePath(rootDirectory, image),
            SamplePath = ResolveRelativePath(rootDirectory, sample),
            AdditionalEntries = additional,
        };
    }

    public static string? ResolveRelativePath(string rootDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var normalized = relativePath.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (normalized.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return null;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(rootDirectory, normalized));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }

        var rootFullPath = Path.GetFullPath(rootDirectory);
        if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(fullPath) ? fullPath : null;
    }

    static bool Matches(string key, string expected)
        => string.Equals(key, expected, StringComparison.OrdinalIgnoreCase);
}
