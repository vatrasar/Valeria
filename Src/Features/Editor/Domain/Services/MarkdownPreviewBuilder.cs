using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
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
    private const string BulletMarker = "•";

    private readonly ICodeSyntaxService _syntax;
    private readonly IMarkdownImageLoader _imageLoader;
    private readonly PreviewOptions _options;
    private readonly MarkdownImageRenderer _imageRenderer;
    private readonly MarkdownInlineRenderer _inlineRenderer;
    private readonly MarkdownCodeBlockRenderer _codeBlockRenderer;
    private readonly MarkdownTableRenderer _tableRenderer;

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

        _imageRenderer = new MarkdownImageRenderer(_imageLoader, _options);
        _inlineRenderer = new MarkdownInlineRenderer(_imageRenderer);
        _codeBlockRenderer = new MarkdownCodeBlockRenderer(_syntax, _options);
        _tableRenderer = new MarkdownTableRenderer(_inlineRenderer);
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

        ImmutableList<MarkdownBlock> blocksToRender = EnsureNumbered(content.Blocks);
        IReadOnlyList<Control> blocks = blocksToRender.Select(block => BuildBlock(block, theme, brushes, onTaskToggled)).ToList();

        return new PreviewBuildResult(blocks, _pendingTargets.ToImmutableList());
    }

    /// <summary>
    /// Synchronously builds preview controls for a single markdown chunk.
    /// Code blocks are collected as targets for background syntax highlighting.
    /// Used by EditorPreviewCoordinator for chunk-level virtualization.
    /// </summary>
    public PreviewBuildResult BuildChunkBlocks(
        MarkdownChunk chunk,
        Action<int, bool>? onTaskToggled = null,
        string? baseDirectory = null)
    {
        ThemeResources theme = ThemeResources.Resolve();
        BrushSet brushes = BrushSet.Default(theme);
        _pendingTargets = [];
        _currentBaseDirectory = baseDirectory;

        ImmutableList<MarkdownBlock> blocksToRender = EnsureNumbered(chunk.Blocks);
        IReadOnlyList<Control> blocks = blocksToRender.Select(block => BuildBlock(block, theme, brushes, onTaskToggled)).ToList();

        return new PreviewBuildResult(blocks, _pendingTargets.ToImmutableList());
    }

    /// <summary>
    /// Asynchronously builds all preview blocks in batches, yielding control to the UI thread
    /// so user input like scrolling the editor remains responsive on large documents.
    /// Code blocks are created as plain selectable text and collected for asynchronous highlighting.
    /// Used by EditorViewModel preview refresh.
    /// </summary>
    public async Task<PreviewBuildResult> BuildBlocksAsync(
        MarkdownContent content,
        Action<int, bool>? onTaskToggled = null,
        string? baseDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ThemeResources theme = ThemeResources.Resolve();
        BrushSet brushes = BrushSet.Default(theme);
        _pendingTargets = [];
        _currentBaseDirectory = baseDirectory;

        ImmutableList<MarkdownBlock> blocksToRender = EnsureNumbered(content.Blocks);
        const int batchSize = 40;
        List<Control> blocks = new(blocksToRender.Count);

        for (int index = 0; index < blocksToRender.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
                return new PreviewBuildResult([], ImmutableList<CodeHighlightTarget>.Empty);

            blocks.Add(BuildBlock(blocksToRender[index], theme, brushes, onTaskToggled));

            if (ShouldYield(index, blocksToRender.Count, batchSize))
                await YieldToDispatcherAsync();
        }

        return new PreviewBuildResult(blocks, _pendingTargets.ToImmutableList());
    }

    /// <summary>
    /// Applies syntax highlighting to a code block target.
    /// Used by EditorViewModel after asynchronous tokenization completes.
    /// </summary>
    public void ApplyHighlight(CodeHighlightTarget target, IReadOnlyList<HighlightedLine> lines)
    {
        _codeBlockRenderer.ApplyHighlight(target, lines);
    }

    private static bool ShouldYield(int index, int totalCount, int batchSize)
    {
        return (index + 1) % batchSize == 0 && index + 1 < totalCount;
    }

    private static async Task YieldToDispatcherAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    private Control BuildBlock(
        MarkdownBlock block,
        ThemeResources theme,
        BrushSet brushes,
        Action<int, bool>? onTaskToggled)
    {
        Control control = block switch
        {
            HeadingBlock heading => BuildHeading(heading, theme, brushes),
            ParagraphBlock paragraph => BuildParagraph(paragraph, theme, brushes, theme.BodyFontSize),
            CodeBlock code => _codeBlockRenderer.BuildCodeBlock(code, theme, _pendingTargets.Add),
            QuoteBlock quote => BuildQuote(quote, theme, onTaskToggled),
            BulletListBlock bullet => BuildBulletList(bullet, theme, brushes, onTaskToggled),
            OrderedListBlock ordered => BuildOrderedList(ordered, theme, brushes, onTaskToggled),
            TableBlock table => _tableRenderer.BuildTable(table, theme, brushes, _currentBaseDirectory),
            ThematicBreakBlock => BuildRule(theme),
            HtmlBlock html => BuildHtml(html, theme),
            _ => new TextBlock()
        };

        if (!block.SourceSpan.IsEmpty)
            control.Tag = block.SourceSpan;
        else if (!block.LineRange.IsEmpty)
            control.Tag = block.LineRange;

        return control;
    }

    private Control BuildHeading(HeadingBlock heading, ThemeResources theme, BrushSet brushes)
    {
        double fontSize = theme.HeadingFontSize(heading.Level);
        SelectableTextBlock text = CreateBodyText(theme, brushes.Heading, fontSize);
        text.FontWeight = FontWeight.Bold;
        text.Margin = new Thickness(0, 12, 0, 6);

        AppendHeadingInlines(heading, text, theme, brushes, fontSize);
        MarkdownLinkHandler.Attach(text);

        return text;
    }

    private void AppendHeadingInlines(
        HeadingBlock heading,
        SelectableTextBlock text,
        ThemeResources theme,
        BrushSet brushes,
        double fontSize)
    {
        if (!string.IsNullOrEmpty(heading.SectionNumber))
        {
            string numberPrefix = heading.Inlines.Count > 0
                ? $"{heading.SectionNumber} "
                : heading.SectionNumber;
            text.Inlines?.Add(new Run(numberPrefix));
        }

        _inlineRenderer.AppendInlines(heading.Inlines, text.Inlines, theme, brushes, fontSize, _currentBaseDirectory);
    }

    private static ImmutableList<MarkdownBlock> EnsureNumbered(ImmutableList<MarkdownBlock> blocks)
    {
        if (blocks.Count == 0 || HasNumberedHeadings(blocks))
            return blocks;

        return MarkdownHeadingNumberer.ApplyNumbering(blocks);
    }

    private static bool HasNumberedHeadings(ImmutableList<MarkdownBlock> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is HeadingBlock heading && heading.Level >= 2)
                return !string.IsNullOrEmpty(heading.SectionNumber);
        }

        return false;
    }

    private Control BuildParagraph(ParagraphBlock paragraph, ThemeResources theme, BrushSet brushes, double fontSize)
    {
        SelectableTextBlock text = CreateBodyText(theme, brushes.Body, fontSize);
        text.Margin = new Thickness(0, 8, 0, 8);

        _inlineRenderer.AppendInlines(paragraph.Inlines, text.Inlines, theme, brushes, fontSize, _currentBaseDirectory);
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

        if (!item.SourceSpan.IsEmpty)
            row.Tag = item.SourceSpan;
        else if (!item.LineRange.IsEmpty)
            row.Tag = item.LineRange;

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
}
