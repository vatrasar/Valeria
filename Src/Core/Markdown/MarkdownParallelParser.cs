using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Concurrently parses markdown text slices into virtualized chunks using multithreading across CPU cores.
/// </summary>
public static class MarkdownParallelParser
{
    /// <summary>
    /// Parses a markdown document in parallel for large documents or sequentially for small documents.
    /// Returns an immutable list of chunks ready for virtualized rendering.
    /// </summary>
    public static ImmutableList<MarkdownChunk> ParseDocument(
        string? markdown,
        PreviewLayoutMetrics metrics,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(markdown))
            return ImmutableList<MarkdownChunk>.Empty;

        ImmutableList<TextChunkSlice> slices = MarkdownTextChunker.SliceDocument(markdown);
        if (slices.Count == 0)
            return ImmutableList<MarkdownChunk>.Empty;

        if (slices.Count == 1)
            return ProcessSingleSliceDocument(markdown, metrics);

        return ProcessParallelSlices(slices, metrics, cancellationToken);
    }

    /// <summary>
    /// Parses a single text slice into a chunk. Used for instantaneous local re-parsing on edits.
    /// </summary>
    public static MarkdownChunk ParseSlice(TextChunkSlice slice, PreviewLayoutMetrics metrics)
    {
        MarkdownContent content = MarkdownParser.Parse(slice.Text);
        if (content.Blocks.Count == 0)
            return CreateEmptyChunk(slice);

        ImmutableList<MarkdownBlock> shiftedBlocks = ShiftBlocks(content.Blocks, slice);
        BlockSourceSpan chunkSpan = ResolveChunkSpan(shiftedBlocks, slice);
        double estimatedHeight = PreviewLayoutEstimator.EstimateBlocksHeight(shiftedBlocks, metrics);
        int contentHash = slice.Text.GetHashCode(StringComparison.Ordinal);

        return new MarkdownChunk(slice.ChunkIndex, slice.StartLine, slice.EndLine, chunkSpan, estimatedHeight, shiftedBlocks, contentHash);
    }

    private static ImmutableList<MarkdownChunk> ProcessSingleSliceDocument(string markdown, PreviewLayoutMetrics metrics)
    {
        MarkdownContent singleContent = MarkdownParser.Parse(markdown);
        return MarkdownChunkSplitter.Split(singleContent, metrics);
    }

    private static ImmutableList<MarkdownChunk> ProcessParallelSlices(
        ImmutableList<TextChunkSlice> slices,
        PreviewLayoutMetrics metrics,
        CancellationToken cancellationToken)
    {
        MarkdownChunk[] chunks = new MarkdownChunk[slices.Count];

        Parallel.For(0, slices.Count, new ParallelOptions { CancellationToken = cancellationToken }, index =>
        {
            chunks[index] = ParseSlice(slices[index], metrics);
        });

        return ImmutableList.CreateRange(chunks);
    }

    private static MarkdownChunk CreateEmptyChunk(TextChunkSlice slice)
    {
        BlockSourceSpan span = new(slice.StartOffset, slice.EndOffset, slice.StartLine, slice.EndLine);
        int contentHash = slice.Text.GetHashCode(StringComparison.Ordinal);
        return new MarkdownChunk(slice.ChunkIndex, slice.StartLine, slice.EndLine, span, 0.0, ImmutableList<MarkdownBlock>.Empty, contentHash);
    }

    private static ImmutableList<MarkdownBlock> ShiftBlocks(ImmutableList<MarkdownBlock> blocks, TextChunkSlice slice)
    {
        int offsetShift = slice.StartOffset;
        int lineShift = slice.StartLine - 1;

        ImmutableList<MarkdownBlock>.Builder builder = ImmutableList.CreateBuilder<MarkdownBlock>();

        for (int i = 0; i < blocks.Count; i++)
        {
            MarkdownBlock shifted = ShiftBlockSpan(blocks[i], offsetShift, lineShift);
            if (i == 0 && slice.OrderedListStart.HasValue && shifted is OrderedListBlock ordered)
                shifted = new OrderedListBlock(slice.OrderedListStart.Value, ordered.Items) { SourceSpan = shifted.SourceSpan };

            builder.Add(shifted);
        }

        return builder.ToImmutable();
    }

    private static MarkdownBlock ShiftBlockSpan(MarkdownBlock block, int offsetShift, int lineShift)
    {
        BlockSourceSpan shifted = ShiftSpan(block.SourceSpan, offsetShift, lineShift);

        return block switch
        {
            HeadingBlock heading => heading with { SourceSpan = shifted },
            ParagraphBlock paragraph => paragraph with { SourceSpan = shifted },
            CodeBlock code => code with { SourceSpan = shifted },
            QuoteBlock quote => quote with
            {
                SourceSpan = shifted,
                Blocks = quote.Blocks.Select(b => ShiftBlockSpan(b, offsetShift, lineShift)).ToImmutableList()
            },
            BulletListBlock bullet => bullet with
            {
                SourceSpan = shifted,
                Items = bullet.Items.Select(item => (ListItemBlock)ShiftBlockSpan(item, offsetShift, lineShift)).ToImmutableList()
            },
            OrderedListBlock ordered => ordered with
            {
                SourceSpan = shifted,
                Items = ordered.Items.Select(item => (ListItemBlock)ShiftBlockSpan(item, offsetShift, lineShift)).ToImmutableList()
            },
            ListItemBlock item => item with
            {
                SourceSpan = shifted,
                Blocks = item.Blocks.Select(b => ShiftBlockSpan(b, offsetShift, lineShift)).ToImmutableList()
            },
            TableBlock table => table with { SourceSpan = shifted },
            ThematicBreakBlock hr => hr with { SourceSpan = shifted },
            HtmlBlock html => html with { SourceSpan = shifted },
            _ => block with { SourceSpan = shifted }
        };
    }

    private static BlockSourceSpan ShiftSpan(BlockSourceSpan span, int offsetShift, int lineShift)
    {
        if (span.IsEmpty)
            return span;

        return new BlockSourceSpan(
            span.StartOffset + offsetShift,
            span.EndOffset + offsetShift,
            span.StartLine + lineShift,
            span.EndLine + lineShift);
    }

    private static BlockSourceSpan ResolveChunkSpan(ImmutableList<MarkdownBlock> blocks, TextChunkSlice slice)
    {
        int startOffset = blocks[0].SourceSpan.StartOffset;
        int endOffset = blocks[^1].SourceSpan.EndOffset;
        return new BlockSourceSpan(startOffset, endOffset, slice.StartLine, slice.EndLine);
    }
}
