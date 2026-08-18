using System.IO;
using YukkuriMovieMaker.Commons;

namespace UTAU.Models;

internal static class VoiceBankPaths
{
    public const string DirectoryName = "utau";

    public static string DefaultDirectory => Path.Combine(AppDirectories.UserResourceDirectory, DirectoryName);

    public static string CreateId(string searchDirectory, string bankDirectory)
    {
        var relative = Path.GetRelativePath(searchDirectory, bankDirectory);
        if (relative.Length == 0 || relative == "." || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            relative = bankDirectory;
        return Uri.EscapeDataString(relative.Replace(Path.DirectorySeparatorChar, '/'));
    }
}
