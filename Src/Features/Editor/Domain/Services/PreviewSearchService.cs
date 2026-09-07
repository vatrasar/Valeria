using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Service responsible for locating and highlighting text in rendered markdown preview controls.
/// Maintains original styling so highlights can be safely cleared and restored.
/// Invoked by EditorView during search operations.
/// </summary>
public sealed class PreviewSearchService : IPreviewSearchService
{
    private const string SearchHighlightBrushKey = "SearchHighlightBrush";
    private const string SearchHighlightTextBrushKey = "SearchHighlightTextBrush";
    private const string SearchActiveHighlightBrushKey = "SearchActiveHighlightBrush";
    private const string SearchActiveHighlightTextBrushKey = "SearchActiveHighlightTextBrush";

    private readonly List<MatchEntry> _matches = [];
    private readonly Dictionary<SelectableTextBlock, SavedBlockState> _savedStates = [];
    private int _currentIndex = -1;

    /// <summary>
    /// Searches preview blocks for the query and highlights matching text runs.
    /// Used by EditorView search operations.
    /// </summary>
    public PreviewSearchResult Search(IEnumerable<Control> blocks, string query, bool matchCase)
    {
        Clear();

        if (string.IsNullOrEmpty(query))
            return PreviewSearchResult.Empty;

        SearchBrushes brushes = ResolveBrushes();
        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        List<SelectableTextBlock> textBlocks = CollectTextBlocks(blocks);

        foreach (SelectableTextBlock textBlock in textBlocks)
            ProcessTextBlock(textBlock, query, comparison, brushes);

        if (_matches.Count == 0)
            return PreviewSearchResult.Empty;

        _currentIndex = 0;
        ApplyActiveHighlight(_matches[0], brushes);
        BringActiveMatchIntoView(_matches[0]);

        return CreateResult(_matches[0], 1, _matches.Count);
    }

    /// <summary>
    /// Moves the active highlight to the next match and scrolls it into view.
    /// Used by EditorView search operations.
    /// </summary>
    public PreviewSearchResult NavigateNext()
    {
        if (_matches.Count == 0)
            return PreviewSearchResult.Empty;

        SearchBrushes brushes = ResolveBrushes();
        RevertActiveHighlight(_matches[_currentIndex], brushes);

        _currentIndex = (_currentIndex + 1) % _matches.Count;
        ApplyActiveHighlight(_matches[_currentIndex], brushes);
        BringActiveMatchIntoView(_matches[_currentIndex]);

        return CreateResult(_matches[_currentIndex], _currentIndex + 1, _matches.Count);
    }

    /// <summary>
    /// Moves the active highlight to the previous match and scrolls it into view.
    /// Used by EditorView search operations.
    /// </summary>
    public PreviewSearchResult NavigatePrevious()
    {
        if (_matches.Count == 0)
            return PreviewSearchResult.Empty;

        SearchBrushes brushes = ResolveBrushes();
        RevertActiveHighlight(_matches[_currentIndex], brushes);

        _currentIndex = (_currentIndex - 1 + _matches.Count) % _matches.Count;
        ApplyActiveHighlight(_matches[_currentIndex], brushes);
        BringActiveMatchIntoView(_matches[_currentIndex]);

        return CreateResult(_matches[_currentIndex], _currentIndex + 1, _matches.Count);
    }

    /// <summary>
    /// Clears all highlights and restores all preview blocks to their original state.
    /// Used by EditorView search operations.
    /// </summary>
    public void Clear()
    {
        foreach ((SelectableTextBlock textBlock, SavedBlockState state) in _savedStates)
            RestoreBlock(textBlock, state);

        _savedStates.Clear();
        _matches.Clear();
        _currentIndex = -1;
    }

    private static void RestoreBlock(SelectableTextBlock textBlock, SavedBlockState state)
    {
        textBlock.Inlines?.Clear();

        if (state.OriginalInlines is not null)
        {
            foreach (Inline inline in state.OriginalInlines)
                textBlock.Inlines?.Add(inline);
        }
        else if (state.OriginalText is not null)
        {
            textBlock.Text = state.OriginalText;
        }
    }

    private void ProcessTextBlock(SelectableTextBlock textBlock, string query, StringComparison comparison, SearchBrushes brushes)
    {
        if (textBlock.Inlines is not null && textBlock.Inlines.Count > 0)
        {
            ProcessInlinesBlock(textBlock, query, comparison, brushes);
            return;
        }

        if (!string.IsNullOrEmpty(textBlock.Text))
            ProcessPlainTextBlock(textBlock, query, comparison, brushes);
    }

    private void ProcessPlainTextBlock(SelectableTextBlock textBlock, string query, StringComparison comparison, SearchBrushes brushes)
    {
        string? text = textBlock.Text;

        if (string.IsNullOrEmpty(text) || text.IndexOf(query, comparison) < 0)
            return;

        _savedStates[textBlock] = new SavedBlockState(text, null);

        int textOffset = 0;
        int searchStart = 0;
        textBlock.Text = string.Empty;

        while (searchStart < text.Length)
        {
            int index = text.IndexOf(query, searchStart, comparison);

            if (index < 0)
            {
                textBlock.Inlines?.Add(new Run(text[searchStart..]));
                break;
            }

            if (index > searchStart)
            {
                string nonMatch = text[searchStart..index];
                textBlock.Inlines?.Add(new Run(nonMatch));
                textOffset += nonMatch.Length;
            }

            string matchText = text.Substring(index, query.Length);
            Run matchRun = new(matchText)
            {
                Background = brushes.Highlight,
                Foreground = brushes.HighlightText
            };

            textBlock.Inlines?.Add(matchRun);
            _matches.Add(new MatchEntry(textBlock, matchRun, textOffset, matchText.Length, brushes.HighlightText, null));
            textOffset += matchText.Length;
            searchStart = index + query.Length;
        }
    }

    private void ProcessInlinesBlock(SelectableTextBlock textBlock, string query, StringComparison comparison, SearchBrushes brushes)
    {
        if (textBlock.Inlines is null || !ContainsQuery(textBlock.Inlines, query, comparison))
            return;

        List<Inline> originalInlines = textBlock.Inlines.ToList();
        _savedStates[textBlock] = new SavedBlockState(null, originalInlines);
        textBlock.Inlines.Clear();

        int textOffset = 0;

        foreach (Inline inline in originalInlines)
            AppendHighlightedInline(inline, textBlock.Inlines, textBlock, query, comparison, brushes, ref textOffset);
    }

    private static bool ContainsQuery(InlineCollection? inlines, string query, StringComparison comparison)
    {
        if (inlines is null)
            return false;

        foreach (Inline inline in inlines)
        {
            if (inline is Run run && run.Text?.IndexOf(query, comparison) >= 0)
                return true;

            if (inline is Span span && ContainsQuery(span.Inlines, query, comparison))
                return true;
        }

        return false;
    }

    private void AppendHighlightedInline(
        Inline source,
        InlineCollection target,
        SelectableTextBlock textBlock,
        string query,
        StringComparison comparison,
        SearchBrushes brushes,
        ref int textOffset)
    {
        switch (source)
        {
            case Run run:
                AppendHighlightedRun(run, target, textBlock, query, comparison, brushes, ref textOffset);
                break;
            case Span span:
                AppendHighlightedSpan(span, target, textBlock, query, comparison, brushes, ref textOffset);
                break;
            case LineBreak:
                target.Add(new LineBreak());
                textOffset += 1;
                break;
            case InlineUIContainer container:
                target.Add(new InlineUIContainer(container.Child));
                break;
        }
    }

    private void AppendHighlightedRun(
        Run source,
        InlineCollection target,
        SelectableTextBlock textBlock,
        string query,
        StringComparison comparison,
        SearchBrushes brushes,
        ref int textOffset)
    {
        string text = source.Text ?? string.Empty;

        if (string.IsNullOrEmpty(text))
            return;

        int searchStart = 0;

        while (searchStart < text.Length)
        {
            int index = text.IndexOf(query, searchStart, comparison);

            if (index < 0)
            {
                target.Add(CloneRun(source, text[searchStart..], source.Foreground, source.Background));
                textOffset += text.Length - searchStart;
                break;
            }

            if (index > searchStart)
            {
                string nonMatch = text[searchStart..index];
                target.Add(CloneRun(source, nonMatch, source.Foreground, source.Background));
                textOffset += nonMatch.Length;
            }

            string matchText = text.Substring(index, query.Length);
            Run matchRun = CloneRun(source, matchText, brushes.HighlightText, brushes.Highlight);

            target.Add(matchRun);
            _matches.Add(new MatchEntry(textBlock, matchRun, textOffset, matchText.Length, source.Foreground, source.Background));
            textOffset += matchText.Length;
            searchStart = index + query.Length;
        }
    }

    private void AppendHighlightedSpan(
        Span source,
        InlineCollection target,
        SelectableTextBlock textBlock,
        string query,
        StringComparison comparison,
        SearchBrushes brushes,
        ref int textOffset)
    {
        Span span = new()
        {
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            FontWeight = source.FontWeight,
            FontStyle = source.FontStyle,
            Foreground = source.Foreground,
            Background = source.Background,
            TextDecorations = source.TextDecorations
        };

        string? url = MarkdownLink.GetUrl(source);

        if (!string.IsNullOrEmpty(url))
        {
            MarkdownLink.SetUrl(span, url);
            MarkdownLink.SetHoverForeground(span, MarkdownLink.GetHoverForeground(source));
        }

        foreach (Inline child in source.Inlines)
            AppendHighlightedInline(child, span.Inlines, textBlock, query, comparison, brushes, ref textOffset);

        target.Add(span);
    }

    private static Run CloneRun(Run source, string text, IBrush? foreground, IBrush? background)
    {
        return new Run(text)
        {
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            FontWeight = source.FontWeight,
            FontStyle = source.FontStyle,
            Foreground = foreground,
            Background = background,
            TextDecorations = source.TextDecorations
        };
    }

    private static void ApplyActiveHighlight(MatchEntry match, SearchBrushes brushes)
    {
        match.MatchRun.Background = brushes.ActiveHighlight;
        match.MatchRun.Foreground = brushes.ActiveHighlightText;
    }

    private static void RevertActiveHighlight(MatchEntry match, SearchBrushes brushes)
    {
        match.MatchRun.Background = brushes.Highlight;
        match.MatchRun.Foreground = brushes.HighlightText;
    }

    private static void BringActiveMatchIntoView(MatchEntry match)
    {
        match.TextBlock.BringIntoView();

        if (match.TextBlock.TextLayout is not null)
        {
            var hit = match.TextBlock.TextLayout.HitTestTextPosition(match.StartIndex);
            match.TextBlock.BringIntoView(new Rect(hit.Left, hit.Top, Math.Max(hit.Width, 20), Math.Max(hit.Height, 20)));
        }
    }

    private static PreviewSearchResult CreateResult(MatchEntry match, int index, int total)
    {
        return new PreviewSearchResult
        {
            TotalMatches = total,
            CurrentMatchIndex = index,
            ActiveTextBlock = match.TextBlock,
            ActiveStartIndex = match.StartIndex,
            ActiveLength = match.Length
        };
    }

    private static List<SelectableTextBlock> CollectTextBlocks(IEnumerable<Control> blocks)
    {
        List<SelectableTextBlock> result = [];

        foreach (Control block in blocks)
            CollectSelectableTextBlocks(block, result);

        return result;
    }

    private static void CollectSelectableTextBlocks(Control control, List<SelectableTextBlock> result)
    {
        if (control is SelectableTextBlock selectable)
        {
            result.Add(selectable);
            return;
        }

        if (control is Panel panel)
        {
            foreach (Control child in panel.Children)
                CollectSelectableTextBlocks(child, result);
            return;
        }

        if (control is Border border && border.Child is not null)
        {
            CollectSelectableTextBlocks(border.Child, result);
            return;
        }

        if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            CollectSelectableTextBlocks(contentChild, result);
            return;
        }

        if (control is ScrollViewer scrollViewer && scrollViewer.Content is Control scrollChild)
        {
            CollectSelectableTextBlocks(scrollChild, result);
            return;
        }

        if (control is ItemsControl itemsControl && itemsControl.ItemsSource is IEnumerable<Control> items)
        {
            foreach (Control item in items)
                CollectSelectableTextBlocks(item, result);
            return;
        }

        foreach (Control child in control.GetVisualChildren().OfType<Control>())
            CollectSelectableTextBlocks(child, result);

        if (!control.GetVisualChildren().Any() && control is ILogical logical)
        {
            foreach (ILogical child in logical.LogicalChildren)
            {
                if (child is Control logicalChild)
                    CollectSelectableTextBlocks(logicalChild, result);
            }
        }
    }

    private static SearchBrushes ResolveBrushes()
    {
        IBrush highlight = FindBrush(SearchHighlightBrushKey, new SolidColorBrush(Color.FromRgb(0x61, 0x4D, 0x00)));
        IBrush highlightText = FindBrush(SearchHighlightTextBrushKey, new SolidColorBrush(Color.FromRgb(0xFF, 0xE8, 0x85)));
        IBrush active = FindBrush(SearchActiveHighlightBrushKey, new SolidColorBrush(Color.FromRgb(0x9E, 0x56, 0x00)));
        IBrush activeText = FindBrush(SearchActiveHighlightTextBrushKey, Brushes.White);

        return new SearchBrushes(highlight, highlightText, active, activeText);
    }

    private static IBrush FindBrush(string key, IBrush fallback)
    {
        if (Application.Current?.TryFindResource(key, out object? value) == true && value is IBrush brush)
            return brush;

        return fallback;
    }

    private sealed record SearchBrushes(IBrush Highlight, IBrush HighlightText, IBrush ActiveHighlight, IBrush ActiveHighlightText);

    private sealed record SavedBlockState(string? OriginalText, List<Inline>? OriginalInlines);

    private sealed record MatchEntry(
        SelectableTextBlock TextBlock,
        Run MatchRun,
        int StartIndex,
        int Length,
        IBrush? OriginalForeground,
        IBrush? OriginalBackground);
}
