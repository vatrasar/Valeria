using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class MarkdownPreviewBuilderBasicTests
{
    private readonly MarkdownImageLoader _imageLoader = new();
    private readonly MarkdownPreviewBuilder _builder;

    public MarkdownPreviewBuilderBasicTests()
    {
        _builder = new MarkdownPreviewBuilder(
            new CodeSyntaxService(),
            Options.Create(new AppConfig()),
            _imageLoader);
    }

    [Fact]
    public async Task BuildBlocksAsync_LargeDocument_RendersAllBlocks()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        StringBuilder sb = new();
        for (int i = 0; i < 120; i++)
        {
            sb.AppendLine($"Paragraph {i}");
            sb.AppendLine();
        }

        MarkdownContent content = MarkdownParser.Parse(sb.ToString());

        PreviewBuildResult result = await session.Dispatch(async () =>
        {
            return await _builder.BuildBlocksAsync(content);
        }, CancellationToken.None);

        Assert.Equal(120, result.Blocks.Count);
    }

    [Fact]
    public async Task BuildBlocksAsync_WhenCancelled_ReturnsEmpty()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        StringBuilder sb = new();
        for (int i = 0; i < 100; i++)
        {
            sb.AppendLine($"Paragraph {i}");
            sb.AppendLine();
        }

        MarkdownContent content = MarkdownParser.Parse(sb.ToString());
        using CancellationTokenSource cts = new();
        cts.Cancel();

        PreviewBuildResult result = await session.Dispatch(async () =>
        {
            return await _builder.BuildBlocksAsync(content, cancellationToken: cts.Token);
        }, CancellationToken.None);

        Assert.Empty(result.Blocks);
    }

    [Fact]
    public async Task BuildBlocks_HeadingsAndParagraph_ReturnsSelectableTexts()
    {
        (int textCount, bool hasBoldHeading) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            List<SelectableTextBlock> texts = Build("# Hi\n\nHello **bold**")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .ToList();

            return (texts.Count, texts.Any(text => text.FontWeight == FontWeight.Bold));
        });

        Assert.True(textCount >= 2);
        Assert.True(hasBoldHeading);
    }

    [Fact]
    public async Task BuildBlocks_Headings_RenderWithLatexStyleNumbering()
    {
        (string titleText, string secText, string subText) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            List<SelectableTextBlock> texts = Build("# Document Title\n\n## Section One\n\n### Sub Section")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .Where(t => t.FontWeight == FontWeight.Bold)
                .ToList();

            string text0 = string.Concat(texts[0].Inlines!.Select(i => (i as Run)?.Text ?? string.Empty));
            string text1 = string.Concat(texts[1].Inlines!.Select(i => (i as Run)?.Text ?? string.Empty));
            string text2 = string.Concat(texts[2].Inlines!.Select(i => (i as Run)?.Text ?? string.Empty));

            return (text0, text1, text2);
        });

        Assert.Equal("Document Title", titleText);
        Assert.Equal("1. Section One", secText);
        Assert.Equal("1.1 Sub Section", subText);
    }

    [Fact]
    public async Task BuildBlocks_FencedCSharp_ReturnsSelectableTextWithCodeAndHeader()
    {
        (string? code, bool hasLanguageLabel) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            IReadOnlyList<Control> blocks = Build("```csharp\nvar x = 1;\n```");
            SelectableTextBlock? text = blocks.SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>).FirstOrDefault();
            bool hasLabel = blocks.SelectMany(MarkdownPreviewTestHelpers.Descendants<TextBlock>).Any(label => label.Text == "C#");

            return (text?.Text, hasLabel);
        });

        Assert.Contains("var x = 1;", code);
        Assert.True(hasLanguageLabel);
    }

    [Fact]
    public async Task BuildBlocks_FencedCode_ReturnsHighlightTargetForLanguage()
    {
        (string? language, string code, int targetCount) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
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
        (int inlinesCount, string combinedText) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            PreviewBuildResult result = _builder.BuildBlocks(MarkdownParser.Parse("```csharp\nint x = 5;\n```"));
            CodeHighlightTarget target = result.CodeTargets.First();

            HighlightedSpan span1 = new("int", "#569CD6", false, false);
            HighlightedSpan span2 = new(" x = 5;", null, false, false);
            HighlightedLine line = new(ImmutableList.Create(span1, span2));

            _builder.ApplyHighlight(target, [line]);

            string text = string.Concat(target.TextBlock.Inlines?.OfType<Run>().Select(r => r.Text) ?? Enumerable.Empty<string>());

            return (target.TextBlock.Inlines?.Count ?? 0, text);
        });

        Assert.True(inlinesCount >= 2);
        Assert.Equal("int x = 5;", combinedText);
    }

    [Fact]
    public async Task BuildBlocks_FencedCode_ConfiguresHorizontalScrollAndNoWrap()
    {
        (TextWrapping wrapping, ScrollBarVisibility hScroll, ScrollBarVisibility vScroll) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            IReadOnlyList<Control> blocks = Build("```csharp\nvar veryLongLine = 123456789;\n```");
            SelectableTextBlock text = blocks.SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>).First();
            ScrollViewer scroller = blocks.SelectMany(MarkdownPreviewTestHelpers.Descendants<ScrollViewer>).First();

            return (text.TextWrapping, scroller.HorizontalScrollBarVisibility, scroller.VerticalScrollBarVisibility);
        });

        Assert.Equal(TextWrapping.NoWrap, wrapping);
        Assert.Equal(ScrollBarVisibility.Auto, hScroll);
        Assert.Equal(ScrollBarVisibility.Auto, vScroll);
    }

    [Fact]
    public async Task BuildBlocks_TaskList_ReturnsInteractiveCheckBoxes()
    {
        (IReadOnlyList<bool?> states, bool allEnabled, int toggledIndex, bool toggledState) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            int toggledIndex = -1;
            bool toggledState = false;
            IReadOnlyList<CheckBox> checkBoxes = Build("- [x] done\n- [ ] open", (index, isChecked) =>
            {
                toggledIndex = index;
                toggledState = isChecked;
            }).SelectMany(MarkdownPreviewTestHelpers.Descendants<CheckBox>).ToList();

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
        int accentBorders = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
            Build("> quoted")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<Border>)
                .Count(border => border.BorderThickness.Left == 2));

        Assert.Equal(1, accentBorders);
    }

    [Fact]
    public async Task BuildBlocks_AllHeadings_AppliesDecreasingFontSizesAndAppropriateWeights()
    {
        (List<double> fontSizes, List<FontWeight> weights) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            string md = "# H1\n## H2\n### H3\n#### H4\n##### H5\n###### H6";
            List<SelectableTextBlock> texts = Build(md)
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .ToList();

            return (
                texts.Select(t => t.FontSize).ToList(),
                texts.Select(t => t.FontWeight).ToList());
        });

        Assert.Equal(6, fontSizes.Count);
        Assert.Equal(32, fontSizes[0]);
        Assert.Equal(25, fontSizes[1]);
        Assert.Equal(21, fontSizes[2]);
        Assert.Equal(18, fontSizes[3]);
        Assert.Equal(15.5, fontSizes[4]);
        Assert.Equal(14, fontSizes[5]);

        Assert.Equal(FontWeight.Bold, weights[0]);
        Assert.Equal(FontWeight.Bold, weights[1]);
        Assert.Equal(FontWeight.Bold, weights[2]);
        Assert.Equal(FontWeight.SemiBold, weights[3]);
        Assert.Equal(FontWeight.SemiBold, weights[4]);
        Assert.Equal(FontWeight.SemiBold, weights[5]);
    }

    private IReadOnlyList<Control> Build(string markdown, Action<int, bool>? onTaskToggled = null)
    {
        return _builder.BuildBlocks(MarkdownParser.Parse(markdown), onTaskToggled).Blocks;
    }
}
