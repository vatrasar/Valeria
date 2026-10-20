using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class MarkdownPreviewBuilderLinkTests
{
    private readonly MarkdownImageLoader _imageLoader = new();
    private readonly MarkdownPreviewBuilder _builder;

    public MarkdownPreviewBuilderLinkTests()
    {
        _builder = new MarkdownPreviewBuilder(
            new CodeSyntaxService(),
            Options.Create(new AppConfig()),
            _imageLoader);
    }

    [Fact]
    public async Task BuildBlocks_Link_RendersAsNativeSpanWithUrl()
    {
        (string? url, string textContent, bool hasUnderline) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build("[example link](https://example.com)")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .First();

            Span linkSpan = textBlock.Inlines!.OfType<Span>().First();
            string? targetUrl = MarkdownLink.GetUrl(linkSpan);
            string inlineText = string.Concat(linkSpan.Inlines.OfType<Run>().Select(run => run.Text));
            bool isUnderlined = linkSpan.TextDecorations is not null && linkSpan.TextDecorations.Count > 0;

            return (targetUrl, inlineText, isUnderlined);
        });

        Assert.Equal("https://example.com", url);
        Assert.Equal("example link", textContent);
        Assert.False(hasUnderline);
    }

    [Fact]
    public async Task BuildBlocks_ParagraphWithLink_HasUniformBaseline()
    {
        (double baseline, int lineCount) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build("This is [example link](https://example.com) inside.")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .First();

            textBlock.Measure(new Size(800, 600));
            textBlock.Arrange(new Rect(0, 0, 800, 600));

            return (textBlock.TextLayout.TextLines[0].Baseline, textBlock.TextLayout.TextLines.Count);
        });

        Assert.Equal(1, lineCount);
        Assert.NotEqual(0.0, baseline);
    }

    [Fact]
    public async Task BuildBlocks_ParagraphWithLink_HoverOverLinkSetsHandCursorAndToolTip()
    {
        (object? tipOnLink, object? tipOutside, IBrush? normalBrush, IBrush? hoverBrush, IBrush? currentHoverBrush, IBrush? restoredBrush) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build("Before [link text](https://test.org) after.")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .First();

            Span linkSpan = textBlock.Inlines!.OfType<Span>().First();
            IBrush? normal = linkSpan.Foreground;
            IBrush? expectedHover = MarkdownLink.GetHoverForeground(linkSpan);

            textBlock.Measure(new Size(800, 600));
            textBlock.Arrange(new Rect(0, 0, 800, 600));

            var linkHit = textBlock.TextLayout.HitTestTextPosition(9);
            Pointer mousePointer = new(0, PointerType.Mouse, true);
            PointerPointProperties properties = new(RawInputModifiers.None, PointerUpdateKind.Other);

            textBlock.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent,
                textBlock,
                mousePointer,
                textBlock,
                new Point(linkHit.Left + 2, linkHit.Top + 2),
                0,
                properties,
                KeyModifiers.None));

            object? tipWhenOverLink = ToolTip.GetTip(textBlock);
            IBrush? brushWhenHovered = linkSpan.Foreground;

            textBlock.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent,
                textBlock,
                mousePointer,
                textBlock,
                new Point(0, 0),
                0,
                properties,
                KeyModifiers.None));

            object? tipWhenOutside = ToolTip.GetTip(textBlock);
            IBrush? brushWhenRestored = linkSpan.Foreground;

            return (tipWhenOverLink, tipWhenOutside, normal, expectedHover, brushWhenHovered, brushWhenRestored);
        });

        Assert.Equal("https://test.org", tipOnLink);
        Assert.Null(tipOutside);
        Assert.NotNull(hoverBrush);
        Assert.Equal(hoverBrush, currentHoverBrush);
        Assert.Equal(normalBrush, restoredBrush);
    }

    [Fact]
    public async Task BuildBlocks_ParagraphWithLink_ClickOpensUrl()
    {
        (string? openedUrl, bool releaseHandled) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            string? capturedUrl = null;
            MarkdownLinkHandler.UrlOpenerOverride = url => capturedUrl = url;

            try
            {
                SelectableTextBlock textBlock = Build("Before [link text](https://test.org) after.")
                    .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                    .First();

                textBlock.Measure(new Size(800, 600));
                textBlock.Arrange(new Rect(0, 0, 800, 600));

                var linkHit = textBlock.TextLayout.HitTestTextPosition(9);
                Pointer mousePointer = new(0, PointerType.Mouse, true);
                PointerPointProperties pressProperties = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

                var pressedArgs = new PointerPressedEventArgs(
                    textBlock,
                    mousePointer,
                    textBlock,
                    new Point(linkHit.Left + 2, linkHit.Top + 2),
                    0,
                    pressProperties,
                    KeyModifiers.None);

                textBlock.RaiseEvent(pressedArgs);

                PointerPointProperties releaseProperties = new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
                var releasedArgs = new PointerReleasedEventArgs(
                    textBlock,
                    mousePointer,
                    textBlock,
                    new Point(linkHit.Left + 2, linkHit.Top + 2),
                    0,
                    releaseProperties,
                    KeyModifiers.None,
                    MouseButton.Left);

                textBlock.RaiseEvent(releasedArgs);

                return (capturedUrl, releasedArgs.Handled);
            }
            finally
            {
                MarkdownLinkHandler.UrlOpenerOverride = null;
            }
        });

        Assert.Equal("https://test.org", openedUrl);
        Assert.True(releaseHandled);
    }

    [Fact]
    public async Task BuildBlocks_ParagraphWithLink_DragSelectionDoesNotOpenUrl()
    {
        (string? openedUrl, bool releaseHandled) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            string? capturedUrl = null;
            MarkdownLinkHandler.UrlOpenerOverride = url => capturedUrl = url;

            try
            {
                SelectableTextBlock textBlock = Build("Before [link text](https://test.org) after.")
                    .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                    .First();

                textBlock.Measure(new Size(800, 600));
                textBlock.Arrange(new Rect(0, 0, 800, 600));

                var linkHit = textBlock.TextLayout.HitTestTextPosition(9);
                Pointer mousePointer = new(0, PointerType.Mouse, true);
                PointerPointProperties pressProperties = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

                var pressedArgs = new PointerPressedEventArgs(
                    textBlock,
                    mousePointer,
                    textBlock,
                    new Point(linkHit.Left + 2, linkHit.Top + 2),
                    0,
                    pressProperties,
                    KeyModifiers.None);

                textBlock.RaiseEvent(pressedArgs);

                PointerPointProperties releaseProperties = new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
                var releasedArgs = new PointerReleasedEventArgs(
                    textBlock,
                    mousePointer,
                    textBlock,
                    new Point(linkHit.Left + 50, linkHit.Top + 2),
                    0,
                    releaseProperties,
                    KeyModifiers.None,
                    MouseButton.Left);

                textBlock.RaiseEvent(releasedArgs);

                return (capturedUrl, releasedArgs.Handled);
            }
            finally
            {
                MarkdownLinkHandler.UrlOpenerOverride = null;
            }
        });

        Assert.Null(openedUrl);
        Assert.False(releaseHandled);
    }

    [Fact]
    public async Task BuildBlocks_ParagraphWithLink_WwwLinkNormalizedToHttps()
    {
        string? openedUrl = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            string? capturedUrl = null;
            MarkdownLinkHandler.UrlOpenerOverride = url => capturedUrl = url;

            try
            {
                SelectableTextBlock textBlock = Build("[example link](www.example.com)")
                    .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                    .First();

                textBlock.Measure(new Size(800, 600));
                textBlock.Arrange(new Rect(0, 0, 800, 600));

                var linkHit = textBlock.TextLayout.HitTestTextPosition(2);
                Pointer mousePointer = new(0, PointerType.Mouse, true);
                PointerPointProperties pressProperties = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

                var pressedArgs = new PointerPressedEventArgs(
                    textBlock,
                    mousePointer,
                    textBlock,
                    new Point(linkHit.Left + 2, linkHit.Top + 2),
                    0,
                    pressProperties,
                    KeyModifiers.None);

                textBlock.RaiseEvent(pressedArgs);

                PointerPointProperties releaseProperties = new(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
                var releasedArgs = new PointerReleasedEventArgs(
                    textBlock,
                    mousePointer,
                    textBlock,
                    new Point(linkHit.Left + 2, linkHit.Top + 2),
                    0,
                    releaseProperties,
                    KeyModifiers.None,
                    MouseButton.Left);

                textBlock.RaiseEvent(releasedArgs);

                return capturedUrl;
            }
            finally
            {
                MarkdownLinkHandler.UrlOpenerOverride = null;
            }
        });

        Assert.Equal("https://www.example.com", openedUrl);
    }

    private IReadOnlyList<Control> Build(string markdown)
    {
        return _builder.BuildBlocks(MarkdownParser.Parse(markdown)).Blocks;
    }
}
