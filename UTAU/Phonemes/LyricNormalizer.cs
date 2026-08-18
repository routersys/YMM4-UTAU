using System.Text;
using UTAU.Notes;

namespace UTAU.Phonemes;

internal static class LyricNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var source = text.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(source.Length);
        var index = 0;
        var pendingSpace = false;

        while (index < source.Length)
        {
            var c = source[index];

            if (c == NotationScanner.DirectiveOpen && index + 1 < source.Length && source[index + 1] == NotationScanner.DirectiveMarker)
            {
                var close = source.IndexOf(NotationScanner.DirectiveClose, index + 2);
                if (close >= 0)
                {
                    FlushSpace(builder, ref pendingSpace);
                    builder.Append(source, index, close - index + 1);
                    index = close + 1;
                    continue;
                }
            }

            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                index++;
                continue;
            }

            FlushSpace(builder, ref pendingSpace);
            builder.Append(c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c);
            index++;
        }

        return builder.ToString();
    }

    static void FlushSpace(StringBuilder builder, ref bool pendingSpace)
    {
        if (!pendingSpace)
            return;
        builder.Append(' ');
        pendingSpace = false;
    }
}
