using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Computes renumbering adjustments for markdown ordered lists when list items are deleted.
/// </summary>
public static partial class MarkdownListRenumberer
{
    [GeneratedRegex(@"^([ \t]*(?:>[ \t]*)*)(\d+)([.)])(?:[ \t]+|$)")]
    private static partial Regex OrderedListItemMarkerPattern();

    [GeneratedRegex(@"^.*(?:\r?\n|$)", RegexOptions.Multiline)]
    private static partial Regex MarkdownLinePattern();

    [GeneratedRegex(@"^\s*(?:>\s*)*(?:`{3,}|~{3,})")]
    private static partial Regex FencePattern();

    /// <summary>
    /// Renumbers subsequent items in an ordered list after an item has been deleted at the specified offset.
    /// Returns a new FormattingResult containing the updated markdown text, or null if no adjustment was needed.
    /// </summary>
    public static FormattingResult? RenumberOrderedListAfterDeletion(string text, int offset)
    {
        string safeText = text ?? string.Empty;
        IReadOnlyList<ListNumberAdjustment> adjustments = GetOrderedListAdjustmentsAfterDeletion(safeText, offset);

        if (adjustments.Count == 0)
            return null;

        StringBuilder builder = new(safeText);

        for (int index = adjustments.Count - 1; index >= 0; index--)
            ApplyAdjustmentToStringBuilder(builder, adjustments[index]);

        return new FormattingResult(builder.ToString(), Math.Clamp(offset, 0, builder.Length), 0);
    }

    /// <summary>
    /// Computes the list number replacements needed to renumber subsequent ordered list items
    /// when an item is removed from an ordered list at or near the specified character offset.
    /// Returns an empty list if no renumbering is needed.
    /// </summary>
    public static IReadOnlyList<ListNumberAdjustment> GetOrderedListAdjustmentsAfterDeletion(string text, int offset)
    {
        string safeText = text ?? string.Empty;

        if (safeText.Length == 0)
            return Array.Empty<ListNumberAdjustment>();

        List<ParsedLineInfo> lines = ParseAllLines(safeText);
        int targetLineIndex = FindLineIndexAtOffset(lines, offset);

        if (!TryFindTargetListMarker(lines, targetLineIndex, out string prefix, out char delimiter))
            return Array.Empty<ListNumberAdjustment>();

        int blockStartIndex = FindBlockStartIndex(lines, targetLineIndex, prefix);
        int blockEndIndex = FindBlockEndIndex(lines, targetLineIndex, prefix);

        List<ParsedLineInfo> items = CollectMatchingItems(lines, blockStartIndex, blockEndIndex, prefix, delimiter);

        return ComputeAdjustments(items, targetLineIndex);
    }

    private sealed record ParsedLineInfo(
        int LineIndex,
        int StartOffset,
        int Length,
        string Content,
        bool IsFence,
        bool IsEmpty,
        bool IsOrderedItem,
        string Prefix,
        int Number,
        char Delimiter,
        int NumberStartOffset,
        int NumberLength
    );

    private static void ApplyAdjustmentToStringBuilder(StringBuilder builder, ListNumberAdjustment adjustment)
    {
        builder.Remove(adjustment.Offset, adjustment.Length);
        builder.Insert(adjustment.Offset, adjustment.NewNumber);
    }

    private static List<ParsedLineInfo> ParseAllLines(string text)
    {
        MatchCollection matches = MarkdownLinePattern().Matches(text);
        List<ParsedLineInfo> lines = new(matches.Count);
        bool insideFence = false;

        for (int index = 0; index < matches.Count; index++)
            lines.Add(ParseSingleLine(matches[index], index, ref insideFence));

        return lines;
    }

    private static ParsedLineInfo ParseSingleLine(Match match, int lineIndex, ref bool insideFence)
    {
        string rawContent = match.Value;
        string content = rawContent.TrimEnd('\r', '\n');
        bool isFence = FencePattern().IsMatch(content);

        if (isFence)
        {
            insideFence = !insideFence;
            return CreateNonItemLine(lineIndex, match.Index, rawContent.Length, content, isFence: true, isEmpty: false);
        }

        if (insideFence)
            return CreateNonItemLine(lineIndex, match.Index, rawContent.Length, content, isFence: false, isEmpty: false);

        bool isEmpty = string.IsNullOrWhiteSpace(content);

        if (isEmpty)
            return CreateNonItemLine(lineIndex, match.Index, rawContent.Length, content, isFence: false, isEmpty: true);

        Match itemMatch = OrderedListItemMarkerPattern().Match(content);

        if (!itemMatch.Success || !int.TryParse(itemMatch.Groups[2].Value, out int number))
            return CreateNonItemLine(lineIndex, match.Index, rawContent.Length, content, isFence: false, isEmpty: false);

        string prefix = itemMatch.Groups[1].Value;
        char delimiter = itemMatch.Groups[3].Value[0];
        int numberStartOffset = match.Index + itemMatch.Groups[2].Index;
        int numberLength = itemMatch.Groups[2].Length;

        return new ParsedLineInfo(
            lineIndex,
            match.Index,
            rawContent.Length,
            content,
            false,
            false,
            true,
            prefix,
            number,
            delimiter,
            numberStartOffset,
            numberLength
        );
    }

    private static ParsedLineInfo CreateNonItemLine(int lineIndex, int startOffset, int length, string content, bool isFence, bool isEmpty)
    {
        return new ParsedLineInfo(
            lineIndex,
            startOffset,
            length,
            content,
            isFence,
            isEmpty,
            false,
            string.Empty,
            0,
            '\0',
            0,
            0
        );
    }

    private static int FindLineIndexAtOffset(IReadOnlyList<ParsedLineInfo> lines, int offset)
    {
        if (lines.Count == 0)
            return 0;

        int clampedOffset = Math.Clamp(offset, 0, int.MaxValue);

        for (int index = 0; index < lines.Count; index++)
        {
            if (IsOffsetWithinLine(lines[index], clampedOffset, index == lines.Count - 1))
                return index;
        }

        return lines.Count - 1;
    }

    private static bool IsOffsetWithinLine(ParsedLineInfo line, int offset, bool isLastLine)
    {
        if (isLastLine)
            return offset >= line.StartOffset && offset <= line.StartOffset + line.Length;

        return offset >= line.StartOffset && offset < line.StartOffset + line.Length;
    }

    private static bool TryFindTargetListMarker(
        IReadOnlyList<ParsedLineInfo> lines,
        int targetLineIndex,
        out string prefix,
        out char delimiter)
    {
        prefix = string.Empty;
        delimiter = '\0';

        if (targetLineIndex < 0 || targetLineIndex >= lines.Count)
            return false;

        if (lines[targetLineIndex].IsOrderedItem)
        {
            prefix = lines[targetLineIndex].Prefix;
            delimiter = lines[targetLineIndex].Delimiter;
            return true;
        }

        if (TryFindPrecedingMarker(lines, targetLineIndex, out prefix, out delimiter))
            return true;

        return TryFindFollowingMarker(lines, targetLineIndex, out prefix, out delimiter);
    }

    private static bool TryFindPrecedingMarker(
        IReadOnlyList<ParsedLineInfo> lines,
        int startIndex,
        out string prefix,
        out char delimiter)
    {
        prefix = string.Empty;
        delimiter = '\0';
        int emptyLineCount = 0;

        for (int index = startIndex - 1; index >= 0; index--)
        {
            if (CheckMarkerCandidateLine(lines[index], ref emptyLineCount, out prefix, out delimiter))
                return true;

            if (emptyLineCount >= 2)
                return false;
        }

        return false;
    }

    private static bool TryFindFollowingMarker(
        IReadOnlyList<ParsedLineInfo> lines,
        int startIndex,
        out string prefix,
        out char delimiter)
    {
        prefix = string.Empty;
        delimiter = '\0';
        int emptyLineCount = 0;

        for (int index = startIndex + 1; index < lines.Count; index++)
        {
            if (CheckMarkerCandidateLine(lines[index], ref emptyLineCount, out prefix, out delimiter))
                return true;

            if (emptyLineCount >= 2)
                return false;
        }

        return false;
    }

    private static bool CheckMarkerCandidateLine(
        ParsedLineInfo line,
        ref int emptyLineCount,
        out string prefix,
        out char delimiter)
    {
        prefix = string.Empty;
        delimiter = '\0';

        if (ShouldStopSearch(line, ref emptyLineCount))
            return false;

        if (line.IsOrderedItem)
        {
            prefix = line.Prefix;
            delimiter = line.Delimiter;
            return true;
        }

        return false;
    }

    private static bool ShouldStopSearch(ParsedLineInfo line, ref int emptyLineCount)
    {
        if (line.IsFence)
            return true;

        if (line.IsEmpty)
        {
            emptyLineCount++;
            return emptyLineCount >= 2;
        }

        emptyLineCount = 0;

        return !line.IsOrderedItem && IsTopLevelParagraph(line);
    }

    private static bool IsTopLevelParagraph(ParsedLineInfo line)
    {
        return !line.Content.StartsWith(' ') && !line.Content.StartsWith('\t');
    }

    private static int FindBlockStartIndex(
        IReadOnlyList<ParsedLineInfo> lines,
        int targetLineIndex,
        string prefix)
    {
        int startIndex = targetLineIndex;
        int emptyCount = 0;

        for (int index = targetLineIndex - 1; index >= 0; index--)
        {
            if (!TryExtendBlockStart(lines[index], prefix, targetLineIndex, ref emptyCount, ref startIndex))
                break;
        }

        return startIndex;
    }

    private static bool TryExtendBlockStart(
        ParsedLineInfo line,
        string prefix,
        int targetLineIndex,
        ref int emptyCount,
        ref int startIndex)
    {
        if (ShouldStopBlockBoundary(line, prefix, targetLineIndex, ref emptyCount))
            return false;

        if (line.IsOrderedItem && line.Prefix == prefix)
            startIndex = line.LineIndex;

        return true;
    }

    private static int FindBlockEndIndex(
        IReadOnlyList<ParsedLineInfo> lines,
        int targetLineIndex,
        string prefix)
    {
        int endIndex = targetLineIndex;
        int emptyCount = 0;

        for (int index = targetLineIndex; index < lines.Count; index++)
        {
            if (!TryExtendBlockEnd(lines[index], prefix, targetLineIndex, ref emptyCount, ref endIndex))
                break;
        }

        return endIndex;
    }

    private static bool TryExtendBlockEnd(
        ParsedLineInfo line,
        string prefix,
        int targetLineIndex,
        ref int emptyCount,
        ref int endIndex)
    {
        if (ShouldStopBlockBoundary(line, prefix, targetLineIndex, ref emptyCount))
            return false;

        if (line.IsOrderedItem && line.Prefix == prefix)
            endIndex = line.LineIndex;

        return true;
    }

    private static bool ShouldStopBlockBoundary(
        ParsedLineInfo line,
        string prefix,
        int targetLineIndex,
        ref int emptyCount)
    {
        if (line.IsFence)
            return true;

        if (line.IsEmpty)
        {
            emptyCount++;
            return emptyCount >= 2;
        }

        emptyCount = 0;

        if (line.LineIndex == targetLineIndex)
            return false;

        return IsTerminatingNonListLine(line, prefix);
    }

    private static bool IsTerminatingNonListLine(ParsedLineInfo line, string prefix)
    {
        if (line.IsOrderedItem)
            return false;

        if (line.Content.StartsWith('#') || line.Content.StartsWith("---", StringComparison.Ordinal) || line.Content.StartsWith("***", StringComparison.Ordinal))
            return true;

        int indentLength = SkipIndentation(line.Content);

        if (indentLength < prefix.Length)
            return true;

        if (indentLength == prefix.Length && line.Content.Length > indentLength)
        {
            char firstChar = line.Content[indentLength];
            if (firstChar is '-' or '*' or '+')
                return true;

            return true;
        }

        return false;
    }

    private static int SkipIndentation(string line)
    {
        int index = 0;

        while (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
            index++;

        return index;
    }

    private static List<ParsedLineInfo> CollectMatchingItems(
        IReadOnlyList<ParsedLineInfo> lines,
        int startIndex,
        int endIndex,
        string prefix,
        char delimiter)
    {
        List<ParsedLineInfo> matching = [];

        for (int index = startIndex; index <= endIndex; index++)
            TryAddMatchingItem(lines[index], prefix, delimiter, matching);

        return matching;
    }

    private static void TryAddMatchingItem(
        ParsedLineInfo line,
        string prefix,
        char delimiter,
        List<ParsedLineInfo> matching)
    {
        if (line.IsOrderedItem && line.Prefix == prefix && line.Delimiter == delimiter)
            matching.Add(line);
    }

    private static List<ListNumberAdjustment> ComputeAdjustments(
        IReadOnlyList<ParsedLineInfo> items,
        int targetLineIndex)
    {
        List<ListNumberAdjustment> adjustments = [];

        if (items.Count == 0 || IsLazyCommonMarkList(items))
            return adjustments;

        int expectedNumber = DetermineInitialExpectedNumber(items, targetLineIndex);

        for (int index = 0; index < items.Count; index++)
            ProcessItemAdjustment(items[index], targetLineIndex, ref expectedNumber, adjustments);

        return adjustments;
    }

    private static int DetermineInitialExpectedNumber(IReadOnlyList<ParsedLineInfo> items, int targetLineIndex)
    {
        ParsedLineInfo firstItem = items[0];

        if (firstItem.LineIndex >= targetLineIndex)
            return 1;

        return firstItem.Number;
    }

    private static void ProcessItemAdjustment(
        ParsedLineInfo item,
        int targetLineIndex,
        ref int expectedNumber,
        List<ListNumberAdjustment> adjustments)
    {
        if (item.LineIndex < targetLineIndex)
        {
            expectedNumber = item.Number + 1;
            return;
        }

        if (item.Number != expectedNumber)
            adjustments.Add(new ListNumberAdjustment(item.NumberStartOffset, item.NumberLength, expectedNumber.ToString()));

        expectedNumber++;
    }

    private static bool IsLazyCommonMarkList(IReadOnlyList<ParsedLineInfo> items)
    {
        if (items.Count <= 1)
            return false;

        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].Number != 1)
                return false;
        }

        return true;
    }
}
