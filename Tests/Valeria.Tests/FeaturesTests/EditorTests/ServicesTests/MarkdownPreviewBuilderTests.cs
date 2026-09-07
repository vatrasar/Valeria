using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
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
    private readonly MarkdownPreviewBuilder _builder =
        new(new CodeSyntaxService(), Options.Create(new AppConfig()));

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

    private IReadOnlyList<Control> Build(string markdown, Action<int, bool>? onTaskToggled = null)
    {
        return _builder.BuildBlocks(MarkdownParser.Parse(markdown), onTaskToggled).Blocks;
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
