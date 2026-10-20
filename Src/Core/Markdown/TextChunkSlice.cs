namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Represents a raw text slice of a markdown document partitioned for parallel parsing.
/// </summary>
public sealed record TextChunkSlice(
    int ChunkIndex,
    int StartLine,
    int EndLine,
    int StartOffset,
    int EndOffset,
    string Text,
    int? OrderedListStart);
