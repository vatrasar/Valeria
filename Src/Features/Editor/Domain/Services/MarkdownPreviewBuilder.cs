using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using Material.Icons;
using Material.Icons.Avalonia;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Renders parsed markdown into dark styled preview controls.
/// Code blocks use TextMate grammars for rich syntax highlighting.
/// Invoked by EditorViewModel on every debounced text change.
/// </summary>
public sealed class MarkdownPreviewBuilder : IMarkdownPreviewBuilder
{
    private const string PrimaryTextBrushKey = "PrimaryTextBrush";
    private const string MutedTextBrushKey = "MutedTextBrush";
    private const string HeadingBrushKey = "HeadingBrush";
    private const string AccentBrushKey = "AccentBrush";
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

    private const string BulletMarker = "•";

    private readonly ICodeSyntaxService _syntax;
    private readonly PreviewOptions _options;

    public MarkdownPreviewBuilder(ICodeSyntaxService syntax, IOptions<AppConfig> config)
    {
        _syntax = syntax;
        _options = config.Value.Preview;
    }

    /// <summary>
    /// Builds one control per top level markdown block.
    /// Used by EditorViewModel preview refresh.
    /// </summary>
    public IReadOnlyList<Control> BuildBlocks(MarkdownContent content)
    {
        ThemeResources theme = ThemeResources.Resolve();
        BrushSet brushes = BrushSet.Default(theme);

        return content.Blocks.Select(block => BuildBlock(block, theme, brushes)).ToList();
    }

    private Control BuildBlock(MarkdownBlock block, ThemeResources theme, BrushSet brushes)
    {
        return block switch
        {
            HeadingBlock heading => BuildHeading(heading, theme, brushes),
            ParagraphBlock paragraph => BuildParagraph(paragraph, theme, brushes, theme.BodyFontSize),
            CodeBlock code => BuildCodeBlock(code, theme),
            QuoteBlock quote => BuildQuote(quote, theme),
            BulletListBlock bullet => BuildBulletList(bullet, theme, brushes),
            OrderedListBlock ordered => BuildOrderedList(ordered, theme, brushes),
            TableBlock table => BuildTable(table, theme, brushes),
            ThematicBreakBlock => BuildRule(theme),
            HtmlBlock html => BuildHtml(html, theme),
            _ => new TextBlock()
        };
    }

    private Control BuildHeading(HeadingBlock heading, ThemeResources theme, BrushSet brushes)
    {
        double fontSize = theme.HeadingFontSize(heading.Level);
        SelectableTextBlock text = CreateBodyText(theme, brushes.Heading, fontSize);
        text.FontWeight = FontWeight.Bold;
        text.Margin = new Thickness(0, 12, 0, 6);

        AppendInlines(heading.Inlines, text.Inlines, theme, brushes, fontSize);

        return text;
    }

    private Control BuildParagraph(ParagraphBlock paragraph, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        SelectableTextBlock text = CreateBodyText(theme, brushes.Body, fontSize);
        text.Margin = new Thickness(0, 8, 0, 8);

        AppendInlines(paragraph.Inlines, text.Inlines, theme, brushes, fontSize);

        return text;
    }

    private static SelectableTextBlock CreateBodyText(ThemeResources theme, IBrush foreground, double fontSize)
    {
        return new SelectableTextBlock
        {
            FontFamily = theme.ProseFont,
            FontSize = fontSize,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private Control BuildCodeBlock(CodeBlock code, ThemeResources theme)
    {
        TextEditor editor = CreateCodeEditor(code, theme);

        Border header = CreateCodeHeader(code, editor, theme);

        Grid layout = new();
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid.SetRow(header, 0);
        Grid.SetRow(editor, 1);
        layout.Children.Add(header);
        layout.Children.Add(editor);

        return new Border
        {
            Background = theme.CodeBlockBackground,
            BorderBrush = theme.CodeBlockBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 8),
            ClipToBounds = true,
            Child = layout
        };
    }

    private TextEditor CreateCodeEditor(CodeBlock code, ThemeResources theme)
    {
        TextEditor editor = new()
        {
            Text = code.Code.TrimEnd('\n'),
            FontFamily = theme.MonoFont,
            FontSize = theme.CodeFontSize,
            Background = Brushes.Transparent,
            Foreground = theme.Body,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8),
            IsReadOnly = true,
            ShowLineNumbers = true,
            WordWrap = false,
            MaxHeight = _options.CodeBlockMaxHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        editor.TextArea.SelectionBrush = theme.EditorSelection;
        editor.LineNumbersForeground = theme.EditorLineNumbers;

        _syntax.ApplyGrammar(editor, code.Language);

        return editor;
    }

    private Border CreateCodeHeader(CodeBlock code, TextEditor editor, ThemeResources theme)
    {
        TextBlock languageLabel = new()
        {
            Text = _syntax.GetLanguageDisplayName(code.Language),
            FontFamily = theme.MonoFont,
            FontSize = 12,
            Foreground = theme.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };

        Button copyButton = new()
        {
            Content = new MaterialIcon { Kind = MaterialIconKind.ContentCopy, Width = 14, Height = 14 },
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };

        ToolTip.SetTip(copyButton, Resources.EditorStrings.CopyCode);
        copyButton.Click += (_, _) => CopyCodeToClipboard(copyButton, code.Code);

        DockPanel headerContent = new() { LastChildFill = true };
        DockPanel.SetDock(copyButton, Dock.Right);
        headerContent.Children.Add(copyButton);
        headerContent.Children.Add(languageLabel);

        return new Border
        {
            Background = theme.CodeBlockHeaderBackground,
            BorderBrush = theme.CodeBlockBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 4, 6, 4),
            Child = headerContent
        };
    }

    private static void CopyCodeToClipboard(Control anchor, string code)
    {
        try
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(anchor);
            _ = topLevel?.Clipboard?.SetTextAsync(code);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Copy code failed: {exception.Message}");
        }
    }

    private Control BuildQuote(QuoteBlock quote, ThemeResources theme)
    {
        BrushSet brushes = BrushSet.Quote(theme);
        StackPanel content = new();

        foreach (MarkdownBlock child in quote.Blocks)
            content.Children.Add(BuildBlock(child, theme, brushes));

        return new Border
        {
            Background = theme.QuoteBackground,
            BorderBrush = theme.QuoteBorder,
            BorderThickness = new Thickness(2, 0, 0, 0),
            CornerRadius = new CornerRadius(0, 4, 4, 0),
            Padding = new Thickness(16, 4, 8, 4),
            Margin = new Thickness(0, 8),
            Child = content
        };
    }

    private Control BuildBulletList(BulletListBlock list, ThemeResources theme, BrushSet brushes)
    {
        StackPanel panel = CreateListPanel();

        foreach (ListItemBlock item in list.Items)
            panel.Children.Add(BuildListItem(item, CreateBulletMarker(theme), theme, brushes));

        return panel;
    }

    private Control BuildOrderedList(OrderedListBlock list, ThemeResources theme, BrushSet brushes)
    {
        StackPanel panel = CreateListPanel();
        int number = list.StartNumber;

        foreach (ListItemBlock item in list.Items)
        {
            panel.Children.Add(BuildListItem(item, CreateNumberMarker(theme, number), theme, brushes));
            number++;
        }

        return panel;
    }

    private static StackPanel CreateListPanel()
    {
        return new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(0, 8, 0, 8)
        };
    }

    private static TextBlock CreateBulletMarker(ThemeResources theme)
    {
        return new TextBlock
        {
            Text = BulletMarker,
            FontFamily = theme.ProseFont,
            FontSize = theme.BodyFontSize,
            Foreground = theme.Muted,
            Margin = new Thickness(0, 8, 0, 0)
        };
    }

    private static TextBlock CreateNumberMarker(ThemeResources theme, int number)
    {
        return new TextBlock
        {
            Text = number + ".",
            FontFamily = theme.ProseFont,
            FontSize = theme.BodyFontSize,
            Foreground = theme.Muted,
            MinWidth = 30,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
    }

    private Control BuildListItem(ListItemBlock item, Control marker, ThemeResources theme, BrushSet brushes)
    {
        if (item.IsChecked.HasValue)
            marker = CreateTaskMarker(item.IsChecked.Value);

        StackPanel content = new();
        content.Children.AddRange(item.Blocks.Select(child => BuildBlock(child, theme, brushes)));

        Grid row = new();
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        Grid.SetColumn(marker, 0);
        Grid.SetColumn(content, 1);
        row.Children.Add(marker);
        row.Children.Add(content);
        content.Margin = new Thickness(10, 0, 0, 0);

        return row;
    }

    private static CheckBox CreateTaskMarker(bool isChecked)
    {
        return new CheckBox
        {
            IsChecked = isChecked,
            IsEnabled = false,
            Focusable = false,
            Margin = new Thickness(0, 10, 0, 0)
        };
    }

    private Control BuildTable(TableBlock table, ThemeResources theme, BrushSet brushes)
    {
        int columnCount = CountTableColumns(table);

        if (columnCount == 0)
            return CreateBodyText(theme, brushes.Body, theme.BodyFontSize);

        Grid grid = new();

        for (int column = 0; column < columnCount; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        int rowIndex = 0;

        if (table.Header is not null)
        {
            AddTableRow(grid, table, table.Header, theme, brushes, columnCount, rowIndex, true);
            rowIndex++;
        }

        foreach (TableRow row in table.Rows)
        {
            AddTableRow(grid, table, row, theme, brushes, columnCount, rowIndex, false);
            rowIndex++;
        }

        Border frame = new()
        {
            BorderBrush = theme.TableCellBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 8),
            ClipToBounds = true,
            Child = grid
        };

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = frame
        };
    }

    private static int CountTableColumns(TableBlock table)
    {
        int headerCells = table.Header?.Cells.Count ?? 0;
        int bodyCells = table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Cells.Count);

        return Math.Max(headerCells, bodyCells);
    }

    private void AddTableRow(Grid grid, TableBlock table, TableRow row, ThemeResources theme, BrushSet brushes, int columnCount, int rowIndex, bool isHeader)
    {
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int column = 0; column < columnCount; column++)
        {
            TableCell cell = column < row.Cells.Count
                ? row.Cells[column]
                : new TableCell(ImmutableList<MarkdownInline>.Empty);

            Border cellControl = CreateTableCell(cell, ResolveColumnAlignment(table, column), theme, brushes, isHeader);
            Grid.SetRow(cellControl, rowIndex);
            Grid.SetColumn(cellControl, column);
            grid.Children.Add(cellControl);
        }
    }

    private Border CreateTableCell(TableCell cell, TableColumnAlignment alignment, ThemeResources theme, BrushSet brushes, bool isHeader)
    {
        SelectableTextBlock text = CreateBodyText(theme, brushes.Body, theme.BodyFontSize);
        text.HorizontalAlignment = MapColumnAlignment(alignment);
        AppendInlines(cell.Inlines, text.Inlines, theme, brushes, theme.BodyFontSize);

        if (isHeader)
            text.FontWeight = FontWeight.Bold;

        return new Border
        {
            Background = isHeader ? theme.TableHeaderBackground : Brushes.Transparent,
            BorderBrush = theme.TableCellBorder,
            BorderThickness = new Thickness(1, 1, 0, 0),
            Padding = new Thickness(13, 6),
            Child = text
        };
    }

    private static TableColumnAlignment ResolveColumnAlignment(TableBlock table, int column)
    {
        if (column < table.Alignments.Count)
            return table.Alignments[column];

        return TableColumnAlignment.None;
    }

    private static HorizontalAlignment MapColumnAlignment(TableColumnAlignment alignment)
    {
        return alignment switch
        {
            TableColumnAlignment.Center => HorizontalAlignment.Center,
            TableColumnAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
    }

    private static Control BuildRule(ThemeResources theme)
    {
        return new Border
        {
            Background = theme.Rule,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 16)
        };
    }

    private static Control BuildHtml(HtmlBlock html, ThemeResources theme)
    {
        return new SelectableTextBlock
        {
            Text = html.Html,
            FontFamily = theme.MonoFont,
            FontSize = theme.CodeFontSize,
            Foreground = theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8)
        };
    }

    private void AppendInlines(ImmutableList<MarkdownInline> inlines, InlineCollection? target, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        if (target is null)
            return;

        foreach (MarkdownInline inline in inlines)
            AppendInline(inline, target, theme, brushes, fontSize);
    }

    private void AppendInline(MarkdownInline inline, InlineCollection target, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        switch (inline)
        {
            case TextRun text:
                target.Add(new Run(text.Text));
                break;
            case BoldSpan bold:
                AppendSpan(target, FontWeight.Bold, null, null, bold.Children, theme, brushes, fontSize);
                break;
            case ItalicSpan italic:
                AppendSpan(target, null, FontStyle.Italic, null, italic.Children, theme, brushes, fontSize);
                break;
            case StrikethroughSpan strike:
                AppendSpan(target, null, null, TextDecorations.Strikethrough, strike.Children, theme, brushes, fontSize);
                break;
            case CodeSpan code:
                target.Add(CreateCodeRun(code.Code, theme, fontSize));
                break;
            case LinkSpan link:
                target.Add(CreateLink(link, theme, brushes, fontSize));
                break;
            case ImageSpan image:
                target.Add(new InlineUIContainer(CreateImagePlaceholder(image, theme)));
                break;
            case GroupSpan group:
                AppendInlines(group.Children, target, theme, brushes, fontSize);
                break;
            case HardLineBreak:
                target.Add(new LineBreak());
                break;
        }
    }

    private void AppendSpan(InlineCollection target, FontWeight? weight, FontStyle? style, TextDecorationCollection? decorations, ImmutableList<MarkdownInline> children, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        Span span = new();

        if (weight.HasValue)
            span.FontWeight = weight.Value;

        if (style.HasValue)
            span.FontStyle = style.Value;

        if (decorations is not null)
            span.TextDecorations = decorations;

        AppendInlines(children, span.Inlines, theme, brushes, fontSize);
        target.Add(span);
    }

    private static Run CreateCodeRun(string code, ThemeResources theme, double fontSize)
    {
        return new Run(code)
        {
            FontFamily = theme.MonoFont,
            FontSize = fontSize * 0.85,
            Background = theme.CodeInlineBackground,
            Foreground = theme.CodeInlineForeground
        };
    }

    private InlineUIContainer CreateLink(LinkSpan link, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        TextBlock label = new()
        {
            FontFamily = theme.ProseFont,
            FontSize = fontSize,
            Foreground = theme.Accent,
            TextDecorations = TextDecorations.Underline,
            TextWrapping = TextWrapping.Wrap
        };

        AppendInlines(link.Children, label.Inlines, theme, brushes, fontSize);

        HyperlinkButton button = new()
        {
            Content = label,
            Padding = new Thickness(0),
            FontSize = fontSize
        };

        string url = link.Url;
        button.Click += (_, _) => OpenUrl(button, url);

        return new InlineUIContainer(button);
    }

    private static void OpenUrl(Control anchor, string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return;

            TopLevel? topLevel = TopLevel.GetTopLevel(anchor);
            _ = topLevel?.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Open url failed: {exception.Message}");
        }
    }

    private static Control CreateImagePlaceholder(ImageSpan image, ThemeResources theme)
    {
        TextBlock caption = new()
        {
            Text = $"{Resources.EditorStrings.ImagePlaceholder}: {image.AlternativeText} ({image.Url})",
            FontFamily = theme.ProseFont,
            FontSize = theme.CodeFontSize,
            Foreground = theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        panel.Children.Add(new MaterialIcon { Kind = MaterialIconKind.ImageOutline, Width = 16, Height = 16 });
        panel.Children.Add(caption);

        return new Border
        {
            BorderBrush = theme.CellBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 4),
            Child = panel
        };
    }

    private sealed record BrushSet(IBrush Body, IBrush Heading)
    {
        public static BrushSet Default(ThemeResources theme)
        {
            return new BrushSet(theme.Body, theme.Heading);
        }

        public static BrushSet Quote(ThemeResources theme)
        {
            return new BrushSet(theme.QuoteForeground, theme.QuoteForeground);
        }
    }

    private sealed class ThemeResources
    {
        public IBrush Body { get; private set; } = Brushes.Gray;
        public IBrush Muted { get; private set; } = Brushes.Gray;
        public IBrush Heading { get; private set; } = Brushes.White;
        public IBrush Accent { get; private set; } = Brushes.SkyBlue;
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
}
