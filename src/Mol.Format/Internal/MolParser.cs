using System.Text;

namespace Mol.Format.Internal;

internal static class MolParser
{
    public static MolParsedDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = Preprocess(text);
        var rootHeadingDepth = lines
            .Where(line => line.HeadingLevel is not null)
            .Select(line => line.HeadingLevel!.Value)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        var index = 0;
        var entries = ParseEntries(lines, ref index, indentationBoundary: null, headingBoundary: 0, rootHeadingDepth);

        if (entries.Count > 0)
        {
            return new MolParsedDocument { Entries = entries };
        }

        var rootScalar = ParseRootScalar(lines);
        return new MolParsedDocument
        {
            Entries = [],
            RootScalar = rootScalar.Value,
            RootScalarWasQuoted = rootScalar.WasQuoted,
        };
    }

    private static List<MolSyntaxEntry> ParseEntries(
        IReadOnlyList<MolLine> lines,
        ref int index,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth)
    {
        var entries = new List<MolSyntaxEntry>();

        while (index < lines.Count)
        {
            var line = lines[index];

            if (IsBoundary(line, indentationBoundary, headingBoundary, rootHeadingDepth))
            {
                break;
            }

            if (line.IsBlank)
            {
                index++;
                continue;
            }

            if (line.HeadingLevel is not null)
            {
                var entry = new MolSyntaxEntry(line.Content[(line.HeadingLevel.Value + 1)..].Trim())
                {
                    IsRootHeading = headingBoundary == 0 && NormalizeHeadingDepth(line.HeadingLevel.Value, rootHeadingDepth) == 1,
                };

                index++;
                ParseHeadingContinuation(entry, lines, ref index, indentationBoundary, NormalizeHeadingDepth(line.HeadingLevel.Value, rootHeadingDepth), rootHeadingDepth);
                entries.Add(entry);
                continue;
            }

            if (!TryParseEntryLine(lines, index, indentationBoundary, headingBoundary, rootHeadingDepth, out var parsedLine))
            {
                index++;
                continue;
            }

            index++;
            ParseIndentedContinuation(parsedLine, lines, ref index, line.Indentation, indentationBoundary, headingBoundary, rootHeadingDepth);
            entries.Add(parsedLine);
        }

        return entries;
    }

    private static bool TryParseEntryLine(
        IReadOnlyList<MolLine> lines,
        int index,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth,
        out MolSyntaxEntry entry)
    {
        var line = lines[index];
        var content = line.Content;

        if (TryStripListMarker(content, out var listContent))
        {
            content = listContent;
        }

        if (TryParseKeyValue(content, out var key, out var inlineValue, out var wasQuoted))
        {
            entry = new MolSyntaxEntry(key)
            {
                InlineValue = inlineValue,
                InlineValueWasQuoted = wasQuoted,
            };

            return true;
        }

        if (LooksLikeBareEntry(lines, index, indentationBoundary, headingBoundary, rootHeadingDepth))
        {
            entry = new MolSyntaxEntry(content.Trim());
            return true;
        }

        entry = null!;
        return false;
    }

    private static void ParseHeadingContinuation(
        MolSyntaxEntry entry,
        IReadOnlyList<MolLine> lines,
        ref int index,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth)
    {
        var firstContentIndex = FindFirstSignificantLine(lines, index, indentationBoundary, headingBoundary, rootHeadingDepth, indentationFloor: null);
        if (firstContentIndex < 0)
        {
            return;
        }

        if (IsStructuralLine(lines, firstContentIndex, indentationBoundary, headingBoundary, rootHeadingDepth))
        {
            index = firstContentIndex;
            entry.Children.AddRange(ParseEntries(lines, ref index, indentationBoundary, headingBoundary, rootHeadingDepth));
            return;
        }

        entry.TextBody = ParseTextBody(
            lines,
            ref index,
            startIndex: firstContentIndex,
            indentationBoundary,
            headingBoundary,
            rootHeadingDepth,
            indentationFloor: null,
            preserveTrailingNewline: true);
    }

    private static void ParseIndentedContinuation(
        MolSyntaxEntry entry,
        IReadOnlyList<MolLine> lines,
        ref int index,
        int lineIndentation,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth)
    {
        var firstContentIndex = FindFirstSignificantLine(lines, index, indentationBoundary, headingBoundary, rootHeadingDepth, lineIndentation);
        if (firstContentIndex < 0)
        {
            return;
        }

        if (IsStructuralLine(lines, firstContentIndex, indentationBoundary, headingBoundary, rootHeadingDepth))
        {
            index = firstContentIndex;
            entry.Children.AddRange(ParseEntries(lines, ref index, lineIndentation, headingBoundary, rootHeadingDepth));
            return;
        }

        entry.TextBody = ParseTextBody(
            lines,
            ref index,
            startIndex: firstContentIndex,
            indentationBoundary,
            headingBoundary,
            rootHeadingDepth,
            indentationFloor: lineIndentation,
            preserveTrailingNewline: false);
    }

    private static string ParseTextBody(
        IReadOnlyList<MolLine> lines,
        ref int index,
        int startIndex,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth,
        int? indentationFloor,
        bool preserveTrailingNewline)
    {
        var bodyLines = new List<MolLine>();
        var cursor = startIndex;
        var activeFenceMarker = string.Empty;

        while (cursor < lines.Count)
        {
            var line = lines[cursor];

            if (activeFenceMarker.Length == 0 && IsBoundary(line, indentationBoundary, headingBoundary, rootHeadingDepth))
            {
                break;
            }

            if (activeFenceMarker.Length == 0 &&
                indentationFloor is not null &&
                !line.IsBlank &&
                line.Indentation <= indentationFloor.Value)
            {
                break;
            }

            bodyLines.Add(line);

            if (!line.IsBlank && IsFenceLine(line.Content.Trim(), out var fenceMarker))
            {
                if (activeFenceMarker.Length == 0)
                {
                    activeFenceMarker = fenceMarker;
                }
                else if (line.Content.Trim() == activeFenceMarker)
                {
                    activeFenceMarker = string.Empty;
                }
            }

            cursor++;
        }

        var hadTrailingBlankLines = false;
        while (bodyLines.Count > 0 && bodyLines[^1].IsBlank)
        {
            hadTrailingBlankLines = true;
            bodyLines.RemoveAt(bodyLines.Count - 1);
        }

        index = cursor;
        return NormalizeTextBody(bodyLines, preserveTrailingNewline || hadTrailingBlankLines);
    }

    private static (string Value, bool WasQuoted) ParseRootScalar(IReadOnlyList<MolLine> lines)
    {
        var significantLines = lines.Where(line => !line.IsBlank).ToList();
        if (significantLines.Count == 1 && TryParseScalarLine(significantLines[0].Content, out var scalarValue, out var wasQuoted))
        {
            return (scalarValue, wasQuoted);
        }

        return (NormalizeTextBody(significantLines, preserveTrailingNewline: true), false);
    }

    private static string NormalizeTextBody(IReadOnlyList<MolLine> lines, bool preserveTrailingNewline)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var minimumIndentation = lines
            .Where(line => !line.IsBlank)
            .Select(line => line.Indentation)
            .DefaultIfEmpty(0)
            .Min();

        var normalizedLines = lines
            .Select(line => line.IsBlank
                ? string.Empty
                : RemoveIndentation(line.Raw, minimumIndentation))
            .ToList();

        if (TryExtractFenceBody(normalizedLines, preserveTrailingNewline, out var fencedValue))
        {
            return fencedValue;
        }

        normalizedLines = normalizedLines
            .Select(static line => UnescapeMarkdownText(line))
            .ToList();

        var joined = string.Join('\n', normalizedLines);
        if (preserveTrailingNewline && normalizedLines.Count > 0)
        {
            joined += '\n';
        }

        return joined;
    }

    private static bool TryExtractFenceBody(IReadOnlyList<string> lines, bool preserveTrailingNewline, out string value)
    {
        value = string.Empty;
        if (lines.Count < 2)
        {
            return false;
        }

        var openingFence = lines[0].Trim();
        if (!IsFenceLine(openingFence, out var fenceMarker))
        {
            return false;
        }

        var closingFenceIndex = -1;
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].Trim() == fenceMarker)
            {
                closingFenceIndex = i;
                break;
            }
        }

        if (closingFenceIndex < 0)
        {
            return false;
        }

        var bodyLines = lines.Skip(1).Take(closingFenceIndex - 1).ToList();
        value = string.Join('\n', bodyLines);

        if (preserveTrailingNewline && bodyLines.Count > 0)
        {
            value += '\n';
        }

        return true;
    }

    private static bool TryParseScalarLine(string content, out string value, out bool wasQuoted)
    {
        value = string.Empty;
        wasQuoted = false;

        var trimmed = content.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (TryParseQuotedValue(trimmed, out value))
        {
            wasQuoted = true;
            return true;
        }

        value = trimmed;
        return true;
    }

    private static int FindFirstSignificantLine(
        IReadOnlyList<MolLine> lines,
        int index,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth,
        int? indentationFloor)
    {
        for (var i = index; i < lines.Count; i++)
        {
            var line = lines[i];

            if (IsBoundary(line, indentationBoundary, headingBoundary, rootHeadingDepth))
            {
                return -1;
            }

            if (indentationFloor is not null && !line.IsBlank && line.Indentation <= indentationFloor.Value)
            {
                return -1;
            }

            if (!line.IsBlank)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool LooksLikeBareEntry(
        IReadOnlyList<MolLine> lines,
        int index,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth)
    {
        var line = lines[index];
        var content = line.Content.Trim();

        if (content.Length == 0 || content.Contains(':'))
        {
            return false;
        }

        for (var i = index + 1; i < lines.Count; i++)
        {
            var candidate = lines[i];

            if (IsBoundary(candidate, indentationBoundary, headingBoundary, rootHeadingDepth))
            {
                return false;
            }

            if (candidate.IsBlank)
            {
                continue;
            }

            return candidate.Indentation > line.Indentation &&
                IsPotentialStructuralEntry(lines, i, indentationBoundary, headingBoundary, rootHeadingDepth);
        }

        return false;
    }

    private static bool IsStructuralLine(
        IReadOnlyList<MolLine> lines,
        int index,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth)
    {
        var line = lines[index];
        if (line.HeadingLevel is not null)
        {
            return NormalizeHeadingDepth(line.HeadingLevel.Value, rootHeadingDepth) > headingBoundary;
        }

        return IsPotentialStructuralEntry(lines, index, indentationBoundary, headingBoundary, rootHeadingDepth);
    }

    private static bool IsBoundary(MolLine line, int? indentationBoundary, int headingBoundary, int rootHeadingDepth)
    {
        if (line.IsBlank)
        {
            return false;
        }

        if (line.HeadingLevel is not null)
        {
            if (indentationBoundary is not null && line.Indentation <= indentationBoundary.Value)
            {
                return true;
            }

            return NormalizeHeadingDepth(line.HeadingLevel.Value, rootHeadingDepth) <= headingBoundary;
        }

        return indentationBoundary is not null && line.Indentation <= indentationBoundary.Value;
    }

    private static int NormalizeHeadingDepth(int actualHeadingDepth, int rootHeadingDepth)
    {
        if (rootHeadingDepth == int.MaxValue)
        {
            return actualHeadingDepth;
        }

        return actualHeadingDepth - rootHeadingDepth + 1;
    }

    private static bool TryParseKeyValue(string content, out string key, out string? inlineValue, out bool wasQuoted)
    {
        key = string.Empty;
        inlineValue = null;
        wasQuoted = false;

        var separatorIndex = FindSeparatorIndex(content);
        if (separatorIndex < 0)
        {
            return false;
        }

        key = content[..separatorIndex].Trim();
        if (key.Length == 0 || key[0] is '"' or '\'')
        {
            return false;
        }

        var remainder = content[(separatorIndex + 1)..].TrimStart();
        if (remainder.Length == 0)
        {
            return true;
        }

        if (TryParseQuotedValue(remainder, out var quotedValue))
        {
            inlineValue = quotedValue;
            wasQuoted = true;
            return true;
        }

        inlineValue = remainder;
        return true;
    }

    private static bool IsPotentialStructuralEntry(
        IReadOnlyList<MolLine> lines,
        int index,
        int? indentationBoundary,
        int headingBoundary,
        int rootHeadingDepth)
    {
        var line = lines[index];
        if (line.HeadingLevel is not null)
        {
            return NormalizeHeadingDepth(line.HeadingLevel.Value, rootHeadingDepth) > headingBoundary;
        }

        var content = line.Content;
        if (TryStripListMarker(content, out var listContent))
        {
            content = listContent;
        }

        if (TryParseKeyValue(content, out _, out _, out _))
        {
            return true;
        }

        return LooksLikeBareEntry(lines, index, indentationBoundary, headingBoundary, rootHeadingDepth);
    }

    private static int FindSeparatorIndex(string content)
    {
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == ':' && (i == 0 || content[i - 1] != '\\'))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryStripListMarker(string content, out string remainder)
    {
        remainder = string.Empty;

        if (content.Length < 2)
        {
            return false;
        }

        if ((content[0] == '-' || content[0] == '*' || content[0] == '+') &&
            content.Length > 1 &&
            char.IsWhiteSpace(content[1]))
        {
            remainder = content[2..].TrimStart();
            return true;
        }

        var index = 0;
        while (index < content.Length && char.IsDigit(content[index]))
        {
            index++;
        }

        if (index > 0 && index + 1 < content.Length && content[index] == '.' && char.IsWhiteSpace(content[index + 1]))
        {
            remainder = content[(index + 2)..].TrimStart();
            return true;
        }

        return false;
    }

    private static bool TryParseQuotedValue(string content, out string value)
    {
        value = string.Empty;
        if (content.Length < 2)
        {
            return false;
        }

        var quote = content[0];
        if ((quote != '"' && quote != '\'') || content[^1] != quote)
        {
            return false;
        }

        var builder = new StringBuilder(content.Length - 2);
        for (var i = 1; i < content.Length - 1; i++)
        {
            var character = content[i];
            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }

            if (i + 1 >= content.Length - 1)
            {
                return false;
            }

            i++;
            builder.Append(content[i] switch
            {
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => content[i],
            });
        }

        value = builder.ToString();
        return true;
    }

    private static List<MolLine> Preprocess(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var rawLines = normalized.Split('\n');
        var lines = new List<MolLine>(rawLines.Length);
        var insideFence = false;
        string? activeFence = null;
        var insideBlockComment = false;

        foreach (var rawLine in rawLines)
        {
            var trimmed = rawLine.Trim();

            if (insideFence)
            {
                lines.Add(MolLine.Create(rawLine));
                if (activeFence is not null && trimmed == activeFence)
                {
                    insideFence = false;
                    activeFence = null;
                }

                continue;
            }

            if (insideBlockComment)
            {
                if (trimmed.Contains("*/", StringComparison.Ordinal))
                {
                    insideBlockComment = false;
                }

                lines.Add(MolLine.Blank());
                continue;
            }

            if (IsFenceLine(trimmed, out var fenceMarker))
            {
                lines.Add(MolLine.Create(rawLine));
                insideFence = true;
                activeFence = fenceMarker;
                continue;
            }

            if (trimmed.Length == 0 || trimmed == "---")
            {
                lines.Add(MolLine.Blank());
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                lines.Add(MolLine.Blank());
                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                if (!trimmed.Contains("*/", StringComparison.Ordinal))
                {
                    insideBlockComment = true;
                }

                lines.Add(MolLine.Blank());
                continue;
            }

            lines.Add(MolLine.Create(rawLine));
        }

        return lines;
    }

    private static bool IsFenceLine(string content, out string fenceMarker)
    {
        fenceMarker = string.Empty;
        if (content.Length < 3)
        {
            return false;
        }

        var fenceCharacter = content[0];
        if (fenceCharacter != '`' && fenceCharacter != '~')
        {
            return false;
        }

        var count = 0;
        while (count < content.Length && content[count] == fenceCharacter)
        {
            count++;
        }

        if (count < 3)
        {
            return false;
        }

        fenceMarker = new string(fenceCharacter, count);
        return true;
    }

    private static string RemoveIndentation(string value, int indentation)
    {
        var index = 0;
        var remaining = indentation;

        while (index < value.Length && remaining > 0 && char.IsWhiteSpace(value[index]))
        {
            remaining--;
            index++;
        }

        return value[index..];
    }

    private static string UnescapeMarkdownText(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && char.IsPunctuation(value[i + 1]))
            {
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private sealed class MolLine
    {
        private MolLine(string raw, int indentation, string content, int? headingLevel, bool isBlank)
        {
            Raw = raw;
            Indentation = indentation;
            Content = content;
            HeadingLevel = headingLevel;
            IsBlank = isBlank;
        }

        public string Raw { get; }

        public int Indentation { get; }

        public string Content { get; }

        public int? HeadingLevel { get; }

        public bool IsBlank { get; }

        public static MolLine Create(string raw)
        {
            var indentation = CountIndentation(raw);
            var content = raw[indentation..];
            var headingLevel = TryGetHeadingLevel(content);
            return new MolLine(raw, indentation, content, headingLevel, isBlank: false);
        }

        public static MolLine Blank() => new(string.Empty, 0, string.Empty, null, isBlank: true);

        private static int CountIndentation(string raw)
        {
            var count = 0;
            while (count < raw.Length && char.IsWhiteSpace(raw[count]))
            {
                count++;
            }

            return count;
        }

        private static int? TryGetHeadingLevel(string content)
        {
            if (!content.StartsWith('#'))
            {
                return null;
            }

            var level = 0;
            while (level < content.Length && content[level] == '#')
            {
                level++;
            }

            if (level == content.Length || !char.IsWhiteSpace(content[level]))
            {
                return null;
            }

            return level;
        }
    }
}
