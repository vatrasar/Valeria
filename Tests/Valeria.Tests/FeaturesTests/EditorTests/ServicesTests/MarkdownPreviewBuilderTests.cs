using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
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
    public async Task BuildBlocks_FencedCSharp_ReturnsEditorWithCodeAndHeader()
    {
        (string? code, bool hasLanguageLabel) = await EvaluateAsync(() =>
        {
            IReadOnlyList<Control> blocks = Build("```csharp\nvar x = 1;\n```");
            TextEditor? editor = blocks.SelectMany(Descendants<TextEditor>).FirstOrDefault();
            bool hasLabel = blocks.SelectMany(Descendants<TextBlock>).Any(label => label.Text == "C#");

            return (editor?.Text, hasLabel);
        });

        Assert.Contains("var x = 1;", code);
        Assert.True(hasLanguageLabel);
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
    public async Task BuildBlocks_TaskList_ReturnsDisabledCheckBoxes()
    {
        IReadOnlyList<bool?> states = await EvaluateAsync(() =>
            Build("- [x] done\n- [ ] open")
                .SelectMany(Descendants<CheckBox>)
                .Select(box => box.IsChecked)
                .ToList());

        bool allDisabled = await EvaluateAsync(() =>
            Build("- [x] done\n- [ ] open")
                .SelectMany(Descendants<CheckBox>)
                .All(box => !box.IsEnabled));

        Assert.Equal(new bool?[] { true, false }, states);
        Assert.True(allDisabled);
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

    private IReadOnlyList<Control> Build(string markdown)
    {
        return _builder.BuildBlocks(MarkdownParser.Parse(markdown));
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
