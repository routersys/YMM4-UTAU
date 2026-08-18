using System.IO;

namespace UTAU.Models;

internal static class VoiceBankScanner
{
    public const int MaximumSearchDepth = 3;

    static readonly EnumerationOptions DirectoryOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple,
        AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint,
    };

    public static IReadOnlyList<string> FindBankDirectories(string searchDirectory, int maximumDepth = MaximumSearchDepth)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(searchDirectory) || !Directory.Exists(searchDirectory))
            return results;

        Collect(Path.GetFullPath(searchDirectory), maximumDepth, results);
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    public static bool IsBankDirectory(string directory)
        => File.Exists(Path.Combine(directory, VoiceBankLoader.CharacterFileName))
            || File.Exists(Path.Combine(directory, VoiceBankLoader.CharacterYamlFileName))
            || File.Exists(Path.Combine(directory, VoiceBankLoader.OtoFileName));

    static void Collect(string directory, int remainingDepth, List<string> results)
    {
        if (IsBankDirectory(directory))
        {
            results.Add(directory);
            return;
        }

        if (remainingDepth <= 0)
            return;

        foreach (var child in EnumerateDirectories(directory))
            Collect(child, remainingDepth - 1, results);
    }

    static IEnumerable<string> EnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory, "*", DirectoryOptions).ToArray();
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
}
