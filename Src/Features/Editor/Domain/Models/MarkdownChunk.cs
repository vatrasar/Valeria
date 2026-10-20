using System.Collections.Immutable;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// Represents a virtualized horizontal slice of a markdown document.
/// Contains a subset of blocks, their source line range and estimated height.
/// </summary>
public sealed record MarkdownChunk
{
    public int Index { get; init; }

    public int StartLine { get; init; }

    public int EndLine { get; init; }

    public BlockSourceSpan Span { get; init; }

    public double EstimatedHeight { get; init; }

    public double? ActualMeasuredHeight { get; set; }

    public double EffectiveHeight => ActualMeasuredHeight ?? EstimatedHeight;

    public ImmutableList<MarkdownBlock> Blocks { get; init; } = ImmutableList<MarkdownBlock>.Empty;

    public int ContentHash { get; init; }

    public MarkdownChunk(
        int index,
        int startLine,
        int endLine,
        BlockSourceSpan span,
        double estimatedHeight,
        ImmutableList<MarkdownBlock> blocks,
        int contentHash = 0)
    {
        Index = index;
        StartLine = startLine;
        EndLine = endLine;
        Span = span;
        EstimatedHeight = estimatedHeight;
        Blocks = blocks;
        ContentHash = contentHash;
    }
}
