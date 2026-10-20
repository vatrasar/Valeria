using System;
using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Valeria.Src.Features.Editor.Domain.Services;

internal sealed class ThemeResources
{
    private const string PrimaryTextBrushKey = "PrimaryTextBrush";
    private const string MutedTextBrushKey = "MutedTextBrush";
    private const string HeadingBrushKey = "HeadingBrush";
    private const string AccentBrushKey = "AccentBrush";
    private const string AccentHoverBrushKey = "AccentHoverBrush";
    private const string CodeInlineBackgroundKey = "CodeInlineBackgroundBrush";
    private const string CodeInlineForegroundKey = "CodeInlineForegroundBrush";
    private const string CodeBlockBackgroundKey = "CodeBlockBackgroundBrush";
    private const string CodeBlockHeaderBackgroundKey = "CodeBlockHeaderBackgroundBrush";
    private const string CodeBlockBorderKey = "CodeBlockBorderBrush";
    private const string QuoteBackgroundKey = "QuoteBackgroundBrush";
    private const string QuoteBorderKey = "QuoteBorderBrush";
    private const string QuoteForegroundKey = "QuoteForegroundBrush";
    private const string TableHeaderBackgroundKey = "TableHeaderBackgroundBrush";
    private const string TableCellBorderKey = "TableCellBorderBrush";
    private const string RuleBrushKey = "RuleBrush";
    private const string BorderBrushKey = "BorderBrush";
    private const string EditorSelectionBrushKey = "EditorSelectionBrush";
    private const string EditorLineNumberBrushKey = "EditorLineNumberBrush";
    private const string ProseFontKey = "ProseFontFamily";
    private const string MonoFontKey = "MonoFontFamily";
    private const string BodyFontSizeKey = "BodyFontSize";
    private const string CodeFontSizeKey = "CodeFontSize";

    public IBrush Body { get; private set; } = Brushes.Gray;
    public IBrush Muted { get; private set; } = Brushes.Gray;
    public IBrush Heading { get; private set; } = Brushes.White;
    public IBrush Accent { get; private set; } = Brushes.SkyBlue;
    public IBrush AccentHover { get; private set; } = new SolidColorBrush(Color.Parse("#72B7E8"));
    public IBrush CodeInlineBackground { get; private set; } = Brushes.DimGray;
    public IBrush CodeInlineForeground { get; private set; } = Brushes.White;
    public IBrush CodeBlockBackground { get; private set; } = Brushes.Black;
    public IBrush CodeBlockHeaderBackground { get; private set; } = Brushes.DimGray;
    public IBrush CodeBlockBorder { get; private set; } = Brushes.DimGray;
    public IBrush QuoteBackground { get; private set; } = Brushes.Transparent;
    public IBrush QuoteBorder { get; private set; } = Brushes.SkyBlue;
    public IBrush QuoteForeground { get; private set; } = Brushes.Gray;
    public IBrush TableHeaderBackground { get; private set; } = Brushes.DimGray;
    public IBrush TableCellBorder { get; private set; } = Brushes.DimGray;
    public IBrush CellBorder { get; private set; } = Brushes.DimGray;
    public IBrush Rule { get; private set; } = Brushes.DimGray;
    public IBrush EditorSelection { get; private set; } = Brushes.DarkBlue;
    public IBrush EditorLineNumbers { get; private set; } = Brushes.Gray;
    public FontFamily ProseFont { get; private set; } = FontFamily.Default;
    public FontFamily MonoFont { get; private set; } = new FontFamily("monospace");
    public double BodyFontSize { get; private set; } = 16;
    public double CodeFontSize { get; private set; } = 13.5;
    public ImmutableList<double> HeadingSizes { get; private set; } =
        ImmutableList.Create(30.0, 24.0, 22.0, 20.0, 18.0, 16.0);

    public double HeadingFontSize(int level)
    {
        return HeadingSizes[Math.Clamp(level, 1, 6) - 1];
    }

    public static ThemeResources Resolve()
    {
        ThemeResources theme = new()
        {
            Body = FindBrush(PrimaryTextBrushKey, Brushes.Gray),
            Muted = FindBrush(MutedTextBrushKey, Brushes.Gray),
            Heading = FindBrush(HeadingBrushKey, Brushes.White),
            Accent = FindBrush(AccentBrushKey, Brushes.SkyBlue),
            AccentHover = FindBrush(AccentHoverBrushKey, new SolidColorBrush(Color.Parse("#72B7E8"))),
            CodeInlineBackground = FindBrush(CodeInlineBackgroundKey, Brushes.DimGray),
            CodeInlineForeground = FindBrush(CodeInlineForegroundKey, Brushes.White),
            CodeBlockBackground = FindBrush(CodeBlockBackgroundKey, Brushes.Black),
            CodeBlockHeaderBackground = FindBrush(CodeBlockHeaderBackgroundKey, Brushes.DimGray),
            CodeBlockBorder = FindBrush(CodeBlockBorderKey, Brushes.DimGray),
            QuoteBackground = FindBrush(QuoteBackgroundKey, Brushes.Transparent),
            QuoteBorder = FindBrush(QuoteBorderKey, Brushes.SkyBlue),
            QuoteForeground = FindBrush(QuoteForegroundKey, Brushes.Gray),
            TableHeaderBackground = FindBrush(TableHeaderBackgroundKey, Brushes.DimGray),
            TableCellBorder = FindBrush(TableCellBorderKey, Brushes.DimGray),
            CellBorder = FindBrush(BorderBrushKey, Brushes.DimGray),
            Rule = FindBrush(RuleBrushKey, Brushes.DimGray),
            EditorSelection = FindBrush(EditorSelectionBrushKey, Brushes.DarkBlue),
            EditorLineNumbers = FindBrush(EditorLineNumberBrushKey, Brushes.Gray),
            ProseFont = FindFont(ProseFontKey, FontFamily.Default),
            MonoFont = FindFont(MonoFontKey, new FontFamily("monospace")),
            BodyFontSize = FindSize(BodyFontSizeKey, 16),
            CodeFontSize = FindSize(CodeFontSizeKey, 13.5)
        };

        theme.HeadingSizes = ImmutableList.Create(
            FindSize("Heading1FontSize", 30),
            FindSize("Heading2FontSize", 24),
            FindSize("Heading3FontSize", 22),
            FindSize("Heading4FontSize", 20),
            FindSize("Heading5FontSize", 18),
            FindSize("Heading6FontSize", 16));

        return theme;
    }

    private static IBrush FindBrush(string key, IBrush fallback)
    {
        if (Application.Current?.TryFindResource(key, out object? value) == true && value is IBrush brush)
            return brush;

        return fallback;
    }

    private static FontFamily FindFont(string key, FontFamily fallback)
    {
        if (Application.Current?.TryFindResource(key, out object? value) == true && value is FontFamily family)
            return family;

        return fallback;
    }

    private static double FindSize(string key, double fallback)
    {
        if (Application.Current?.TryFindResource(key, out object? value) == true && value is double size)
            return size;

        return fallback;
    }
}
