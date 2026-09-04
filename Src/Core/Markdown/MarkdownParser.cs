using System;
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

        return new MarkdownContent(ParseBlocks(document));
    }

    private static ImmutableList<MarkdownBlock> ParseBlocks(MdSyntax.ContainerBlock container)
    {
        ImmutableList<MarkdownBlock>.Builder builder = ImmutableList.CreateBuilder<MarkdownBlock>();

        foreach (MdSyntax.Block? block in container)
            foreach (MarkdownBlock parsed in ParseBlock(block))
                builder.Add(parsed);

        return builder.ToImmutable();
    }

    private static ImmutableList<MarkdownBlock> ParseBlock(MdSyntax.Block? block)
    {
        return block switch
        {
            MdSyntax.HeadingBlock heading => ImmutableList.Create<MarkdownBlock>(
                new HeadingBlock(NormalizeHeadingLevel(heading.Level), ParseInlines(heading.Inline))),
            MdSyntax.ParagraphBlock paragraph => ImmutableList.Create<MarkdownBlock>(
                new ParagraphBlock(ParseInlines(paragraph.Inline))),
            MdSyntax.FencedCodeBlock fence => ImmutableList.Create<MarkdownBlock>(
                new CodeBlock(NormalizeLanguage(fence.Info?.ToString()), fence.Lines.ToString())),
            MdSyntax.CodeBlock code => ImmutableList.Create<MarkdownBlock>(
                new CodeBlock(null, code.Lines.ToString())),
            MdSyntax.QuoteBlock quote => ImmutableList.Create<MarkdownBlock>(
                new QuoteBlock(ParseBlocks(quote))),
            MdSyntax.ListBlock list => ImmutableList.Create<MarkdownBlock>(ParseList(list)),
            MdTables.Table table => ImmutableList.Create<MarkdownBlock>(ParseTable(table)),
            MdSyntax.ThematicBreakBlock => ImmutableList.Create<MarkdownBlock>(new ThematicBreakBlock()),
            MdSyntax.HtmlBlock html => ImmutableList.Create<MarkdownBlock>(
                new HtmlBlock(html.Lines.ToString())),
            MdSyntax.LeafBlock leaf => ParseLeafFallback(leaf),
            MdSyntax.ContainerBlock nested => ParseBlocks(nested),
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

    private static ImmutableList<MarkdownBlock> ParseLeafFallback(MdSyntax.LeafBlock leaf)
    {
        string text = leaf.Lines.ToString();

        if (string.IsNullOrWhiteSpace(text))
            return ImmutableList<MarkdownBlock>.Empty;

        return ImmutableList.Create<MarkdownBlock>(
            new ParagraphBlock(ImmutableList.Create<MarkdownInline>(new TextRun(text))));
    }

    private static MarkdownBlock ParseList(MdSyntax.ListBlock list)
    {
        ImmutableList<ListItemBlock>.Builder items = ImmutableList.CreateBuilder<ListItemBlock>();

        foreach (MdSyntax.ListItemBlock item in list.OfType<MdSyntax.ListItemBlock>())
            items.Add(ParseListItem(item));

        if (list.IsOrdered)
            return new OrderedListBlock(ParseOrderedStart(list.OrderedStart), items.ToImmutable());

        return new BulletListBlock(items.ToImmutable());
    }

    private static int ParseOrderedStart(string? orderedStart)
    {
        if (int.TryParse(orderedStart, out int start))
            return Math.Max(1, start);

        return 1;
    }

    private static ListItemBlock ParseListItem(MdSyntax.ListItemBlock item)
    {
        bool? isChecked = null;
        ImmutableList<MarkdownBlock>.Builder blocks = ImmutableList.CreateBuilder<MarkdownBlock>();

        foreach (MdSyntax.Block? child in item)
            foreach (MarkdownBlock parsed in ParseListItemChild(child, ref isChecked))
                blocks.Add(parsed);

        return new ListItemBlock(blocks.ToImmutable(), isChecked);
    }

    private static ImmutableList<MarkdownBlock> ParseListItemChild(MdSyntax.Block? child, ref bool? isChecked)
    {
        if (child is MdSyntax.ParagraphBlock paragraph)
            return ImmutableList.Create<MarkdownBlock>(
                new ParagraphBlock(ParseItemParagraphInlines(paragraph, ref isChecked)));

        return ParseBlock(child);
    }

    private static ImmutableList<MarkdownInline> ParseItemParagraphInlines(MdSyntax.ParagraphBlock paragraph, ref bool? isChecked)
    {
        if (paragraph.Inline?.FirstChild is TaskList task)
        {
            isChecked = task.Checked;

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

    private static TableBlock ParseTable(MdTables.Table table)
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

        return new TableBlock(header, rows.ToImmutable(), alignments);
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
        if (lineBreak.IsHard)
            return new HardLineBreak();

        return new TextRun(" ");
    }

    private static string ExtractPlainText(MdInlines.ContainerInline container)
    {
        return string.Concat(container
            .Descendants<MdInlines.LiteralInline>()
            .Select(literal => literal.Content.ToString()));
    }
}
