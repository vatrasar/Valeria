using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Result of a pure text transformation requested by a formatting action.
/// </summary>
public sealed record FormattingResult(string Text, int CaretOffset, int SelectionLength);

/// <summary>
/// Pure text transformations backing the editor formatting toolbar.
/// All methods are side-effect free and covered by unit tests.
/// </summary>
public static partial class EditorFormatting
{
    private const string LineEnding = "\n";

    /// <summary>
    /// Toggles wrapping of the selection with the given markers, inserting placeholder when empty.
    /// Used by bold, italic, strikethrough and inline code toolbar actions.
    /// </summary>
    public static FormattingResult ToggleWrap(string text, int selectionStart, int selectionLength, string marker, string placeholder)
    {
        string safeText = text ?? string.Empty;
        ClampSelection(safeText, ref selectionStart, ref selectionLength);

        if (TryUnwrap(safeText, selectionStart, selectionLength, marker, out FormattingResult? unwrapped))
            return unwrapped;

        return Wrap(safeText, selectionStart, selectionLength, marker, marker, placeholder);
    }

    /// <summary>
    /// Wraps the selection with different opening and closing markers.
    /// Used by the link toolbar action.
    /// </summary>
    public static FormattingResult WrapLink(string text, int selectionStart, int selectionLength, string placeholder)
    {
        string safeText = text ?? string.Empty;
        ClampSelection(safeText, ref selectionStart, ref selectionLength);

        return Wrap(safeText, selectionStart, selectionLength, "[", "](https://)", placeholder);
    }

    /// <summary>
    /// Toggles a heading of the given level on every line intersecting the selection.
    /// Used by heading toolbar actions.
    /// </summary>
    public static FormattingResult ToggleHeading(string text, int selectionStart, int selectionLength, int level)
    {
        string safeText = text ?? string.Empty;
        ClampSelection(safeText, ref selectionStart, ref selectionLength);

        int clampedLevel = Math.Clamp(level, 1, 6);
        string prefix = new string('#', clampedLevel) + " ";

        return MapSelectedLines(safeText, selectionStart, selectionLength, line => ToggleHeadingLine(line, prefix));
    }

    /// <summary>
    /// Toggles an unordered list marker on every line intersecting the selection.
    /// Used by the bullet list toolbar action.
    /// </summary>
    public static FormattingResult ToggleBulletList(string text, int selectionStart, int selectionLength)
    {
        string safeText = text ?? string.Empty;
        ClampSelection(safeText, ref selectionStart, ref selectionLength);

        return MapSelectedLines(safeText, selectionStart, selectionLength, line => ToggleLinePrefix(line, "- "));
    }

    /// <summary>
    /// Toggles an ordered list marker on every line intersecting the selection.
    /// Used by the numbered list toolbar action.
    /// </summary>
    public static FormattingResult ToggleOrderedList(string text, int selectionStart, int selectionLength)
    {
        string safeText = text ?? string.Empty;
        ClampSelection(safeText, ref selectionStart, ref selectionLength);

        int number = 1;

        return MapSelectedLines(safeText, selectionStart, selectionLength, line => ToggleOrderedLine(line, ref number));
    }

    /// <summary>
    /// Toggles a quote marker on every line intersecting the selection.
    /// Used by the quote toolbar action.
    /// </summary>
    public static FormattingResult ToggleQuote(string text, int selectionStart, int selectionLength)
    {
        string safeText = text ?? string.Empty;
        ClampSelection(safeText, ref selectionStart, ref selectionLength);

        return MapSelectedLines(safeText, selectionStart, selectionLength, line => ToggleLinePrefix(line, "> "));
    }

    /// <summary>
    /// Inserts a new empty list item after the line at the caret when that line
    /// is a markdown list item (bullet or ordered). Returns null otherwise.
    /// Used by the Ctrl+Enter shortcut.
    /// </summary>
    public static FormattingResult? ContinueListItem(string text, int caretOffset)
    {
        string safeText = text ?? string.Empty;
        int caret = Math.Clamp(caretOffset, 0, safeText.Length);
        int lineStart = FindLineStart(safeText, caret);
        int lineEnd = FindLineEnd(safeText, caret);
        string line = safeText.Substring(lineStart, lineEnd - lineStart);

        string? marker = BuildContinuedMarker(line);

        if (marker is null)
            return null;

        string updated = safeText.Insert(lineEnd, LineEnding + marker);
        int newCaret = lineEnd + 1 + marker.Length;

        return new FormattingResult(updated, newCaret, 0);
    }

    /// <summary>
    /// Changes one task-list marker while preserving the rest of the markdown source.
    /// Used by the interactive task checkboxes in the rendered preview.
    /// </summary>
    public static FormattingResult? ToggleTask(string text, int taskIndex, bool isChecked)
    {
        string safeText = text ?? string.Empty;

        if (taskIndex < 0)
            return null;

        IReadOnlyList<int> markerIndexes = FindTaskMarkerIndexes(safeText);

        if (taskIndex >= markerIndexes.Count)
            return null;

        int markerIndex = markerIndexes[taskIndex];
        string replacement = isChecked ? "[x]" : "[ ]";
        string updated = safeText.Substring(0, markerIndex) + replacement + safeText.Substring(markerIndex + replacement.Length);

        return new FormattingResult(updated, markerIndex, 0);
    }

    /// <summary>
    /// Wraps selected lines in a fenced code block, or inserts an empty fence at the caret.
    /// Used by the code block toolbar action.
    /// </summary>
    public static FormattingResult InsertCodeFence(string text, int selectionStart, int selectionLength, string language)
    {
        string safeText = text ?? string.Empty;
        ClampSelection(safeText, ref selectionStart, ref selectionLength);

        string fence = string.IsNullOrWhiteSpace(language) ? "```" : "```" + language.Trim();
        string selected = safeText.Substring(selectionStart, selectionLength);
        string body = selectionLength == 0 ? string.Empty : selected.Trim('\n');

        string replacement = fence + LineEnding + body + (body.Length == 0 ? string.Empty : LineEnding) + "```";
        string updated = safeText.Substring(0, selectionStart) + replacement + safeText.Substring(selectionStart + selectionLength);

        int caret = selectionStart + fence.Length + 1;

        return new FormattingResult(updated, caret, 0);
    }

    /// <summary>
    /// Inserts a starter pipe table at the caret position.
    /// Used by the table toolbar action.
    /// </summary>
    public static FormattingResult InsertTable(string text, int caretOffset)
    {
        string safeText = text ?? string.Empty;
        int caret = Math.Clamp(caretOffset, 0, safeText.Length);
        int lineStart = FindLineStart(safeText, caret);
        bool needsLeadingBreak = lineStart > 0 && safeText[lineStart - 1] != '\n';

        string template = "| Header 1 | Header 2 |\n| --- | --- |\n| Cell | Cell |\n";
        string insertion = (needsLeadingBreak ? "\n" : string.Empty) + "\n" + template;

        string updated = safeText.Substring(0, caret) + insertion + safeText.Substring(caret);

        return new FormattingResult(updated, caret + insertion.Length, 0);
    }

    private static void ClampSelection(string text, ref int start, ref int length)
    {
        start = Math.Clamp(start, 0, text.Length);
        length = Math.Clamp(length, 0, text.Length - start);
    }

    private static bool TryUnwrap(string text, int start, int length, string marker, [NotNullWhen(true)] out FormattingResult? result)
    {
        result = null;

        if (length == 0 || marker.Length == 0)
            return false;

        string selected = text.Substring(start, length);

        if (!selected.StartsWith(marker, StringComparison.Ordinal) || !selected.EndsWith(marker, StringComparison.Ordinal))
            return false;

        if (selected.Length < marker.Length * 2)
            return false;

        string unwrapped = selected.Substring(marker.Length, selected.Length - marker.Length * 2);
        string updated = text.Substring(0, start) + unwrapped + text.Substring(start + length);

        result = new FormattingResult(updated, start, unwrapped.Length);

        return true;
    }

    private static FormattingResult Wrap(string text, int start, int length, string before, string after, string placeholder)
    {
        string selected = text.Substring(start, length);
        bool isEmpty = selected.Length == 0;
        string inner = isEmpty ? placeholder : selected;

        string updated = text.Substring(0, start) + before + inner + after + text.Substring(start + length);

        if (isEmpty)
            return new FormattingResult(updated, start + before.Length, inner.Length);

        return new FormattingResult(updated, start, (before + inner + after).Length);
    }

    private static FormattingResult MapSelectedLines(string text, int start, int length, Func<string, string> map)
    {
        int blockStart = FindLineStart(text, start);
        int blockEnd = FindLineEnd(text, start + length);
        string[] lines = text.Substring(blockStart, blockEnd - blockStart).Split('\n');

        for (int index = 0; index < lines.Length; index++)
            lines[index] = map(lines[index]);

        string replacement = string.Join(LineEnding, lines);
        string updated = text.Substring(0, blockStart) + replacement + text.Substring(blockEnd);

        return new FormattingResult(updated, blockStart, replacement.Length);
    }

    private static int FindLineStart(string text, int offset)
    {
        int searchFrom = Math.Min(offset, text.Length);
        int newline = text.LastIndexOf('\n', Math.Max(0, searchFrom - 1));

        return newline < 0 ? 0 : newline + 1;
    }

    private static int FindLineEnd(string text, int offset)
    {
        int newline = text.IndexOf('\n', Math.Min(offset, text.Length));

        return newline < 0 ? text.Length : newline;
    }

    private static string ToggleLinePrefix(string line, string prefix)
    {
        if (line.StartsWith(prefix, StringComparison.Ordinal))
            return line.Substring(prefix.Length);

        return prefix + line;
    }

    private static string ToggleHeadingLine(string line, string prefix)
    {
        Match heading = HeadingPattern().Match(line);

        if (!heading.Success)
            return prefix + line;

        if (heading.Value == prefix)
            return line.Substring(heading.Length);

        return prefix + line.Substring(heading.Length);
    }

    private static string ToggleOrderedLine(string line, ref int number)
    {
        Match ordered = OrderedListPattern().Match(line);
        string replacement = ordered.Success ? string.Empty : number + ". ";
        number++;

        if (ordered.Success)
            return line.Substring(ordered.Length);

        return replacement + line;
    }

    private static string? BuildContinuedMarker(string line)
    {
        int index = SkipIndentation(line);
        string leading = line.Substring(0, index);

        if (TryGetBulletPrefix(line, index, out string? bulletPrefix))
            return leading + bulletPrefix;

        if (TryGetOrderedPrefix(line, out string? orderedPrefix))
            return leading + orderedPrefix;

        return null;
    }

    private static IReadOnlyList<int> FindTaskMarkerIndexes(string text)
    {
        List<int> markerIndexes = [];
        bool insideFence = false;

        foreach (Match line in MarkdownLinePattern().Matches(text))
            AddTaskMarkerFromLine(line, markerIndexes, ref insideFence);

        return markerIndexes;
    }

    private static void AddTaskMarkerFromLine(Match line, List<int> markerIndexes, ref bool insideFence)
    {
        string content = line.Value.TrimEnd('\r', '\n');

        if (FencePattern().IsMatch(content))
        {
            insideFence = !insideFence;
            return;
        }

        if (insideFence)
            return;

        Match task = TaskMarkerPattern().Match(content);

        if (task.Success)
            markerIndexes.Add(line.Index + task.Groups["state"].Index - 1);
    }

    private static int SkipIndentation(string line)
    {
        int index = 0;

        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;

        return index;
    }

    private static bool TryGetBulletPrefix(string line, int contentStart, [NotNullWhen(true)] out string? prefix)
    {
        prefix = null;

        if (line.Length <= contentStart || line[contentStart] is not ('-' or '*' or '+'))
            return false;

        if (line.Length <= contentStart + 1 || line[contentStart + 1] != ' ')
            return false;

        prefix = line[contentStart] + " ";

        return true;
    }

    private static bool TryGetOrderedPrefix(string line, [NotNullWhen(true)] out string? prefix)
    {
        prefix = null;
        Match ordered = OrderedListItemPattern().Match(line);

        if (!ordered.Success || !int.TryParse(ordered.Groups[1].Value, out int number))
            return false;

        prefix = number + 1 + ordered.Groups[2].Value + " ";

        return true;
    }

    [GeneratedRegex(@"^#{1,6} ")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\d+[.)] ")]
    private static partial Regex OrderedListPattern();

    [GeneratedRegex(@"^\s*(\d+)([.)])")]
    private static partial Regex OrderedListItemPattern();

    [GeneratedRegex(@"^.*(?:\r?\n|$)", RegexOptions.Multiline)]
    private static partial Regex MarkdownLinePattern();

    [GeneratedRegex(@"^\s*(?:>\s*)*(?:`{3,}|~{3,})")]
    private static partial Regex FencePattern();

    [GeneratedRegex(@"^[ \t]*(?:>[ \t]*)*(?:[-+*]|\d+[.)])[ \t]+\[(?<state>[ xX])\]")]
    private static partial Regex TaskMarkerPattern();
}
