using System;
using Avalonia;
using Valeria.Src.Features.Editor.Domain.Services;

namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// Single source of truth for preview typography ratios, element spacings,
/// and line height calculations derived dynamically from theme font sizes.
/// </summary>
public sealed record PreviewLayoutMetrics
{
    public const double BodyLineHeightRatio = 1.5;
    public const double CodeLineHeightRatio = 1.45;
    public const double HeadingLineHeightRatio = 1.3;

    public const double ParagraphVerticalMargin = 16.0;
    public const double HeadingVerticalMargin = 18.0;
    public const double CodeBlockVerticalPadding = 48.0;
    public const double TableRowHeight = 36.0;
    public const double TableVerticalMargin = 16.0;
    public const double ListItemSpacing = 6.0;
    public const double ThematicBreakTotalHeight = 36.0;

    public double BodyLineHeight { get; init; }

    public double CodeLineHeight { get; init; }

    public int CodeBlockMaxHeight { get; init; }

    public ThemeResources Theme { get; init; }

    public PreviewLayoutMetrics(ThemeResources theme, int codeBlockMaxHeight = 420)
    {
        Theme = theme;
        CodeBlockMaxHeight = codeBlockMaxHeight;
        BodyLineHeight = Math.Ceiling(theme.BodyFontSize * BodyLineHeightRatio);
        CodeLineHeight = Math.Ceiling(theme.CodeFontSize * CodeLineHeightRatio);
    }

    /// <summary>
    /// Creates a default metrics instance using standard fallback typography values.
    /// </summary>
    public static PreviewLayoutMetrics CreateDefault(int codeBlockMaxHeight = 420)
    {
        return new PreviewLayoutMetrics(new ThemeResources(), codeBlockMaxHeight);
    }

    /// <summary>
    /// Calculates the estimated visual height of a paragraph based on estimated line count.
    /// </summary>
    public double CalculateParagraphHeight(int estimatedLineCount)
    {
        int lines = Math.Max(1, estimatedLineCount);
        return (lines * BodyLineHeight) + ParagraphVerticalMargin;
    }

    /// <summary>
    /// Calculates the estimated height of a heading for the given level.
    /// </summary>
    public double CalculateHeadingHeight(int level)
    {
        double fontSize = Theme.HeadingFontSize(level);
        return Math.Ceiling(fontSize * HeadingLineHeightRatio) + HeadingVerticalMargin;
    }

    /// <summary>
    /// Calculates the estimated height of a fenced or indented code block, capped at max height.
    /// </summary>
    public double CalculateCodeBlockHeight(int codeLines)
    {
        int lines = Math.Max(1, codeLines);
        double naturalHeight = (lines * CodeLineHeight) + CodeBlockVerticalPadding;
        return Math.Min(naturalHeight, CodeBlockMaxHeight);
    }

    /// <summary>
    /// Calculates the estimated height of a markdown table.
    /// </summary>
    public double CalculateTableHeight(int rowCount)
    {
        int rows = Math.Max(1, rowCount);
        return (rows * TableRowHeight) + TableVerticalMargin;
    }

    /// <summary>
    /// Calculates the estimated height of a single list item.
    /// </summary>
    public double CalculateListItemHeight(int estimatedLineCount)
    {
        int lines = Math.Max(1, estimatedLineCount);
        return (lines * BodyLineHeight) + ListItemSpacing;
    }

    /// <summary>
    /// Calculates the estimated height of an HTML raw block.
    /// </summary>
    public double CalculateHtmlBlockHeight(int lineCount)
    {
        int lines = Math.Max(1, lineCount);
        return (lines * CodeLineHeight) + ParagraphVerticalMargin;
    }
}
