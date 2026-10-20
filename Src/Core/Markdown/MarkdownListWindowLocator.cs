using System;
using System.Text.RegularExpressions;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Locates local text windows around cursor offsets for efficient list renumbering in large documents.
/// </summary>
internal static partial class MarkdownListWindowLocator
{
    [GeneratedRegex(@"^[ \t]*\d+([.)])([ \t]+|$)", RegexOptions.Compiled)]
    private static partial Regex OrderedListItemMarkerPattern();

    public static bool IsLikelyNearOrderedList(string text, int offset)
    {
        int clampedOffset = Math.Clamp(offset, 0, text.Length);

        int lineStart = FindLineStart(text, clampedOffset);
        int lineEnd = FindLineEnd(text, clampedOffset);

        if (IsOrderedListLine(text, lineStart, lineEnd))
            return true;

        if (lineStart > 0)
        {
            int prevLineEnd = lineStart - 1;
            int prevLineStart = FindLineStart(text, prevLineEnd);
            if (IsOrderedListLine(text, prevLineStart, prevLineEnd))
                return true;
        }

        if (lineEnd < text.Length)
        {
            int nextLineStart = lineEnd + 1;
            int nextLineEnd = FindLineEnd(text, nextLineStart);
            if (IsOrderedListLine(text, nextLineStart, nextLineEnd))
                return true;
        }

        return false;
    }

    public static bool TryGetLocalListWindow(string text, int offset, out string windowText, out int windowStart)
    {
        int clampedOffset = Math.Clamp(offset, 0, text.Length);

        int start = clampedOffset;
        int consecutiveNewlines = 0;
        while (start > 0)
        {
            start--;
            if (text[start] == '\n')
            {
                consecutiveNewlines++;
                if (consecutiveNewlines >= 2)
                    break;
            }
            else if (!char.IsWhiteSpace(text[start]))
            {
                consecutiveNewlines = 0;
            }
        }
        if (start > 0 && text[start] == '\n')
            start++;

        int end = clampedOffset;
        consecutiveNewlines = 0;
        while (end < text.Length)
        {
            if (text[end] == '\n')
            {
                consecutiveNewlines++;
                if (consecutiveNewlines >= 2)
                    break;
            }
            else if (!char.IsWhiteSpace(text[end]))
            {
                consecutiveNewlines = 0;
            }
            end++;
        }

        windowStart = start;
        windowText = text[start..end];
        return windowText.Length > 0;
    }

    private static bool IsOrderedListLine(string text, int start, int end)
    {
        if (start >= end)
            return false;

        string line = text[start..end].TrimEnd('\r');
        return OrderedListItemMarkerPattern().IsMatch(line);
    }

    private static int FindLineStart(string text, int offset)
    {
        int index = Math.Clamp(offset, 0, text.Length);
        while (index > 0 && text[index - 1] != '\n')
            index--;
        return index;
    }

    private static int FindLineEnd(string text, int offset)
    {
        int index = Math.Clamp(offset, 0, text.Length);
        while (index < text.Length && text[index] != '\n')
            index++;
        return index;
    }
}
