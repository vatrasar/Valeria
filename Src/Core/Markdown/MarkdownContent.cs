using System.Collections.Immutable;

namespace NewMarkText.Src.Core.Markdown;

/// <summary>
/// UI-agnostic intermediate representation of a parsed markdown document.
/// Produced by <see cref="MarkdownParser"/> and consumed by the preview builder.
/// </summary>
public sealed record MarkdownContent(ImmutableList<MarkdownBlock> Blocks)
{
    public static readonly MarkdownContent Empty = new(ImmutableList<MarkdownBlock>.Empty);
}

/// <summary>Base record of every markdown block element.</summary>
public abstract record MarkdownBlock;

/// <summary>Heading with level 1-6 and formatted inline content.</summary>
public sealed record HeadingBlock(int Level, ImmutableList<MarkdownInline> Inlines) : MarkdownBlock;

/// <summary>Regular paragraph with formatted inline content.</summary>
public sealed record ParagraphBlock(ImmutableList<MarkdownInline> Inlines) : MarkdownBlock;

/// <summary>Fenced or indented code block. Language is the normalized info string or null.</summary>
public sealed record CodeBlock(string? Language, string Code) : MarkdownBlock;

/// <summary>Blockquote wrapping nested blocks.</summary>
public sealed record QuoteBlock(ImmutableList<MarkdownBlock> Blocks) : MarkdownBlock;

/// <summary>Single list item. IsChecked is set only for task list items.</summary>
public sealed record ListItemBlock(ImmutableList<MarkdownBlock> Blocks, bool? IsChecked) : MarkdownBlock;

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

/// <summary>Hard line break inside a paragraph.</summary>
public sealed record HardLineBreak : MarkdownInline;
