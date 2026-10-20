using System;
using System.Collections.Immutable;
using System.Linq;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Estimates visual element heights for preview chunks using typography metrics.
/// </summary>
public static class PreviewLayoutEstimator
{
    private const int AverageCharactersPerLine = 75;

    /// <summary>
    /// Calculates the estimated height of a collection of markdown blocks.
    /// </summary>
    public static double EstimateBlocksHeight(ImmutableList<MarkdownBlock> blocks, PreviewLayoutMetrics metrics)
    {
        double total = 0;

        for (int i = 0; i < blocks.Count; i++)
            total += EstimateBlockHeight(blocks[i], metrics);

        return Math.Max(total, metrics.BodyLineHeight);
    }

    /// <summary>
    /// Calculates the estimated visual height of a single markdown block.
    /// </summary>
    public static double EstimateBlockHeight(MarkdownBlock block, PreviewLayoutMetrics metrics)
    {
        return block switch
        {
            HeadingBlock heading => metrics.CalculateHeadingHeight(heading.Level),
            ParagraphBlock paragraph => EstimateParagraphHeight(paragraph, metrics),
            CodeBlock code => EstimateCodeHeight(code, metrics),
            BulletListBlock bullet => EstimateListHeight(bullet.Items, metrics),
            OrderedListBlock ordered => EstimateListHeight(ordered.Items, metrics),
            TableBlock table => metrics.CalculateTableHeight(table.Rows.Count),
            QuoteBlock quote => EstimateBlocksHeight(quote.Blocks, metrics) + 16,
            ThematicBreakBlock => PreviewLayoutMetrics.ThematicBreakTotalHeight,
            HtmlBlock html => EstimateHtmlHeight(html, metrics),
            _ => metrics.BodyLineHeight
        };
    }

    private static double EstimateParagraphHeight(ParagraphBlock paragraph, PreviewLayoutMetrics metrics)
    {
        int characterCount = CountInlineCharacters(paragraph.Inlines);
        int hardLineBreakCount = CountHardBreaks(paragraph.Inlines);
        int wrappedLines = (int)Math.Ceiling(characterCount / (double)AverageCharactersPerLine);
        int totalLines = Math.Max(1, Math.Max(wrappedLines, hardLineBreakCount + 1));

        return metrics.CalculateParagraphHeight(totalLines);
    }

    private static double EstimateCodeHeight(CodeBlock code, PreviewLayoutMetrics metrics)
    {
        int lines = CountNewlines(code.Code) + 1;
        return metrics.CalculateCodeBlockHeight(lines);
    }

    private static double EstimateListHeight(ImmutableList<ListItemBlock> items, PreviewLayoutMetrics metrics)
    {
        double total = 0;

        for (int i = 0; i < items.Count; i++)
            total += EstimateListItemHeight(items[i], metrics);

        return total;
    }

    private static double EstimateListItemHeight(ListItemBlock item, PreviewLayoutMetrics metrics)
    {
        if (item.Blocks.Count == 0)
            return metrics.CalculateListItemHeight(1);

        double childHeight = 0;
        for (int i = 0; i < item.Blocks.Count; i++)
            childHeight += EstimateBlockHeight(item.Blocks[i], metrics);

        return Math.Max(childHeight, metrics.CalculateListItemHeight(1));
    }

    private static double EstimateHtmlHeight(HtmlBlock html, PreviewLayoutMetrics metrics)
    {
        int lines = CountNewlines(html.Html) + 1;
        return metrics.CalculateHtmlBlockHeight(lines);
    }

    private static int CountInlineCharacters(ImmutableList<MarkdownInline> inlines)
    {
        int total = 0;

        for (int i = 0; i < inlines.Count; i++)
            total += GetInlineLength(inlines[i]);

        return total;
    }

    private static int GetInlineLength(MarkdownInline inline)
    {
        return inline switch
        {
            TextRun run => run.Text.Length,
            CodeSpan code => code.Code.Length,
            LinkSpan link => CountInlineCharacters(link.Children),
            BoldSpan bold => CountInlineCharacters(bold.Children),
            ItalicSpan italic => CountInlineCharacters(italic.Children),
            StrikethroughSpan strike => CountInlineCharacters(strike.Children),
            GroupSpan group => CountInlineCharacters(group.Children),
            _ => 0
        };
    }

    private static int CountHardBreaks(ImmutableList<MarkdownInline> inlines)
    {
        int count = 0;

        for (int i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is HardLineBreak)
                count++;
        }

        return count;
    }

    private static int CountNewlines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                count++;
        }

        return count;
    }
}
