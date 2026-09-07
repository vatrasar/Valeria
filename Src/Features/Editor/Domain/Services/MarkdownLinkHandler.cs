using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Attached properties for markdown links within inline collections.
/// </summary>
public static class MarkdownLink
{
    public static readonly AttachedProperty<string?> UrlProperty =
        AvaloniaProperty.RegisterAttached<Inline, string?>("Url", typeof(MarkdownLink));

    public static readonly AttachedProperty<IBrush?> HoverForegroundProperty =
        AvaloniaProperty.RegisterAttached<Inline, IBrush?>("HoverForeground", typeof(MarkdownLink));

    /// <summary>
    /// Gets the destination URL associated with the specified inline element.
    /// Used by MarkdownLinkHandler to resolve navigation targets.
    /// </summary>
    public static string? GetUrl(Inline inline)
    {
        return inline.GetValue(UrlProperty);
    }

    /// <summary>
    /// Sets the destination URL associated with the specified inline element.
    /// Used by MarkdownPreviewBuilder when creating link spans.
    /// </summary>
    public static void SetUrl(Inline inline, string? value)
    {
        inline.SetValue(UrlProperty, value);
    }

    /// <summary>
    /// Gets the hover foreground brush associated with the specified inline element.
    /// Used by MarkdownLinkHandler to update text color when pointer is hovered.
    /// </summary>
    public static IBrush? GetHoverForeground(Inline inline)
    {
        return inline.GetValue(HoverForegroundProperty);
    }

    /// <summary>
    /// Sets the hover foreground brush associated with the specified inline element.
    /// Used by MarkdownPreviewBuilder when creating link spans.
    /// </summary>
    public static void SetHoverForeground(Inline inline, IBrush? value)
    {
        inline.SetValue(HoverForegroundProperty, value);
    }
}

/// <summary>
/// Represents a mapped character range of a hyperlink inside a text block.
/// </summary>
public readonly record struct MarkdownLinkRange(
    int StartIndex,
    int Length,
    string Url,
    Inline TargetInline,
    IBrush? HoverForeground);

/// <summary>
/// Manages hit-testing, mouse cursor, tooltips and click handling for native text links.
/// </summary>
public sealed class MarkdownLinkHandler
{
    private const double ClickTolerance = 4.0;

    private readonly SelectableTextBlock _textBlock;
    private readonly IReadOnlyList<MarkdownLinkRange> _linkRanges;
    private MarkdownLinkRange? _pressedLink;
    private Point _pressedPoint;
    private Inline? _activeHoverInline;
    private IBrush? _savedForeground;

    private MarkdownLinkHandler(SelectableTextBlock textBlock, IReadOnlyList<MarkdownLinkRange> linkRanges)
    {
        _textBlock = textBlock;
        _linkRanges = linkRanges;

        _textBlock.PointerMoved += OnPointerMoved;
        _textBlock.PointerPressed += OnPointerPressed;
        _textBlock.PointerReleased += OnPointerReleased;
        _textBlock.PointerExited += OnPointerExited;
        _textBlock.DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    /// <summary>
    /// Scans the inlines of the text block and attaches link interaction handling if links are present.
    /// Used by MarkdownPreviewBuilder when generating headings, paragraphs and table cells.
    /// </summary>
    public static void Attach(SelectableTextBlock textBlock)
    {
        List<MarkdownLinkRange> linkRanges = CollectLinkRanges(textBlock.Inlines);

        if (linkRanges.Count == 0)
            return;

        _ = new MarkdownLinkHandler(textBlock, linkRanges);
    }

    private static List<MarkdownLinkRange> CollectLinkRanges(InlineCollection? inlines)
    {
        List<MarkdownLinkRange> ranges = new();

        if (inlines is null)
            return ranges;

        int currentIndex = 0;
        ScanInlines(inlines, ref currentIndex, null, null, null, ranges);

        return ranges;
    }

    private static void ScanInlines(
        IEnumerable<Inline> inlines,
        ref int currentIndex,
        string? parentUrl,
        Inline? parentLinkInline,
        IBrush? parentHoverBrush,
        List<MarkdownLinkRange> ranges)
    {
        foreach (Inline inline in inlines)
        {
            ProcessInline(inline, ref currentIndex, parentUrl, parentLinkInline, parentHoverBrush, ranges);
        }
    }

    private static void ProcessInline(
        Inline inline,
        ref int currentIndex,
        string? parentUrl,
        Inline? parentLinkInline,
        IBrush? parentHoverBrush,
        List<MarkdownLinkRange> ranges)
    {
        string? url = MarkdownLink.GetUrl(inline) ?? parentUrl;
        Inline? linkInline = MarkdownLink.GetUrl(inline) is not null ? inline : parentLinkInline;
        IBrush? hoverBrush = MarkdownLink.GetHoverForeground(inline) ?? parentHoverBrush;

        switch (inline)
        {
            case Run run:
                ProcessRun(run, ref currentIndex, url, linkInline, hoverBrush, ranges);
                break;
            case Span span:
                ScanInlines(span.Inlines, ref currentIndex, url, linkInline, hoverBrush, ranges);
                break;
            case LineBreak:
                currentIndex += Environment.NewLine.Length;
                break;
            case InlineUIContainer:
                currentIndex += 1;
                break;
        }
    }

    private static void ProcessRun(
        Run run,
        ref int currentIndex,
        string? url,
        Inline? linkInline,
        IBrush? hoverBrush,
        List<MarkdownLinkRange> ranges)
    {
        int runLength = run.Text?.Length ?? 0;

        if (runLength == 0)
            return;

        if (url is not null && linkInline is not null)
            AppendLinkRange(ranges, currentIndex, runLength, url, linkInline, hoverBrush);

        currentIndex += runLength;
    }

    private static void AppendLinkRange(
        List<MarkdownLinkRange> ranges,
        int startIndex,
        int length,
        string url,
        Inline targetInline,
        IBrush? hoverForeground)
    {
        if (ranges.Count > 0)
        {
            MarkdownLinkRange last = ranges[^1];
            if (last.Url == url && last.TargetInline == targetInline && last.StartIndex + last.Length == startIndex)
            {
                ranges[^1] = new MarkdownLinkRange(last.StartIndex, last.Length + length, url, targetInline, hoverForeground);
                return;
            }
        }

        ranges.Add(new MarkdownLinkRange(startIndex, length, url, targetInline, hoverForeground));
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        Point point = args.GetPosition(_textBlock);
        MarkdownLinkRange? link = FindLinkAtPoint(point);

        if (link is not null)
        {
            UpdateHoverState(link.Value);
            _textBlock.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(_textBlock, link.Value.Url);
            return;
        }

        ClearHoverState();
        _textBlock.Cursor = Cursor.Default;
        ToolTip.SetTip(_textBlock, null);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_textBlock).Properties.IsLeftButtonPressed)
            return;

        Point point = args.GetPosition(_textBlock);
        _pressedLink = FindLinkAtPoint(point);
        _pressedPoint = point;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_pressedLink is null)
            return;

        MarkdownLinkRange linkToOpen = _pressedLink.Value;
        _pressedLink = null;

        if (args.InitialPressMouseButton != MouseButton.Left)
            return;

        Point currentPoint = args.GetPosition(_textBlock);
        Vector delta = currentPoint - _pressedPoint;

        if (delta.Length > ClickTolerance)
            return;

        OpenUrl(linkToOpen.Url);
        args.Handled = true;
    }

    private void OnPointerExited(object? sender, PointerEventArgs args)
    {
        _pressedLink = null;
        ClearHoverState();
        _textBlock.Cursor = Cursor.Default;
        ToolTip.SetTip(_textBlock, null);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        ClearHoverState();
    }

    private void UpdateHoverState(MarkdownLinkRange link)
    {
        if (ReferenceEquals(_activeHoverInline, link.TargetInline))
            return;

        ClearHoverState();

        _activeHoverInline = link.TargetInline;
        _savedForeground = link.TargetInline.Foreground;
        link.TargetInline.Foreground = link.HoverForeground ?? ResolveHoverBrush(link.TargetInline);
    }

    private void ClearHoverState()
    {
        if (_activeHoverInline is null)
            return;

        _activeHoverInline.Foreground = _savedForeground;
        _activeHoverInline = null;
        _savedForeground = null;
    }

    private static IBrush ResolveHoverBrush(Inline inline)
    {
        if (Application.Current?.TryFindResource("AccentHoverBrush", out object? resource) == true && resource is IBrush brush)
            return brush;

        if (inline.Foreground is ISolidColorBrush solid)
            return new SolidColorBrush(solid.Color) { Opacity = 0.75 };

        return Brushes.SkyBlue;
    }

    private MarkdownLinkRange? FindLinkAtPoint(Point point)
    {
        TextHitTestResult hit = _textBlock.TextLayout.HitTestPoint(point);

        if (!hit.IsInside)
            return null;

        return FindLinkAtIndex(hit.TextPosition);
    }

    private MarkdownLinkRange? FindLinkAtIndex(int textPosition)
    {
        foreach (MarkdownLinkRange link in _linkRanges)
        {
            if (textPosition >= link.StartIndex && textPosition < link.StartIndex + link.Length)
                return link;
        }

        return null;
    }

    private void OpenUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return;

            TopLevel? topLevel = TopLevel.GetTopLevel(_textBlock);
            _ = topLevel?.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Open url failed: {exception.Message}");
        }
    }
}
