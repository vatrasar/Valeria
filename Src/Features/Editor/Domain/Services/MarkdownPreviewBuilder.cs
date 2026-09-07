using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Renders parsed markdown into dark styled preview controls.
/// Code blocks are rendered as lightweight selectable text and colorized
/// asynchronously by EditorViewModel via <see cref="ApplyHighlight"/>.
/// Invoked by EditorViewModel on every debounced text change.
/// </summary>
public sealed class MarkdownPreviewBuilder : IMarkdownPreviewBuilder
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

    private const string BulletMarker = "•";

    private readonly ICodeSyntaxService _syntax;
    private readonly IMarkdownImageLoader _imageLoader;
    private readonly PreviewOptions _options;
    private List<CodeHighlightTarget> _pendingTargets = [];
    private string? _currentBaseDirectory;

    public MarkdownPreviewBuilder(
        ICodeSyntaxService syntax,
        IOptions<AppConfig> config,
        IMarkdownImageLoader? imageLoader = null)
    {
        _syntax = syntax;
        _options = config.Value.Preview;
        _imageLoader = imageLoader ?? new MarkdownImageLoader();
    }

    /// <summary>
    /// Synchronously builds all preview blocks. Code blocks are created as
    /// plain selectable text and collected for asynchronous highlighting. Task
    /// list checkboxes use the callback to update the source document.
    /// Used by EditorViewModel preview refresh.
    /// </summary>
    public PreviewBuildResult BuildBlocks(
        MarkdownContent content,
        Action<int, bool>? onTaskToggled = null,
        string? baseDirectory = null)
    {
        ThemeResources theme = ThemeResources.Resolve();
        BrushSet brushes = BrushSet.Default(theme);
        _pendingTargets = [];
        _currentBaseDirectory = baseDirectory;

        IReadOnlyList<Control> blocks = content.Blocks.Select(block => BuildBlock(block, theme, brushes, onTaskToggled)).ToList();

        return new PreviewBuildResult(blocks, _pendingTargets.ToImmutableList());
    }

    /// <summary>
    /// Applies pre-tokenized spans onto a previously built code target.
    /// Called on the UI thread once background highlighting finished.
    /// </summary>
    public void ApplyHighlight(CodeHighlightTarget target, IReadOnlyList<HighlightedLine> lines)
    {
        SelectableTextBlock text = target.TextBlock;
        ThemeResources theme = ThemeResources.Resolve();
        InlineCollection? inlines = text.Inlines;

        if (inlines is null)
            return;

        text.Text = string.Empty;
        inlines.Clear();

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            if (lineIndex > 0)
                inlines.Add(new LineBreak());

            FillLineInlines(lines[lineIndex], inlines, theme);
        }
    }

    private void FillLineInlines(HighlightedLine line, InlineCollection target, ThemeResources theme)
    {
        foreach (HighlightedSpan span in line.Spans)
            target.Add(CreateSpanRun(span, theme));
    }

    private static Run CreateSpanRun(HighlightedSpan span, ThemeResources theme)
    {
        return new Run(span.Text)
        {
            FontFamily = theme.MonoFont,
            FontSize = theme.CodeFontSize,
            Foreground = TryCreateBrush(span.ForegroundHex) ?? theme.Body,
            FontWeight = span.IsBold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = span.IsItalic ? FontStyle.Italic : FontStyle.Normal
        };
    }

    private static IBrush? TryCreateBrush(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return null;

        string value = hex.TrimStart('#');

        if (value.Length == 6 && uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

        if (value.Length == 8 && uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            return new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

        return null;
    }

    private Control BuildBlock(
        MarkdownBlock block,
        ThemeResources theme,
        BrushSet brushes,
        Action<int, bool>? onTaskToggled)
    {
        return block switch
        {
            HeadingBlock heading => BuildHeading(heading, theme, brushes),
            ParagraphBlock paragraph => BuildParagraph(paragraph, theme, brushes, theme.BodyFontSize),
            CodeBlock code => BuildCodeBlock(code, theme),
            QuoteBlock quote => BuildQuote(quote, theme, onTaskToggled),
            BulletListBlock bullet => BuildBulletList(bullet, theme, brushes, onTaskToggled),
            OrderedListBlock ordered => BuildOrderedList(ordered, theme, brushes, onTaskToggled),
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
        MarkdownLinkHandler.Attach(text);

        return text;
    }

    private Control BuildParagraph(ParagraphBlock paragraph, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        SelectableTextBlock text = CreateBodyText(theme, brushes.Body, fontSize);
        text.Margin = new Thickness(0, 8, 0, 8);

        AppendInlines(paragraph.Inlines, text.Inlines, theme, brushes, fontSize);
        MarkdownLinkHandler.Attach(text);

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
        SelectableTextBlock text = CreateCodeText(code, theme);
        _pendingTargets.Add(new CodeHighlightTarget(text, code.Language, code.Code.TrimEnd('\n')));

        Border header = CreateCodeHeader(code, theme);

        ScrollViewer scroller = new()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = _options.CodeBlockMaxHeight,
            Content = text
        };

        DockPanel layout = new() { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(scroller);

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

    private static SelectableTextBlock CreateCodeText(CodeBlock code, ThemeResources theme)
    {
        return new SelectableTextBlock
        {
            Text = code.Code.TrimEnd('\n'),
            FontFamily = theme.MonoFont,
            FontSize = theme.CodeFontSize,
            Foreground = theme.Body,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(12, 8, 32, 24)
        };
    }

    private Border CreateCodeHeader(CodeBlock code, ThemeResources theme)
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

    private Control BuildQuote(QuoteBlock quote, ThemeResources theme, Action<int, bool>? onTaskToggled)
    {
        BrushSet brushes = BrushSet.Quote(theme);
        StackPanel content = new();

        foreach (MarkdownBlock child in quote.Blocks)
            content.Children.Add(BuildBlock(child, theme, brushes, onTaskToggled));

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

    private Control BuildBulletList(
        BulletListBlock list,
        ThemeResources theme,
        BrushSet brushes,
        Action<int, bool>? onTaskToggled)
    {
        StackPanel panel = CreateListPanel();

        foreach (ListItemBlock item in list.Items)
            panel.Children.Add(BuildListItem(item, CreateBulletMarker(theme), theme, brushes, onTaskToggled));

        return panel;
    }

    private Control BuildOrderedList(
        OrderedListBlock list,
        ThemeResources theme,
        BrushSet brushes,
        Action<int, bool>? onTaskToggled)
    {
        StackPanel panel = CreateListPanel();
        int number = list.StartNumber;

        foreach (ListItemBlock item in list.Items)
        {
            panel.Children.Add(BuildListItem(item, CreateNumberMarker(theme, number), theme, brushes, onTaskToggled));
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

    private Control BuildListItem(
        ListItemBlock item,
        Control marker,
        ThemeResources theme,
        BrushSet brushes,
        Action<int, bool>? onTaskToggled)
    {
        if (item.IsChecked.HasValue && item.TaskIndex.HasValue)
            marker = CreateTaskMarker(item.IsChecked.Value, item.TaskIndex.Value, onTaskToggled);

        StackPanel content = new();
        content.Children.AddRange(item.Blocks.Select(child => BuildBlock(child, theme, brushes, onTaskToggled)));

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

    private static CheckBox CreateTaskMarker(bool isChecked, int taskIndex, Action<int, bool>? onTaskToggled)
    {
        CheckBox checkBox = new()
        {
            IsChecked = isChecked,
            IsEnabled = onTaskToggled is not null,
            Focusable = onTaskToggled is not null,
            Margin = new Thickness(0, 10, 0, 0)
        };

        if (onTaskToggled is not null)
            checkBox.IsCheckedChanged += (_, _) => onTaskToggled(taskIndex, checkBox.IsChecked == true);

        return checkBox;
    }

    private Control BuildTable(TableBlock table, ThemeResources theme, BrushSet brushes)
    {
        int columnCount = CountTableColumns(table);

        if (columnCount == 0)
            return CreateBodyText(theme, brushes.Body, theme.BodyFontSize);

        List<int> columnLengths = GetColumnLengths(table, columnCount);
        int maxColumnLength = columnLengths.Count > 0 ? columnLengths.Max() : 0;

        Grid grid = new();

        for (int column = 0; column < columnCount; column++)
        {
            int length = column < columnLengths.Count ? columnLengths[column] : 0;
            grid.ColumnDefinitions.Add(CreateTableColumnDefinition(length, maxColumnLength));
        }

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

        return new MarkdownTableScrollViewer
        {
            ColumnCount = columnCount,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = frame
        };
    }

    private static ColumnDefinition CreateTableColumnDefinition(int columnLength, int maxColumnLength)
    {
        if (ShouldBeCompactColumn(columnLength, maxColumnLength))
            return new ColumnDefinition(GridLength.Auto);

        double weight = CalculateStarWeight(columnLength);

        return new ColumnDefinition(new GridLength(weight, GridUnitType.Star));
    }

    private static bool ShouldBeCompactColumn(int columnLength, int maxColumnLength)
    {
        if (columnLength > 16)
            return false;

        if (columnLength == maxColumnLength)
            return false;

        return columnLength <= 8 || columnLength * 2 <= maxColumnLength;
    }

    private static double CalculateStarWeight(int columnLength)
    {
        if (columnLength <= 25)
            return 1.0;

        if (columnLength <= 60)
            return 1.5;

        if (columnLength <= 120)
            return 2.0;

        return 3.0;
    }

    private static List<int> GetColumnLengths(TableBlock table, int columnCount)
    {
        List<int> lengths = new(columnCount);

        for (int column = 0; column < columnCount; column++)
            lengths.Add(GetColumnMaxTextLength(table, column));

        return lengths;
    }

    private static int GetColumnMaxTextLength(TableBlock table, int column)
    {
        int maxLength = 0;

        if (table.Header is not null && column < table.Header.Cells.Count)
            maxLength = Math.Max(maxLength, GetCellTextLength(table.Header.Cells[column]));

        foreach (TableRow row in table.Rows)
        {
            if (column < row.Cells.Count)
                maxLength = Math.Max(maxLength, GetCellTextLength(row.Cells[column]));
        }

        return maxLength;
    }

    private static int GetCellTextLength(TableCell cell)
    {
        int length = 0;

        foreach (MarkdownInline inline in cell.Inlines)
            length += GetInlineTextLength(inline);

        return length;
    }

    private static int GetInlineTextLength(MarkdownInline inline)
    {
        return inline switch
        {
            TextRun text => text.Text.Length,
            CodeSpan code => code.Code.Length,
            LinkSpan link => link.Children.Sum(GetInlineTextLength),
            BoldSpan bold => bold.Children.Sum(GetInlineTextLength),
            ItalicSpan italic => italic.Children.Sum(GetInlineTextLength),
            StrikethroughSpan strike => strike.Children.Sum(GetInlineTextLength),
            GroupSpan group => group.Children.Sum(GetInlineTextLength),
            _ => 0
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
        text.TextAlignment = MapTextAlignment(alignment);
        AppendInlines(cell.Inlines, text.Inlines, theme, brushes, theme.BodyFontSize);
        MarkdownLinkHandler.Attach(text);

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

    private static TextAlignment MapTextAlignment(TableColumnAlignment alignment)
    {
        return alignment switch
        {
            TableColumnAlignment.Center => TextAlignment.Center,
            TableColumnAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
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
                target.Add(new InlineUIContainer(CreateImageControl(image, theme)));
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

    private Span CreateLink(LinkSpan link, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        Span linkSpan = new()
        {
            Foreground = theme.Accent
        };

        MarkdownLink.SetUrl(linkSpan, link.Url);
        MarkdownLink.SetHoverForeground(linkSpan, theme.AccentHover);
        AppendInlines(link.Children, linkSpan.Inlines, theme, brushes, fontSize);

        return linkSpan;
    }

    private Control CreateImageControl(ImageSpan image, ThemeResources theme)
    {
        string? baseDirectory = _currentBaseDirectory;
        Border container = new()
        {
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Margin = new Thickness(0, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        ToolTip.SetTip(container, FormatImageTooltip(image));

        if (_imageLoader.TryGetCached(image.Url, baseDirectory, out Bitmap? cachedBitmap))
        {
            if (cachedBitmap is not null)
            {
                container.Child = CreateImageView(cachedBitmap);
                return container;
            }

            container.Child = CreateBrokenImagePlaceholder(image, theme);
            return container;
        }

        container.Child = CreateLoadingPlaceholder(image, theme);
        InitiateAsyncImageLoad(image, baseDirectory, container, theme);

        return container;
    }

    private void InitiateAsyncImageLoad(
        ImageSpan image,
        string? baseDirectory,
        Border container,
        ThemeResources theme)
    {
        _ = _imageLoader.LoadImageAsync(image.Url, baseDirectory)
            .ContinueWith(task =>
            {
                Bitmap? bitmap = task.IsCompletedSuccessfully ? task.Result : null;
                Dispatcher.UIThread.Post(() =>
                {
                    if (bitmap is not null)
                        container.Child = CreateImageView(bitmap);
                    else
                        container.Child = CreateBrokenImagePlaceholder(image, theme);
                });
            }, TaskScheduler.Default);
    }

    private Image CreateImageView(Bitmap bitmap)
    {
        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            MaxWidth = _options.MaxWidth,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private static Control CreateLoadingPlaceholder(ImageSpan image, ThemeResources theme)
    {
        TextBlock caption = new()
        {
            Text = FormatImageLabel(image),
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
            Child = panel
        };
    }

    private static Control CreateBrokenImagePlaceholder(ImageSpan image, ThemeResources theme)
    {
        TextBlock caption = new()
        {
            Text = FormatImageLabel(image),
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

        panel.Children.Add(new MaterialIcon { Kind = MaterialIconKind.ImageBrokenVariant, Width = 16, Height = 16 });
        panel.Children.Add(caption);

        return new Border
        {
            BorderBrush = theme.CellBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Child = panel
        };
    }

    private static string FormatImageLabel(ImageSpan image)
    {
        if (string.IsNullOrWhiteSpace(image.AlternativeText))
            return $"{Resources.EditorStrings.ImagePlaceholder} ({image.Url})";

        return $"{Resources.EditorStrings.ImagePlaceholder}: {image.AlternativeText} ({image.Url})";
    }

    private static string FormatImageTooltip(ImageSpan image)
    {
        if (string.IsNullOrWhiteSpace(image.AlternativeText))
            return image.Url;

        return $"{image.AlternativeText} ({image.Url})";
    }

    private sealed class MarkdownTableScrollViewer : ScrollViewer
    {
        private const double DefaultMinColumnWidth = 60;
        private const double FallbackWidth = 800;

        public int ColumnCount { get; init; } = 1;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Content is Control child)
                AdjustChildWidth(child, availableSize.Width);

            return base.MeasureOverride(availableSize);
        }

        private void AdjustChildWidth(Control child, double availableWidth)
        {
            double minTableWidth = ColumnCount * DefaultMinColumnWidth;
            double viewportWidth = ResolveViewportWidth(availableWidth, minTableWidth);
            double targetWidth = Math.Max(minTableWidth, viewportWidth);

            if (double.IsNaN(child.Width) || Math.Abs(child.Width - targetWidth) > 0.5)
                child.Width = targetWidth;
        }

        private double ResolveViewportWidth(double availableWidth, double minTableWidth)
        {
            if (!double.IsInfinity(availableWidth))
                return availableWidth;

            if (Bounds.Width > 0)
                return Bounds.Width;

            return minTableWidth > 0 ? minTableWidth : FallbackWidth;
        }
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
}
