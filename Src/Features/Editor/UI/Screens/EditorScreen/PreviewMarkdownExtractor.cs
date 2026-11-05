using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Extracts markdown source code corresponding to selected preview text blocks
/// by mapping rendered text positions to source document spans.
/// </summary>
public static class PreviewMarkdownExtractor
{
    private static readonly string[] MarkdownDelimiters = ["***", "**", "*", "___", "__", "_", "`", "~~"];

    /// <summary>
    /// Extracts the markdown text corresponding to the specified selected preview text blocks.
    /// Falls back to the provided plain text if the source spans are missing or invalid.
    /// </summary>
    public static string ExtractMarkdown(
        IReadOnlyList<PreviewTextBlock> selectedBlocks,
        string markdown,
        string fallbackPlainText)
    {
        if (selectedBlocks.Count == 0 || string.IsNullOrEmpty(markdown))
            return fallbackPlainText;

        (Control? _, BlockSourceSpan firstSpan) = EditorSyncResolver.FindClickedBlock(selectedBlocks[0]);
        (Control? _, BlockSourceSpan lastSpan) = EditorSyncResolver.FindClickedBlock(selectedBlocks[^1]);

        if (firstSpan.IsEmpty || lastSpan.IsEmpty)
            return fallbackPlainText;

        int startOffset = CalculateStartOffset(selectedBlocks[0], firstSpan, markdown);
        int endOffset = CalculateEndOffset(selectedBlocks[^1], lastSpan, markdown);

        return SliceMarkdownRange(markdown, startOffset, endOffset, fallbackPlainText);
    }

    private static string SliceMarkdownRange(string markdown, int startOffset, int endOffset, string fallbackPlainText)
    {
        int clampedStart = Math.Clamp(Math.Min(startOffset, endOffset), 0, markdown.Length - 1);
        int clampedEnd = Math.Clamp(Math.Max(startOffset, endOffset), clampedStart, markdown.Length - 1);
        int length = clampedEnd - clampedStart + 1;

        if (length <= 0)
            return fallbackPlainText;

        string extracted = markdown.Substring(clampedStart, length).TrimEnd('\r', '\n');
        return string.IsNullOrEmpty(extracted) ? fallbackPlainText : extracted;
    }

    private static int CalculateStartOffset(PreviewTextBlock block, BlockSourceSpan span, string markdown)
    {
        int selStart = Math.Min(block.SelectionStart, block.SelectionEnd);
        if (selStart <= 0)
            return span.StartOffset;

        int blockLength = Math.Clamp(span.EndOffset - span.StartOffset + 1, 0, markdown.Length - span.StartOffset);
        if (blockLength <= 0)
            return span.StartOffset;

        string blockMarkdown = markdown.Substring(span.StartOffset, blockLength);
        string plainText = block.GetPlainText();

        int? mapped = MapOffset(plainText, blockMarkdown, selStart);
        if (!mapped.HasValue)
            return span.StartOffset;

        int expanded = ExpandStartDelimiter(blockMarkdown, mapped.Value);
        return Math.Clamp(span.StartOffset + expanded, span.StartOffset, span.EndOffset);
    }

    private static int CalculateEndOffset(PreviewTextBlock block, BlockSourceSpan span, string markdown)
    {
        int selEnd = Math.Max(block.SelectionStart, block.SelectionEnd);
        int textLength = block.GetTextLength();
        if (selEnd >= textLength)
            return span.EndOffset;

        int blockLength = Math.Clamp(span.EndOffset - span.StartOffset + 1, 0, markdown.Length - span.StartOffset);
        if (blockLength <= 0)
            return span.EndOffset;

        string blockMarkdown = markdown.Substring(span.StartOffset, blockLength);
        string plainText = block.GetPlainText();

        int? mapped = MapOffset(plainText, blockMarkdown, selEnd - 1);
        if (!mapped.HasValue)
            return span.EndOffset;

        int expanded = ExpandEndDelimiter(blockMarkdown, mapped.Value);
        return Math.Clamp(span.StartOffset + expanded, span.StartOffset, span.EndOffset);
    }

    private static int? MapOffset(string plainText, string blockMarkdown, int plainCharIndex)
    {
        if (plainCharIndex < 0 || plainCharIndex >= plainText.Length || string.IsNullOrEmpty(blockMarkdown))
            return null;

        int mdIndex = 0;
        int plainIndex = 0;

        while (plainIndex <= plainCharIndex && mdIndex < blockMarkdown.Length)
        {
            if (blockMarkdown[mdIndex] == plainText[plainIndex])
            {
                if (plainIndex == plainCharIndex)
                    return mdIndex;

                plainIndex++;
            }

            mdIndex++;
        }

        return null;
    }

    private static int ExpandStartDelimiter(string blockMarkdown, int startOffset)
    {
        foreach (string delim in MarkdownDelimiters)
        {
            int len = delim.Length;
            if (startOffset >= len && blockMarkdown.AsSpan(startOffset - len, len).SequenceEqual(delim))
                return startOffset - len;
        }

        return startOffset;
    }

    private static int ExpandEndDelimiter(string blockMarkdown, int endOffset)
    {
        foreach (string delim in MarkdownDelimiters)
        {
            int len = delim.Length;
            if (endOffset + 1 + len <= blockMarkdown.Length && blockMarkdown.AsSpan(endOffset + 1, len).SequenceEqual(delim))
                return endOffset + len;
        }

        return endOffset;
    }
}
