using System.Globalization;
using System.Text;

namespace UTAU.Models;

internal static class AliasNormalizer
{
    public static string Normalize(string text)
    {
        if (!NeedsNormalization(text))
            return text;

        var source = Compose(text);
        var builder = new StringBuilder(source.Length);
        var pendingSpace = false;

        foreach (var c in source)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(c);
        }

        return builder.ToString();
    }

    static bool NeedsNormalization(string text)
    {
        var previousWasSpace = true;
        var hasCombiningMark = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (previousWasSpace || c != ' ')
                    return true;
                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            if (!char.IsAscii(c) && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                hasCombiningMark = true;
        }

        return hasCombiningMark || (previousWasSpace && text.Length > 0);
    }

    static string Compose(string text)
    {
        try
        {
            return text.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return text;
        }
    }
}
