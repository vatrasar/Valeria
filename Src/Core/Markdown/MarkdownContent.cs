using System;
using System.Collections.Immutable;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// UI-agnostic intermediate representation of a parsed markdown document.
/// Produced by <see cref="MarkdownParser"/> and consumed by the preview builder.
/// </summary>
public sealed record MarkdownContent(ImmutableList<MarkdownBlock> Blocks)
{
    public static readonly MarkdownContent Empty = new(ImmutableList<MarkdownBlock>.Empty);
}

/// <summary>Represents a 1-based source line range for a markdown block.</summary>
public readonly record struct BlockLineRange(int StartLine, int EndLine)
{
    public static readonly BlockLineRange Empty = new(0, 0);
    public bool IsEmpty => StartLine <= 0 || EndLine <= 0;
}

/// <summary>
/// Represents the character offset range and 1-based line range of a block in source markdown.
/// Used for precise intra-block synchronization between editor and preview.
/// </summary>
public readonly record struct BlockSourceSpan(int StartOffset, int EndOffset, int StartLine, int EndLine)
{
    public static readonly BlockSourceSpan Empty = new(0, 0, 0, 0);
    public bool IsEmpty => StartOffset < 0 || EndOffset < StartOffset || (StartLine == 0 && EndLine == 0);
    public int Length => Math.Max(0, EndOffset - StartOffset + 1);

    public bool ContainsOffset(int offset) => offset >= StartOffset && offset <= EndOffset;
    public bool ContainsLine(int line) => line >= StartLine && line <= EndLine;

    public static implicit operator BlockLineRange(BlockSourceSpan span) => new(span.StartLine, span.EndLine);
}

/// <summary>Base record of every markdown block element.</summary>
public abstract record MarkdownBlock
{
    public BlockSourceSpan SourceSpan { get; init; } = BlockSourceSpan.Empty;

    public BlockLineRange LineRange
    {
        get => new(SourceSpan.StartLine, SourceSpan.EndLine);
        init => SourceSpan = new BlockSourceSpan(SourceSpan.StartOffset, SourceSpan.EndOffset, value.StartLine, value.EndLine);
    }
}

/// <summary>Heading with level 1-6 and formatted inline content.</summary>
public sealed record HeadingBlock(int Level, ImmutableList<MarkdownInline> Inlines) : MarkdownBlock;

/// <summary>Regular paragraph with formatted inline content.</summary>
public sealed record ParagraphBlock(ImmutableList<MarkdownInline> Inlines) : MarkdownBlock;

/// <summary>Fenced or indented code block. Language is the normalized info string or null.</summary>
public sealed record CodeBlock(string? Language, string Code) : MarkdownBlock;

/// <summary>Blockquote wrapping nested blocks.</summary>
public sealed record QuoteBlock(ImmutableList<MarkdownBlock> Blocks) : MarkdownBlock;

/// <summary>Single list item. IsChecked and TaskIndex are set only for task list items.</summary>
public sealed record ListItemBlock(ImmutableList<MarkdownBlock> Blocks, bool? IsChecked, int? TaskIndex = null) : MarkdownBlock;

/// <summary>Unordered list of items.</summary>
public sealed record BulletListBlock(ImmutableList<ListItemBlock> Items) : MarkdownBlock;

/// <summary>Ordered list of items with a starting number.</summary>
public sealed record OrderedListBlock(int StartNumber, ImmutableList<ListItemBlock> Items) : MarkdownBlock;

/// <summary>Alignment of a table column parsed from the separator row.</summary>
public enum TableColumnAlignment
{
    None,
    Left,
    Center,
    Right
}

/// <summary>Single table cell with formatted inline content.</summary>
public sealed record TableCell(ImmutableList<MarkdownInline> Inlines);

/// <summary>Single table row. Header marks the thead row.</summary>
public sealed record TableRow(ImmutableList<TableCell> Cells, bool IsHeader);

/// <summary>Pipe table with an optional header row, body rows and column alignments.</summary>
public sealed record TableBlock(TableRow? Header, ImmutableList<TableRow> Rows, ImmutableList<TableColumnAlignment> Alignments) : MarkdownBlock;

/// <summary>Horizontal rule.</summary>
public sealed record ThematicBreakBlock : MarkdownBlock;

/// <summary>Raw HTML block kept as plain text for preview.</summary>
public sealed record HtmlBlock(string Html) : MarkdownBlock;

/// <summary>Base record of every markdown inline element.</summary>
public abstract record MarkdownInline;

/// <summary>Plain text run.</summary>
public sealed record TextRun(string Text) : MarkdownInline;

/// <summary>Bold span with nested inlines.</summary>
public sealed record BoldSpan(ImmutableList<MarkdownInline> Children) : MarkdownInline;

/// <summary>Italic span with nested inlines.</summary>
public sealed record ItalicSpan(ImmutableList<MarkdownInline> Children) : MarkdownInline;

/// <summary>Strikethrough span with nested inlines.</summary>
public sealed record StrikethroughSpan(ImmutableList<MarkdownInline> Children) : MarkdownInline;

/// <summary>Inline code span.</summary>
public sealed record CodeSpan(string Code) : MarkdownInline;

/// <summary>Hyperlink with display inlines, target url and optional title.</summary>
public sealed record LinkSpan(ImmutableList<MarkdownInline> Children, string Url, string? Title) : MarkdownInline;

/// <summary>Image with alternative text and source url.</summary>
public sealed record ImageSpan(string AlternativeText, string Url) : MarkdownInline;

/// <summary>Transparent grouping of inlines used when flattening unknown containers.</summary>
public sealed record GroupSpan(ImmutableList<MarkdownInline> Children) : MarkdownInline;

/// <summary>Line break inside a paragraph. Covers hard breaks (two trailing
/// spaces or a backslash) as well as plain single newlines, which render as
/// an actual new line in the preview (WYSIWYG-style soft breaks).</summary>
public sealed record HardLineBreak : MarkdownInline;
