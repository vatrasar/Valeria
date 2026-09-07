using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class MarkdownPreviewBuilderTests
{
    private readonly MarkdownImageLoader _imageLoader = new();
    private readonly MarkdownPreviewBuilder _builder;

    public MarkdownPreviewBuilderTests()
    {
        _builder = new MarkdownPreviewBuilder(
            new CodeSyntaxService(),
            Options.Create(new AppConfig()),
            _imageLoader);
    }



    [Fact]
    public async Task BuildBlocks_Link_RendersAsNativeSpanWithUrl()
    {
        (string? url, string textContent, bool hasUnderline) = await EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build("[example link](https://example.com)")
                .SelectMany(Descendants<SelectableTextBlock>)
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
        (double baseline, int lineCount) = await EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build("This is [example link](https://example.com) inside.")
                .SelectMany(Descendants<SelectableTextBlock>)
                .First();

            textBlock.Measure(new Avalonia.Size(800, 600));
            textBlock.Arrange(new Avalonia.Rect(0, 0, 800, 600));

            return (textBlock.TextLayout.TextLines[0].Baseline, textBlock.TextLayout.TextLines.Count);
        });

        Assert.Equal(1, lineCount);
        Assert.NotEqual(0.0, baseline);
    }

    [Fact]
    public async Task BuildBlocks_ParagraphWithLink_HoverOverLinkSetsHandCursorAndToolTip()
    {
        (object? tipOnLink, object? tipOutside, IBrush? normalBrush, IBrush? hoverBrush, IBrush? currentHoverBrush, IBrush? restoredBrush) = await EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build("Before [link text](https://test.org) after.")
                .SelectMany(Descendants<SelectableTextBlock>)
                .First();

            Span linkSpan = textBlock.Inlines!.OfType<Span>().First();
            IBrush? normal = linkSpan.Foreground;
            IBrush? expectedHover = MarkdownLink.GetHoverForeground(linkSpan);

            textBlock.Measure(new Avalonia.Size(800, 600));
            textBlock.Arrange(new Avalonia.Rect(0, 0, 800, 600));

            var linkHit = textBlock.TextLayout.HitTestTextPosition(9);
            Avalonia.Input.Pointer mousePointer = new(0, PointerType.Mouse, true);
            PointerPointProperties properties = new(RawInputModifiers.None, PointerUpdateKind.Other);

            textBlock.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent,
                textBlock,
                mousePointer,
                textBlock,
                new Avalonia.Point(linkHit.Left + 2, linkHit.Top + 2),
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
                new Avalonia.Point(0, 0),
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
    public async Task BuildBlocks_HeadingsAndParagraph_ReturnsSelectableTexts()
    {
        (int textCount, bool hasBoldHeading) = await EvaluateAsync(() =>
        {
            List<SelectableTextBlock> texts = Build("# Hi\n\nHello **bold**")
                .SelectMany(Descendants<SelectableTextBlock>)
                .ToList();

            return (texts.Count, texts.Any(text => text.FontWeight == FontWeight.Bold));
        });

        Assert.True(textCount >= 2);
        Assert.True(hasBoldHeading);
    }

    [Fact]
    public async Task BuildBlocks_FencedCSharp_ReturnsSelectableTextWithCodeAndHeader()
    {
        (string? code, bool hasLanguageLabel) = await EvaluateAsync(() =>
        {
            IReadOnlyList<Control> blocks = Build("```csharp\nvar x = 1;\n```");
            SelectableTextBlock? text = blocks.SelectMany(Descendants<SelectableTextBlock>).FirstOrDefault();
            bool hasLabel = blocks.SelectMany(Descendants<TextBlock>).Any(label => label.Text == "C#");

            return (text?.Text, hasLabel);
        });

        Assert.Contains("var x = 1;", code);
        Assert.True(hasLanguageLabel);
    }

    [Fact]
    public async Task BuildBlocks_FencedCode_ReturnsHighlightTargetForLanguage()
    {
        (string? language, string code, int targetCount) = await EvaluateAsync(() =>
        {
            PreviewBuildResult result = _builder.BuildBlocks(MarkdownParser.Parse("```py\nx = 1\n```"));
            CodeHighlightTarget target = result.CodeTargets.First();

            return (target.Language, target.Code, result.CodeTargets.Count);
        });

        Assert.Equal(1, targetCount);
        Assert.Equal("py", language);
        Assert.Contains("x = 1", code);
    }

    [Fact]
    public async Task ApplyHighlight_PopulatesInlinesWithVisibleText()
    {
        (int inlinesCount, string combinedText) = await EvaluateAsync(() =>
        {
            PreviewBuildResult result = _builder.BuildBlocks(MarkdownParser.Parse("```csharp\nint x = 5;\n```"));
            CodeHighlightTarget target = result.CodeTargets.First();

            HighlightedSpan span1 = new("int", "#569CD6", false, false);
            HighlightedSpan span2 = new(" x = 5;", null, false, false);
            HighlightedLine line = new(System.Collections.Immutable.ImmutableList.Create(span1, span2));

            _builder.ApplyHighlight(target, new[] { line });

            string text = string.Concat(target.TextBlock.Inlines?.OfType<Run>().Select(r => r.Text) ?? Enumerable.Empty<string>());

            return (target.TextBlock.Inlines?.Count ?? 0, text);
        });

        Assert.True(inlinesCount >= 2);
        Assert.Equal("int x = 5;", combinedText);
    }

    [Fact]
    public async Task BuildBlocks_FencedCode_ConfiguresHorizontalScrollAndNoWrap()
    {
        (TextWrapping wrapping, ScrollBarVisibility hScroll, ScrollBarVisibility vScroll) = await EvaluateAsync(() =>
        {
            IReadOnlyList<Control> blocks = Build("```csharp\nvar veryLongLine = 123456789;\n```");
            SelectableTextBlock text = blocks.SelectMany(Descendants<SelectableTextBlock>).First();
            ScrollViewer scroller = blocks.SelectMany(Descendants<ScrollViewer>).First();

            return (text.TextWrapping, scroller.HorizontalScrollBarVisibility, scroller.VerticalScrollBarVisibility);
        });

        Assert.Equal(TextWrapping.NoWrap, wrapping);
        Assert.Equal(ScrollBarVisibility.Auto, hScroll);
        Assert.Equal(ScrollBarVisibility.Auto, vScroll);
    }

    [Fact]
    public async Task BuildBlocks_Table_ReturnsGridWithHeaderAndBodyCells()
    {
        (int cellCount, bool hasBoldHeader) = await EvaluateAsync(() =>
        {
            List<SelectableTextBlock> cells = Build("| A | B |\n| --- | --- |\n| 1 | 2 |")
                .SelectMany(Descendants<Grid>)
                .SelectMany(Descendants<SelectableTextBlock>)
                .ToList();

            return (cells.Count, cells.Any(cell => cell.FontWeight == FontWeight.Bold));
        });

        Assert.True(cellCount >= 4);
        Assert.True(hasBoldHeader);
    }

    [Fact]
    public async Task BuildBlocks_TableWithLongText_WrapsCellContent()
    {
        (int cellCount, int lineCount, TextWrapping wrapping, TextAlignment alignment) = await EvaluateAsync(() =>
        {
            string markdown = "| Short | Long Content Header |\n| :--- | :---: |\n| 1 | This is a very long text inside the table cell that should definitely wrap across multiple lines rather than remaining on a single line. |";
            IReadOnlyList<Control> blocks = Build(markdown);
            Control tableControl = blocks.First();

            Window window = new() { Width = 400, Height = 600, Content = tableControl };
            try
            {
                window.Show();
                tableControl.Measure(new Avalonia.Size(400, 600));
                tableControl.Arrange(new Avalonia.Rect(0, 0, 400, 600));
                Dispatcher.UIThread.RunJobs();

                List<SelectableTextBlock> cells = Descendants<SelectableTextBlock>(tableControl).ToList();
                if (cells.Count == 0 && tableControl is ContentControl contentControl && contentControl.Content is Control contentChild)
                    cells = Descendants<SelectableTextBlock>(contentChild).ToList();

                SelectableTextBlock? longCell = cells.LastOrDefault();

                return (
                    cells.Count,
                    longCell?.TextLayout?.TextLines?.Count ?? 0,
                    longCell?.TextWrapping ?? TextWrapping.NoWrap,
                    longCell?.TextAlignment ?? TextAlignment.Left);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });

        Assert.True(cellCount >= 4);
        Assert.True(lineCount > 1);
        Assert.Equal(TextWrapping.Wrap, wrapping);
        Assert.Equal(TextAlignment.Center, alignment);
    }

    [Fact]
    public async Task BuildBlocks_Table_IndexColumnIsCompactAndTableFillsFullWidth()
    {
        (double tableWidth, double col0Width, double col1Width, bool col0IsAuto, bool col1IsStar) = await EvaluateAsync(() =>
        {
            string markdown = "| # | Description |\n| - | --- |\n| 1 | This is a detailed description that takes more space than a single digit index column. |";
            IReadOnlyList<Control> blocks = Build(markdown);
            Control tableControl = blocks.First();

            Window window = new() { Width = 500, Height = 600, Content = tableControl };
            try
            {
                window.Show();
                tableControl.Measure(new Avalonia.Size(500, 600));
                tableControl.Arrange(new Avalonia.Rect(0, 0, 500, 600));
                Dispatcher.UIThread.RunJobs();

                Grid grid = Descendants<Grid>(tableControl).First();
                Border cell0 = Descendants<Border>(grid).First();
                Border cell1 = Descendants<Border>(grid).Skip(1).First();

                bool isAuto = grid.ColumnDefinitions[0].Width.IsAuto;
                bool isStar = grid.ColumnDefinitions[1].Width.IsStar;

                return (
                    grid.Bounds.Width,
                    cell0.Bounds.Width,
                    cell1.Bounds.Width,
                    isAuto,
                    isStar);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });

        Assert.True(tableWidth >= 499 && tableWidth <= 500);
        Assert.True(col0IsAuto);
        Assert.True(col1IsStar);
        Assert.True(col0Width < 80);
        Assert.True(col1Width > 400);
    }

    [Fact]
    public async Task BuildBlocks_TaskList_ReturnsInteractiveCheckBoxes()
    {
        (IReadOnlyList<bool?> states, bool allEnabled, int toggledIndex, bool toggledState) = await EvaluateAsync(() =>
        {
            int toggledIndex = -1;
            bool toggledState = false;
            IReadOnlyList<CheckBox> checkBoxes = Build("- [x] done\n- [ ] open", (index, isChecked) =>
            {
                toggledIndex = index;
                toggledState = isChecked;
            }).SelectMany(Descendants<CheckBox>).ToList();

            List<bool?> initialStates = checkBoxes.Select(box => box.IsChecked).ToList();
            checkBoxes[1].IsChecked = true;

            return (
                initialStates,
                checkBoxes.All(box => box.IsEnabled),
                toggledIndex,
                toggledState);
        });

        Assert.Equal(new bool?[] { true, false }, states);
        Assert.True(allEnabled);
        Assert.Equal(1, toggledIndex);
        Assert.True(toggledState);
    }

    [Fact]
    public async Task BuildBlocks_Quote_ReturnsBorderWithAccentBar()
    {
        int accentBorders = await EvaluateAsync(() =>
            Build("> quoted")
                .SelectMany(Descendants<Border>)
                .Count(border => border.BorderThickness.Left == 2));

        Assert.Equal(1, accentBorders);
    }

    [Fact]
    public async Task BuildBlocks_ImageCached_RendersImageControlWithBitmap()
    {
        const string tinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        await _imageLoader.LoadImageAsync(tinyPng);

        (bool hasImage, bool hasSource, object? tip) = await EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build($"![Sample Image]({tinyPng})")
                .SelectMany(Descendants<SelectableTextBlock>)
                .First();

            InlineUIContainer container = textBlock.Inlines!.OfType<InlineUIContainer>().First();
            Border border = Assert.IsType<Border>(container.Child);
            Image? img = border.Child as Image;
            object? tooltip = ToolTip.GetTip(border);

            return (img is not null, img?.Source is not null, tooltip);
        });

        Assert.True(hasImage);
        Assert.True(hasSource);
        Assert.Equal($"Sample Image ({tinyPng})", tip);
    }

    [Fact]
    public async Task BuildBlocks_ImageUncached_RendersContainerWithToolTip()
    {
        const string url = "https://example.com/photo.png";

        (Border border, object? tip) = await EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build($"![Sample Image]({url})")
                .SelectMany(Descendants<SelectableTextBlock>)
                .First();

            InlineUIContainer container = textBlock.Inlines!.OfType<InlineUIContainer>().First();
            Border b = Assert.IsType<Border>(container.Child);
            object? tooltip = ToolTip.GetTip(b);

            return (b, tooltip);
        });

        Assert.NotNull(border);
        Assert.Equal($"Sample Image ({url})", tip);
    }

    [Fact]
    public async Task BuildBlocks_ImageRelativePath_WithBaseDirectory_RendersImageControlWhenCached()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ValeriaPreviewImageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string fileName = "preview-pic.png";
            string filePath = Path.Combine(tempDir, fileName);
            byte[] rawBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            await File.WriteAllBytesAsync(filePath, rawBytes);

            await _imageLoader.LoadImageAsync(fileName, tempDir);

            (bool hasImage, bool hasSource) = await EvaluateAsync(() =>
            {
                SelectableTextBlock textBlock = Build($"![Photo]({fileName})", null, tempDir)
                    .SelectMany(Descendants<SelectableTextBlock>)
                    .First();

                InlineUIContainer container = textBlock.Inlines!.OfType<InlineUIContainer>().First();
                Border border = Assert.IsType<Border>(container.Child);
                Image? img = border.Child as Image;

                return (img is not null, img?.Source is not null);
            });

            Assert.True(hasImage);
            Assert.True(hasSource);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private IReadOnlyList<Control> Build(string markdown, Action<int, bool>? onTaskToggled = null, string? baseDirectory = null)
    {
        return _builder.BuildBlocks(MarkdownParser.Parse(markdown), onTaskToggled, baseDirectory).Blocks;
    }

    private static Task<T> EvaluateAsync<T>(Func<T> evaluate)
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        return session.Dispatch(evaluate, CancellationToken.None);
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        if (root is T match)
            yield return match;

        foreach (Control child in VisualChildrenOf(root))
            foreach (T nested in Descendants<T>(child))
                yield return nested;
    }

    private static IEnumerable<Control> VisualChildrenOf(Control control)
    {
        foreach (Control child in control.GetVisualChildren().OfType<Control>())
            yield return child;

        if (control.GetVisualChildren().Count() == 0 && control is ILogical logical)
            foreach (ILogical child in logical.LogicalChildren)
                if (child is Control childControl)
                    yield return childControl;
    }
}
