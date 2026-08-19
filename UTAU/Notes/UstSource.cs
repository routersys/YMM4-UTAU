using System.IO;

namespace UTAU.Notes;

internal static class UstSource
{
    public const string Extension = ".ust";
    public const char Quote = '"';

    static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    public static bool TryGetPath(string? text, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == Quote && trimmed[^1] == Quote)
            trimmed = trimmed[1..^1].Trim();

        if (trimmed.Length <= Extension.Length)
            return false;
        if (!trimmed.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return false;
        if (trimmed.AsSpan().IndexOfAny(InvalidPathChars) >= 0)
            return false;

        path = trimmed;
        return true;
    }
}
