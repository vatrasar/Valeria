using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using MdSyntax = Markdig.Syntax;
using MdInlines = Markdig.Syntax.Inlines;
using MdTables = Markdig.Extensions.Tables;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Parses markdown source into a UI-agnostic <see cref="MarkdownContent"/> tree.
/// Used by the editor preview builder and covered by unit tests.
/// </summary>
public static class MarkdownParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>
    /// Parses markdown source into intermediate content. Never throws for malformed input.
    /// Used by EditorViewModel preview refresh.
    /// </summary>
    public static MarkdownContent Parse(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return MarkdownContent.Empty;

        MdSyntax.MarkdownDocument document = Markdig.Markdown.Parse(markdown, Pipeline);
        int[] lineOffsets = BuildLineOffsets(markdown);

        int taskIndex = 0;

        return new MarkdownContent(ParseBlocks(document, ref taskIndex, lineOffsets, markdown.Length));
    }

    private static ImmutableList<MarkdownBlock> ParseBlocks(MdSyntax.ContainerBlock container, ref int taskIndex, int[] lineOffsets, int markdownLength)
    {
        ImmutableList<MarkdownBlock>.Builder builder = ImmutableList.CreateBuilder<MarkdownBlock>();

        foreach (MdSyntax.Block? block in container)
            foreach (MarkdownBlock parsed in ParseBlock(block, ref taskIndex, lineOffsets, markdownLength))
                builder.Add(parsed);

        return builder.ToImmutable();
    }

    private static ImmutableList<MarkdownBlock> ParseBlock(MdSyntax.Block? block, ref int taskIndex, int[] lineOffsets, int markdownLength)
    {
        return block switch
        {
            MdSyntax.HeadingBlock heading => ImmutableList.Create<MarkdownBlock>(
                new HeadingBlock(NormalizeHeadingLevel(heading.Level), ParseInlines(heading.Inline))
                {
                    SourceSpan = ResolveSourceSpan(heading, lineOffsets, markdownLength)
                }),
            MdSyntax.ParagraphBlock paragraph => ImmutableList.Create<MarkdownBlock>(
                new ParagraphBlock(ParseInlines(paragraph.Inline))
                {
                    SourceSpan = ResolveSourceSpan(paragraph, lineOffsets, markdownLength)
                }),
            MdSyntax.FencedCodeBlock fence => ImmutableList.Create<MarkdownBlock>(
                new CodeBlock(NormalizeLanguage(fence.Info?.ToString()), fence.Lines.ToString())
                {
                    SourceSpan = ResolveSourceSpan(fence, lineOffsets, markdownLength)
                }),
            MdSyntax.CodeBlock code => ImmutableList.Create<MarkdownBlock>(
                new CodeBlock(null, code.Lines.ToString())
                {
                    SourceSpan = ResolveSourceSpan(code, lineOffsets, markdownLength)
                }),
            MdSyntax.QuoteBlock quote => ImmutableList.Create<MarkdownBlock>(
                new QuoteBlock(ParseBlocks(quote, ref taskIndex, lineOffsets, markdownLength))
                {
                    SourceSpan = ResolveSourceSpan(quote, lineOffsets, markdownLength)
                }),
            MdSyntax.ListBlock list => ImmutableList.Create<MarkdownBlock>(ParseList(list, ref taskIndex, lineOffsets, markdownLength)),
            MdTables.Table table => ImmutableList.Create<MarkdownBlock>(ParseTable(table, lineOffsets, markdownLength)),
            MdSyntax.ThematicBreakBlock thematic => ImmutableList.Create<MarkdownBlock>(new ThematicBreakBlock
            {
                SourceSpan = ResolveSourceSpan(thematic, lineOffsets, markdownLength)
            }),
            MdSyntax.HtmlBlock html => ImmutableList.Create<MarkdownBlock>(
                new HtmlBlock(html.Lines.ToString())
                {
                    SourceSpan = ResolveSourceSpan(html, lineOffsets, markdownLength)
                }),
            MdSyntax.LeafBlock leaf => ParseLeafFallback(leaf, lineOffsets, markdownLength),
            MdSyntax.ContainerBlock nested => ParseBlocks(nested, ref taskIndex, lineOffsets, markdownLength),
            _ => ImmutableList<MarkdownBlock>.Empty
        };
    }

    private static int NormalizeHeadingLevel(int level)
    {
        return Math.Clamp(level, 1, 6);
    }

    private static string? NormalizeLanguage(string? info)
    {
        if (string.IsNullOrWhiteSpace(info))
            return null;

        string language = info
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        if (language.Length == 0)
            return null;

        return language.ToLowerInvariant();
    }

    private static ImmutableList<MarkdownBlock> ParseLeafFallback(MdSyntax.LeafBlock leaf, int[] lineOffsets, int markdownLength)
    {
        string text = leaf.Lines.ToString();

        if (string.IsNullOrWhiteSpace(text))
            return ImmutableList<MarkdownBlock>.Empty;

        return ImmutableList.Create<MarkdownBlock>(
            new ParagraphBlock(ImmutableList.Create<MarkdownInline>(new TextRun(text)))
            {
                SourceSpan = ResolveSourceSpan(leaf, lineOffsets, markdownLength)
            });
    }

    private static MarkdownBlock ParseList(MdSyntax.ListBlock list, ref int taskIndex, int[] lineOffsets, int markdownLength)
    {
        ImmutableList<ListItemBlock>.Builder items = ImmutableList.CreateBuilder<ListItemBlock>();

        foreach (MdSyntax.ListItemBlock item in list.OfType<MdSyntax.ListItemBlock>())
            items.Add(ParseListItem(item, ref taskIndex, lineOffsets, markdownLength));

        if (list.IsOrdered)
        {
            return new OrderedListBlock(ParseOrderedStart(list.OrderedStart), items.ToImmutable())
            {
                SourceSpan = ResolveSourceSpan(list, lineOffsets, markdownLength)
            };
        }

        return new BulletListBlock(items.ToImmutable())
        {
            SourceSpan = ResolveSourceSpan(list, lineOffsets, markdownLength)
        };
    }

    private static int ParseOrderedStart(string? orderedStart)
    {
        if (int.TryParse(orderedStart, out int start))
            return Math.Max(1, start);

        return 1;
    }

    private static ListItemBlock ParseListItem(MdSyntax.ListItemBlock item, ref int taskIndex, int[] lineOffsets, int markdownLength)
    {
        bool? isChecked = null;
        int? taskIndexForItem = null;
        ImmutableList<MarkdownBlock>.Builder blocks = ImmutableList.CreateBuilder<MarkdownBlock>();

        foreach (MdSyntax.Block? child in item)
            foreach (MarkdownBlock parsed in ParseListItemChild(child, ref isChecked, ref taskIndexForItem, ref taskIndex, lineOffsets, markdownLength))
                blocks.Add(parsed);

        return new ListItemBlock(blocks.ToImmutable(), isChecked, taskIndexForItem)
        {
            SourceSpan = ResolveSourceSpan(item, lineOffsets, markdownLength)
        };
    }

    private static ImmutableList<MarkdownBlock> ParseListItemChild(
        MdSyntax.Block? child,
        ref bool? isChecked,
        ref int? taskIndexForItem,
        ref int taskIndex,
        int[] lineOffsets,
        int markdownLength)
    {
        if (child is MdSyntax.ParagraphBlock paragraph)
        {
            return ImmutableList.Create<MarkdownBlock>(
                new ParagraphBlock(ParseItemParagraphInlines(paragraph, ref isChecked, ref taskIndexForItem, ref taskIndex))
                {
                    SourceSpan = ResolveSourceSpan(paragraph, lineOffsets, markdownLength)
                });
        }

        return ParseBlock(child, ref taskIndex, lineOffsets, markdownLength);
    }

    private static ImmutableList<MarkdownInline> ParseItemParagraphInlines(
        MdSyntax.ParagraphBlock paragraph,
        ref bool? isChecked,
        ref int? taskIndexForItem,
        ref int taskIndex)
    {
        if (paragraph.Inline?.FirstChild is TaskList task)
        {
            isChecked = task.Checked;
            taskIndexForItem ??= taskIndex++;

            return TrimLeadingSpace(ParseInlines(paragraph.Inline));
        }

        return ParseInlines(paragraph.Inline);
    }

    private static ImmutableList<MarkdownInline> TrimLeadingSpace(ImmutableList<MarkdownInline> inlines)
    {
        if (inlines.Count == 0 || inlines[0] is not TextRun first)
            return inlines;

        string trimmed = first.Text.TrimStart();

        if (trimmed.Length == 0)
            return inlines.RemoveAt(0);

        return inlines.SetItem(0, first with { Text = trimmed });
    }

    private static TableBlock ParseTable(MdTables.Table table, int[] lineOffsets, int markdownLength)
    {
        ImmutableList<TableColumnAlignment> alignments = ParseColumnAlignments(table);
        ImmutableList<TableRow>.Builder rows = ImmutableList.CreateBuilder<TableRow>();
        TableRow? header = null;

        foreach (MdTables.TableRow row in table.OfType<MdTables.TableRow>())
        {
            TableRow parsed = ParseTableRow(row);

            if (row.IsHeader && header is null)
                header = parsed;
            else
                rows.Add(parsed);
        }

        return new TableBlock(header, rows.ToImmutable(), alignments)
        {
            SourceSpan = ResolveSourceSpan(table, lineOffsets, markdownLength)
        };
    }

    private static ImmutableList<TableColumnAlignment> ParseColumnAlignments(MdTables.Table table)
    {
        ImmutableList<TableColumnAlignment>.Builder builder = ImmutableList.CreateBuilder<TableColumnAlignment>();

        foreach (MdTables.TableColumnDefinition definition in table.ColumnDefinitions)
            builder.Add(MapColumnAlignment(definition.Alignment));

        return builder.ToImmutable();
    }

    private static TableColumnAlignment MapColumnAlignment(MdTables.TableColumnAlign? alignment)
    {
        return alignment switch
        {
            MdTables.TableColumnAlign.Left => TableColumnAlignment.Left,
            MdTables.TableColumnAlign.Center => TableColumnAlignment.Center,
            MdTables.TableColumnAlign.Right => TableColumnAlignment.Right,
            _ => TableColumnAlignment.None
        };
    }

    private static TableRow ParseTableRow(MdTables.TableRow row)
    {
        ImmutableList<TableCell>.Builder cells = ImmutableList.CreateBuilder<TableCell>();

        foreach (MdTables.TableCell cell in row.OfType<MdTables.TableCell>())
            cells.Add(new TableCell(ParseCellInlines(cell)));

        return new TableRow(cells.ToImmutable(), row.IsHeader);
    }

    private static ImmutableList<MarkdownInline> ParseCellInlines(MdTables.TableCell cell)
    {
        ImmutableList<MarkdownInline>.Builder builder = ImmutableList.CreateBuilder<MarkdownInline>();

        foreach (MdSyntax.Block? child in cell)
            AppendCellChildInlines(child, builder);

        return builder.ToImmutable();
    }

    private static void AppendCellChildInlines(MdSyntax.Block? child, ImmutableList<MarkdownInline>.Builder builder)
    {
        if (child is not MdSyntax.ParagraphBlock paragraph || paragraph.Inline is null)
            return;

        if (builder.Count != 0)
            builder.Add(new TextRun(" "));

        builder.AddRange(ParseInlines(paragraph.Inline));
    }

    private static ImmutableList<MarkdownInline> ParseInlines(MdInlines.ContainerInline? container)
    {
        if (container is null)
            return ImmutableList<MarkdownInline>.Empty;

        ImmutableList<MarkdownInline>.Builder builder = ImmutableList.CreateBuilder<MarkdownInline>();

        foreach (MdInlines.Inline inline in container)
        {
            MarkdownInline? parsed = ParseInline(inline);

            if (parsed is not null)
                builder.Add(parsed);
        }

        return builder.ToImmutable();
    }

    private static MarkdownInline? ParseInline(MdInlines.Inline inline)
    {
        return inline switch
        {
            MdInlines.LiteralInline literal => new TextRun(literal.Content.ToString()),
            MdInlines.EmphasisInline emphasis => ParseEmphasis(emphasis),
            MdInlines.CodeInline code => new CodeSpan(code.Content.ToString()),
            MdInlines.LinkInline link when link.IsImage => ParseImage(link),
            MdInlines.LinkInline link => new LinkSpan(ParseInlines(link), link.Url ?? string.Empty, link.Title),
            MdInlines.AutolinkInline autolink => new LinkSpan(
                ImmutableList.Create<MarkdownInline>(new TextRun(autolink.Url)),
                autolink.Url,
                null),
            MdInlines.LineBreakInline lineBreak => ParseLineBreak(lineBreak),
            MdInlines.HtmlInline html => new TextRun(html.Tag),
            MdInlines.HtmlEntityInline entity => new TextRun(entity.Transcoded.ToString()),
            MathInline math => new CodeSpan(math.Content.ToString()),
            TaskList => null,
            MdInlines.ContainerInline nested => new GroupSpan(ParseInlines(nested)),
            _ => null
        };
    }

    private static MarkdownInline ParseEmphasis(MdInlines.EmphasisInline emphasis)
    {
        ImmutableList<MarkdownInline> children = ParseInlines(emphasis);

        if (emphasis.DelimiterChar == '~')
            return new StrikethroughSpan(children);

        if (emphasis.DelimiterCount >= 2)
            return new BoldSpan(children);

        return new ItalicSpan(children);
    }

    private static MarkdownInline ParseImage(MdInlines.LinkInline link)
    {
        string alternativeText = ExtractPlainText(link);

        return new ImageSpan(alternativeText, link.Url ?? string.Empty);
    }

    private static MarkdownInline ParseLineBreak(MdInlines.LineBreakInline lineBreak)
    {
        return new HardLineBreak();
    }

    private static string ExtractPlainText(MdInlines.ContainerInline container)
    {
        return string.Concat(container
            .Descendants<MdInlines.LiteralInline>()
            .Select(literal => literal.Content.ToString()));
    }

    private static BlockSourceSpan ResolveSourceSpan(MdSyntax.Block? block, int[] lineOffsets, int markdownLength)
    {
        if (block is null || markdownLength <= 0)
            return BlockSourceSpan.Empty;

        int startOffset = block.Span.Start >= 0
            ? Math.Clamp(block.Span.Start, 0, markdownLength - 1)
            : ResolveFallbackStartOffset(block.Line, lineOffsets);

        int startLine = GetLineNumber(startOffset, lineOffsets);
        int endLineFromLeaf = ResolveLeafEndLine(block, startLine);
        int endOffsetFromLine = ResolveLineEndOffset(endLineFromLeaf, lineOffsets, markdownLength);

        int rawEndOffset = block.Span.End >= startOffset ? block.Span.End : endOffsetFromLine;
        int endOffset = Math.Max(rawEndOffset, endOffsetFromLine);
        endOffset = Math.Clamp(endOffset, startOffset, markdownLength - 1);

        int endLine = Math.Max(startLine, GetLineNumber(endOffset, lineOffsets));

        return new BlockSourceSpan(startOffset, endOffset, startLine, endLine);
    }

    private static int ResolveFallbackStartOffset(int lineZeroBased, int[] lineOffsets)
    {
        if (lineZeroBased < 0 || lineOffsets.Length == 0)
            return 0;

        int index = Math.Clamp(lineZeroBased, 0, lineOffsets.Length - 1);
        return lineOffsets[index];
    }

    private static int ResolveLineEndOffset(int lineOneBased, int[] lineOffsets, int markdownLength)
    {
        if (lineOffsets.Length == 0 || markdownLength <= 0)
            return 0;

        if (lineOneBased >= lineOffsets.Length)
            return markdownLength - 1;

        int nextLineStart = lineOffsets[lineOneBased];
        return Math.Max(0, nextLineStart - 1);
    }

    private static int ResolveLeafEndLine(MdSyntax.Block block, int startLine)
    {
        if (block is MdSyntax.LeafBlock leaf && leaf.Lines.Count > 0)
            return startLine + leaf.Lines.Count - 1;

        return startLine;
    }

    private static int[] BuildLineOffsets(string markdown)
    {
        List<int> offsets = [0];

        for (int index = 0; index < markdown.Length; index++)
            AppendNewlineOffset(markdown[index], index, offsets);

        return offsets.ToArray();
    }

    private static void AppendNewlineOffset(char character, int index, List<int> offsets)
    {
        if (character == '\n')
            offsets.Add(index + 1);
    }

    private static int GetLineNumber(int offset, int[] lineOffsets)
    {
        if (lineOffsets.Length == 0)
            return 1;

        int clampedOffset = Math.Clamp(offset, 0, int.MaxValue);
        int index = Array.BinarySearch(lineOffsets, clampedOffset);

        if (index >= 0)
            return index + 1;

        int insertionIndex = ~index;
        return Math.Max(1, insertionIndex);
    }
}
