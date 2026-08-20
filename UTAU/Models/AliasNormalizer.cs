using System.Globalization;
using System.Text;

namespace UTAU.Models;

internal static class AliasNormalizer
{
    enum Requirement
    {
        None,
        Spacing,
        Compose,
    }

    public static string Normalize(string text)
    {
        var requirement = Inspect(text);
        if (requirement == Requirement.None)
            return text;

        var source = requirement == Requirement.Compose ? Compose(text) : text;
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

    public static ReadOnlySpan<char> Normalize(ReadOnlySpan<char> text, Span<char> buffer)
    {
        var requirement = Inspect(text);
        if (requirement == Requirement.None)
            return text;
        if (requirement == Requirement.Compose || buffer.Length < text.Length)
            return Normalize(text.ToString());

        return buffer[..CollapseSpaces(text, buffer)];
    }

    static Requirement Inspect(ReadOnlySpan<char> text)
    {
        var previousWasSpace = true;
        var spacing = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (previousWasSpace || c != ' ')
                    spacing = true;
                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            if (!char.IsAscii(c) && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                return Requirement.Compose;
        }

        if (previousWasSpace && text.Length > 0)
            spacing = true;

        return spacing ? Requirement.Spacing : Requirement.None;
    }

    static int CollapseSpaces(ReadOnlySpan<char> source, Span<char> destination)
    {
        var written = 0;
        var pendingSpace = false;

        foreach (var c in source)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = written > 0;
                continue;
            }

            if (pendingSpace)
                destination[written++] = ' ';
            pendingSpace = false;
            destination[written++] = c;
        }

        return written;
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
