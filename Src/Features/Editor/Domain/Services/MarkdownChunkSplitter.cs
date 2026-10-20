using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Splits parsed markdown content into manageable chunks for virtualized rendering.
/// Flattens oversized lists into smaller sub-lists and preserves single-chunk mode for small files.
/// </summary>
public static class MarkdownChunkSplitter
{
    public const int DefaultChunkSize = 40;
    public const int LargeDocumentThreshold = 200;

    /// <summary>
    /// Splits markdown content into chunks based on typography metrics and document size.
    /// Returns a single chunk for documents below the threshold.
    /// </summary>
    public static ImmutableList<MarkdownChunk> Split(MarkdownContent content, PreviewLayoutMetrics metrics)
    {
        if (content.Blocks.Count == 0)
            return ImmutableList<MarkdownChunk>.Empty;

        List<MarkdownBlock> normalizedBlocks = NormalizeBlocks(content.Blocks);
        int totalElements = CountTotalElements(content.Blocks);

        if (totalElements < LargeDocumentThreshold)
            return CreateSingleChunk(normalizedBlocks, metrics);

        return CreateMultipleChunks(normalizedBlocks, metrics);
    }

    private static List<MarkdownBlock> NormalizeBlocks(ImmutableList<MarkdownBlock> blocks)
    {
        List<MarkdownBlock> result = new(blocks.Count);

        for (int i = 0; i < blocks.Count; i++)
        {
            MarkdownBlock block = blocks[i];

            if (block is BulletListBlock bullet && bullet.Items.Count > DefaultChunkSize)
            {
                result.AddRange(SplitBulletList(bullet));
                continue;
            }

            if (block is OrderedListBlock ordered && ordered.Items.Count > DefaultChunkSize)
            {
                result.AddRange(SplitOrderedList(ordered));
                continue;
            }

            result.Add(block);
        }

        return result;
    }

    private static IEnumerable<BulletListBlock> SplitBulletList(BulletListBlock list)
    {
        for (int start = 0; start < list.Items.Count; start += DefaultChunkSize)
        {
            int count = Math.Min(DefaultChunkSize, list.Items.Count - start);
            ImmutableList<ListItemBlock> slice = list.Items.GetRange(start, count);
            BlockSourceSpan span = ResolveSliceSpan(slice);

            yield return new BulletListBlock(slice)
            {
                SourceSpan = span
            };
        }
    }

    private static IEnumerable<OrderedListBlock> SplitOrderedList(OrderedListBlock list)
    {
        for (int start = 0; start < list.Items.Count; start += DefaultChunkSize)
        {
            int count = Math.Min(DefaultChunkSize, list.Items.Count - start);
            ImmutableList<ListItemBlock> slice = list.Items.GetRange(start, count);
            BlockSourceSpan span = ResolveSliceSpan(slice);

            yield return new OrderedListBlock(list.StartNumber + start, slice)
            {
                SourceSpan = span
            };
        }
    }

    private static BlockSourceSpan ResolveSliceSpan(ImmutableList<ListItemBlock> slice)
    {
        if (slice.Count == 0)
            return BlockSourceSpan.Empty;

        ListItemBlock first = slice[0];
        ListItemBlock last = slice[^1];

        int startOffset = first.SourceSpan.StartOffset;
        int endOffset = last.SourceSpan.EndOffset;
        int startLine = first.SourceSpan.StartLine;
        int endLine = last.SourceSpan.EndLine;

        return new BlockSourceSpan(startOffset, endOffset, startLine, endLine);
    }

    private static ImmutableList<MarkdownChunk> CreateSingleChunk(List<MarkdownBlock> blocks, PreviewLayoutMetrics metrics)
    {
        ImmutableList<MarkdownBlock> immutableBlocks = blocks.ToImmutableList();
        int startLine = blocks.First().SourceSpan.StartLine;
        int endLine = blocks.Last().SourceSpan.EndLine;
        BlockSourceSpan span = new(
            blocks.First().SourceSpan.StartOffset,
            blocks.Last().SourceSpan.EndOffset,
            startLine,
            endLine);

        double height = PreviewLayoutEstimator.EstimateBlocksHeight(immutableBlocks, metrics);
        MarkdownChunk chunk = new(0, startLine, endLine, span, height, immutableBlocks);

        return ImmutableList.Create(chunk);
    }

    private static ImmutableList<MarkdownChunk> CreateMultipleChunks(List<MarkdownBlock> blocks, PreviewLayoutMetrics metrics)
    {
        ImmutableList<MarkdownChunk>.Builder chunks = ImmutableList.CreateBuilder<MarkdownChunk>();
        int chunkIndex = 0;
        List<MarkdownBlock> currentSlice = [];
        int currentCount = 0;

        for (int i = 0; i < blocks.Count; i++)
        {
            MarkdownBlock block = blocks[i];
            int count = CountElements(block);

            if (currentSlice.Count > 0 && currentCount + count > DefaultChunkSize)
            {
                chunks.Add(BuildChunk(chunkIndex++, currentSlice, metrics));
                currentSlice = [];
                currentCount = 0;
            }

            currentSlice.Add(block);
            currentCount += count;
        }

        if (currentSlice.Count > 0)
            chunks.Add(BuildChunk(chunkIndex++, currentSlice, metrics));

        return chunks.ToImmutable();
    }

    private static int CountTotalElements(ImmutableList<MarkdownBlock> blocks)
    {
        int total = 0;
        for (int i = 0; i < blocks.Count; i++)
            total += CountElements(blocks[i]);

        return total;
    }

    private static int CountElements(MarkdownBlock block)
    {
        return block switch
        {
            BulletListBlock bullet => bullet.Items.Count,
            OrderedListBlock ordered => ordered.Items.Count,
            _ => 1
        };
    }

    private static MarkdownChunk BuildChunk(int index, List<MarkdownBlock> blocks, PreviewLayoutMetrics metrics)
    {
        ImmutableList<MarkdownBlock> immutableBlocks = blocks.ToImmutableList();
        int startLine = blocks.First().SourceSpan.StartLine;
        int endLine = blocks.Last().SourceSpan.EndLine;
        BlockSourceSpan span = new(
            blocks.First().SourceSpan.StartOffset,
            blocks.Last().SourceSpan.EndOffset,
            startLine,
            endLine);

        double height = PreviewLayoutEstimator.EstimateBlocksHeight(immutableBlocks, metrics);

        return new MarkdownChunk(index, startLine, endLine, span, height, immutableBlocks);
    }
}
