using System.Text;

namespace UTAU.Models;

internal static class CharacterYamlReader
{
    const string SubBanksKey = "subbanks";
    const string ToneRangesKey = "tone_ranges";

    readonly record struct YamlLine(int Indent, bool IsSequenceItem, string Content);

    public static CharacterYaml Parse(string content)
    {
        var lines = ReadLines(content);
        var scalars = new List<KeyValuePair<string, string>>();
        var subBanks = new List<SubBank>();
        var documentIndent = lines.Count > 0 ? lines.Min(x => x.Indent) : 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Indent != documentIndent || line.IsSequenceItem)
                continue;

            if (!TrySplitMapping(line.Content, out var key, out var value))
                continue;

            if (string.Equals(key, SubBanksKey, StringComparison.OrdinalIgnoreCase))
            {
                var end = i + 1;
                while (end < lines.Count && lines[end].Indent > documentIndent)
                    end++;
                subBanks.AddRange(ParseSubBanks(lines, i + 1, end));
                i = end - 1;
                continue;
            }

            if (value.Length > 0)
                scalars.Add(new KeyValuePair<string, string>(key, value));
        }

        return new CharacterYaml { Scalars = scalars, SubBanks = subBanks };
    }

    static List<YamlLine> ReadLines(string content)
    {
        var lines = new List<YamlLine>();
        foreach (var raw in VoiceBankTextReader.ReadLines(content))
        {
            var line = StripComment(raw).TrimEnd();
            if (line.Trim().Length == 0)
                continue;

            var indent = 0;
            while (indent < line.Length && line[indent] == ' ')
                indent++;
            if (indent < line.Length && line[indent] == '\t')
                continue;

            var body = line[indent..];
            var isSequenceItem = body.StartsWith("- ", StringComparison.Ordinal) || body == "-";
            if (isSequenceItem)
            {
                indent += body.Length > 1 ? 2 : 1;
                body = body.Length > 1 ? body[2..].Trim() : string.Empty;
            }

            lines.Add(new YamlLine(indent, isSequenceItem, body));
        }
        return lines;
    }

    static IEnumerable<SubBank> ParseSubBanks(List<YamlLine> lines, int start, int end)
    {
        var itemIndent = int.MaxValue;
        for (var i = start; i < end; i++)
            if (lines[i].IsSequenceItem && lines[i].Indent < itemIndent)
                itemIndent = lines[i].Indent;
        if (itemIndent == int.MaxValue)
            yield break;

        var index = start;
        while (index < end)
        {
            if (!lines[index].IsSequenceItem || lines[index].Indent != itemIndent)
            {
                index++;
                continue;
            }

            var itemEnd = index + 1;
            while (itemEnd < end && !(lines[itemEnd].IsSequenceItem && lines[itemEnd].Indent == itemIndent))
                itemEnd++;

            var subBank = ParseSubBank(lines, index, itemEnd, itemIndent);
            if (subBank is not null)
                yield return subBank;
            index = itemEnd;
        }
    }

    static SubBank? ParseSubBank(List<YamlLine> lines, int start, int end, int itemIndent)
    {
        var color = string.Empty;
        var prefix = string.Empty;
        var suffix = string.Empty;
        var toneRanges = new List<ToneRange>();
        var hasField = false;

        for (var i = start; i < end; i++)
        {
            var line = lines[i];
            if (line.IsSequenceItem && line.Indent > itemIndent)
                continue;
            if (!TrySplitMapping(line.Content, out var key, out var value))
                continue;

            hasField = true;
            if (string.Equals(key, ToneRangesKey, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var token in EnumerateSequence(lines, i, end, value))
                    if (ToneRange.TryParse(token, out var range))
                        toneRanges.Add(range);
            }
            else if (string.Equals(key, "color", StringComparison.OrdinalIgnoreCase))
                color = value;
            else if (string.Equals(key, "prefix", StringComparison.OrdinalIgnoreCase))
                prefix = value;
            else if (string.Equals(key, "suffix", StringComparison.OrdinalIgnoreCase))
                suffix = value;
        }

        return hasField
            ? new SubBank { Color = color, Prefix = prefix, Suffix = suffix, ToneRanges = toneRanges }
            : null;
    }

    static IEnumerable<string> EnumerateSequence(List<YamlLine> lines, int keyIndex, int end, string inlineValue)
    {
        if (inlineValue.Length > 0)
        {
            foreach (var token in ParseFlowSequence(inlineValue))
                yield return token;
            yield break;
        }

        var keyIndent = lines[keyIndex].Indent;
        for (var i = keyIndex + 1; i < end; i++)
        {
            var line = lines[i];
            if (!line.IsSequenceItem)
                yield break;
            if (line.Indent <= keyIndent)
                yield break;
            if (line.Content.Length > 0)
                yield return Unquote(line.Content);
        }
    }

    static IEnumerable<string> ParseFlowSequence(string value)
    {
        var text = value.Trim();
        if (text.StartsWith('[') && text.EndsWith(']'))
            text = text[1..^1];

        foreach (var token in text.Split(','))
        {
            var trimmed = Unquote(token.Trim());
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }

    static bool TrySplitMapping(string content, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var quote = '\0';
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c != ':')
                continue;
            if (i + 1 < content.Length && content[i + 1] != ' ')
                continue;

            key = Unquote(content[..i].Trim());
            value = Unquote(content[(i + 1)..].Trim());
            return key.Length > 0;
        }

        return false;
    }

    static string Unquote(string value)
    {
        if (value.Length < 2)
            return value;

        var quote = value[0];
        if ((quote != '"' && quote != '\'') || value[^1] != quote)
            return value;

        var inner = value[1..^1];
        if (quote == '\'')
            return inner.Replace("''", "'");

        var builder = new StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length)
            {
                i++;
                builder.Append(inner[i] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => inner[i],
                });
                continue;
            }
            builder.Append(inner[i]);
        }
        return builder.ToString();
    }

    static string StripComment(string line)
    {
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
                return line[..i];
        }
        return line;
    }
}
