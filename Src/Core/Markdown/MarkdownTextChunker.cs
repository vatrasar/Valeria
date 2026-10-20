using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Partitions raw markdown source into text slices along safe block boundaries for parallel processing.
/// </summary>
public static class MarkdownTextChunker
{
    public const int DefaultChunkSize = 40;
    public const int LargeDocumentThreshold = 200;

    /// <summary>
    /// Slices raw markdown text into chunk slices for parallel processing.
    /// Preserves single-chunk mode for small documents.
    /// </summary>
    public static ImmutableList<TextChunkSlice> SliceDocument(string? markdown, int chunkSize = DefaultChunkSize)
    {
        if (string.IsNullOrEmpty(markdown))
            return ImmutableList<TextChunkSlice>.Empty;

        List<LineSpan> lines = ScanLines(markdown);
        int contentLines = CountContentLines(lines, markdown);

        if (contentLines < LargeDocumentThreshold)
            return CreateSingleSlice(markdown, lines.Count);

        return PartitionLines(markdown, lines, chunkSize);
    }

    private static int CountContentLines(List<LineSpan> lines, string markdown)
    {
        int count = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            LineSpan line = lines[i];
            if (line.Length > 0 && !IsWhitespace(markdown, line.Offset, line.Length))
                count++;
        }

        return count;
    }

    private static bool IsWhitespace(string text, int offset, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (!char.IsWhiteSpace(text[offset + i]))
                return false;
        }

        return true;
    }

    private static ImmutableList<TextChunkSlice> CreateSingleSlice(string markdown, int totalLines)
    {
        TextChunkSlice single = new(
            ChunkIndex: 0,
            StartLine: 1,
            EndLine: Math.Max(1, totalLines),
            StartOffset: 0,
            EndOffset: markdown.Length,
            Text: markdown,
            OrderedListStart: null);

        return ImmutableList.Create(single);
    }

    private static ImmutableList<TextChunkSlice> PartitionLines(string markdown, List<LineSpan> lines, int chunkSize)
    {
        ImmutableList<TextChunkSlice>.Builder slices = ImmutableList.CreateBuilder<TextChunkSlice>();
        ChunkScanState state = new();

        for (int i = 0; i < lines.Count; i++)
        {
            LineSpan line = lines[i];
            string lineText = markdown.Substring(line.Offset, line.Length);

            UpdateFenceState(state, lineText);
            TrackOrderedListState(state, lineText);

            if (!string.IsNullOrWhiteSpace(lineText))
                state.AccumulatedElements++;

            bool isLastLine = i == lines.Count - 1;
            bool canSplit = ShouldSplitAtLine(state, lineText, isLastLine, chunkSize);

            if (canSplit)
                EmitSlice(markdown, lines, state, i, slices);
        }

        return slices.ToImmutable();
    }

    private static void UpdateFenceState(ChunkScanState state, string lineText)
    {
        string trimmed = lineText.TrimStart();
        if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
        {
            string marker = trimmed.Substring(0, 3);
            if (!state.InFencedCode)
            {
                state.InFencedCode = true;
                state.FenceDelimiter = marker;
            }
            else if (marker == state.FenceDelimiter)
            {
                state.InFencedCode = false;
                state.FenceDelimiter = null;
            }
        }
    }

    private static void TrackOrderedListState(ChunkScanState state, string lineText)
    {
        if (state.InFencedCode)
            return;

        int? orderedNumber = TryExtractOrderedNumber(lineText);
        if (orderedNumber.HasValue)
        {
            state.CurrentOrderedStart ??= orderedNumber.Value;
            state.LastOrderedNumber = orderedNumber.Value;
        }
        else if (string.IsNullOrWhiteSpace(lineText))
        {
            state.CurrentOrderedStart = null;
            state.LastOrderedNumber = null;
        }
    }

    private static bool ShouldSplitAtLine(ChunkScanState state, string lineText, bool isLastLine, int chunkSize)
    {
        if (isLastLine)
            return true;

        if (state.InFencedCode)
            return false;

        if (state.AccumulatedElements < chunkSize)
            return false;

        return IsSafeSplitPoint(lineText);
    }

    private static bool IsSafeSplitPoint(string lineText)
    {
        if (string.IsNullOrWhiteSpace(lineText))
            return true;

        string trimmed = lineText.TrimStart();
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
            return true;

        if (TryExtractOrderedNumber(trimmed).HasValue)
            return true;

        if (trimmed.StartsWith("#"))
            return true;

        return false;
    }

    private static void EmitSlice(
        string markdown,
        List<LineSpan> lines,
        ChunkScanState state,
        int currentLineIndex,
        ImmutableList<TextChunkSlice>.Builder slices)
    {
        LineSpan firstLine = lines[state.SliceStartLineIndex];
        LineSpan lastLine = lines[currentLineIndex];

        int startOffset = firstLine.Offset;
        int endOffset = lastLine.Offset + lastLine.Length;
        int textLength = endOffset - startOffset;
        string text = markdown.Substring(startOffset, textLength);

        slices.Add(new TextChunkSlice(
            ChunkIndex: state.NextChunkIndex++,
            StartLine: state.SliceStartLineIndex + 1,
            EndLine: currentLineIndex + 1,
            StartOffset: startOffset,
            EndOffset: endOffset,
            Text: text,
            OrderedListStart: state.CurrentOrderedStart));

        state.SliceStartLineIndex = currentLineIndex + 1;
        state.AccumulatedElements = 0;

        if (state.LastOrderedNumber.HasValue)
            state.CurrentOrderedStart = state.LastOrderedNumber.Value + 1;
        else
            state.CurrentOrderedStart = null;
    }

    private static int? TryExtractOrderedNumber(string lineText)
    {
        string trimmed = lineText.TrimStart();
        int dotIndex = trimmed.IndexOf('.');
        if (dotIndex <= 0 || dotIndex > 9)
            return null;

        string digits = trimmed.Substring(0, dotIndex);
        if (int.TryParse(digits, out int number) && number > 0)
            return number;

        return null;
    }

    private static List<LineSpan> ScanLines(string markdown)
    {
        List<LineSpan> lines = [];
        int lineStart = 0;

        for (int i = 0; i < markdown.Length; i++)
        {
            if (markdown[i] == '\n')
            {
                int length = i - lineStart;
                if (length > 0 && markdown[i - 1] == '\r')
                    length--;

                lines.Add(new LineSpan(lineStart, length));
                lineStart = i + 1;
            }
        }

        if (lineStart < markdown.Length)
            lines.Add(new LineSpan(lineStart, markdown.Length - lineStart));

        return lines;
    }

    private readonly record struct LineSpan(int Offset, int Length);

    private sealed class ChunkScanState
    {
        public bool InFencedCode;
        public string? FenceDelimiter;
        public int? CurrentOrderedStart;
        public int? LastOrderedNumber;
        public int AccumulatedElements;
        public int SliceStartLineIndex;
        public int NextChunkIndex;
    }
}
