using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class PreviewSelectionControllerTests
{
    [Fact]
    public async Task PreviewTextBlock_PlainAndSelectedText_ExtractsCorrectly()
    {
        await EvaluateAsync(() =>
        {
            PreviewTextBlock block = new() { Text = "Hello Markdown World" };

            Assert.NotNull(block.SelectionBrush);
            Assert.Equal(20, block.GetTextLength());
            Assert.Equal("Hello Markdown World", block.GetPlainText());

            block.SetSelectionRange(6, 14);
            Assert.Equal("Markdown", block.GetSelectedPlainText());

            block.ClearSelection();
            Assert.Equal(string.Empty, block.GetSelectedPlainText());
        });
    }

    [Fact]
    public async Task PreviewTextBlock_RetainsSelectionOnLostFocus()
    {
        await EvaluateAsync(() =>
        {
            PreviewTextBlock block = new() { Text = "Sample text retaining selection", RetainSelectionOnLostFocus = true };
            block.SetSelectionRange(0, 6);

            Assert.Equal("Sample", block.GetSelectedPlainText());

            block.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));

            Assert.Equal(0, block.SelectionStart);
            Assert.Equal(6, block.SelectionEnd);
            Assert.Equal("Sample", block.GetSelectedPlainText());
        });
    }

    [Fact]
    public async Task GetSelectedText_WhenNoBlocksSelected_ReturnsEmptyString()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();
            PreviewTextBlock block1 = new() { Text = "Heading 1" };
            PreviewTextBlock block2 = new() { Text = "Paragraph content" };

            PreviewSelectionController controller = new(scroller, blocksControl, () => [block1, block2]);

            Assert.False(controller.HasSelection);
            Assert.Equal(string.Empty, controller.GetSelectedText());
        });
    }

    [Fact]
    public async Task SelectAll_SelectsAllBlocksAndFormatsOutput()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();
            PreviewTextBlock heading = new() { Text = "Title" };
            PreviewTextBlock paragraph1 = new() { Text = "First paragraph" };
            PreviewTextBlock paragraph2 = new() { Text = "Second paragraph" };

            PreviewSelectionController controller = new(scroller, blocksControl, () => [heading, paragraph1, paragraph2]);

            controller.SelectAll();

            Assert.True(controller.HasSelection);
            Assert.Equal("Title\n\nFirst paragraph\n\nSecond paragraph", controller.GetSelectedText());

            controller.ClearSelection();
            Assert.False(controller.HasSelection);
            Assert.Equal(string.Empty, controller.GetSelectedText());
        });
    }

    [Fact]
    public async Task DownwardMultiBlockSelection_ExtractsSpanningText()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();
            PreviewTextBlock heading = new() { Text = "# Main Section" };
            PreviewTextBlock para1 = new() { Text = "First body paragraph with lots of words." };
            PreviewTextBlock para2 = new() { Text = "Second body paragraph ending here." };

            PreviewSelectionController controller = new(scroller, blocksControl, () => [heading, para1, para2]);

            heading.SetSelectionRange(2, heading.GetTextLength());
            para1.SetSelectionRange(0, para1.GetTextLength());
            para2.SetSelectionRange(0, 11);

            Assert.True(controller.HasSelection);
            string selected = controller.GetSelectedText();

            Assert.Equal("Main Section\n\nFirst body paragraph with lots of words.\n\nSecond body", selected);
        });
    }

    [Fact]
    public async Task UpwardMultiBlockSelection_ExtractsSpanningTextInDocumentOrder()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();
            PreviewTextBlock heading = new() { Text = "Header" };
            PreviewTextBlock middle = new() { Text = "Middle" };
            PreviewTextBlock bottom = new() { Text = "Bottom paragraph" };

            PreviewSelectionController controller = new(scroller, blocksControl, () => [heading, middle, bottom]);

            heading.SetSelectionRange(3, heading.GetTextLength());
            middle.SetSelectionRange(0, middle.GetTextLength());
            bottom.SetSelectionRange(0, 6);

            Assert.True(controller.HasSelection);
            string selected = controller.GetSelectedText();

            Assert.Equal("der\n\nMiddle\n\nBottom", selected);
        });
    }

    [Fact]
    public async Task GetSelectedMarkdown_SingleHeadingBlock_ExtractsHeadingMarkdown()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();
            PreviewTextBlock heading = new()
            {
                Text = "Main Section",
                Tag = new Valeria.Src.Core.Markdown.BlockSourceSpan(0, 13, 1, 1)
            };

            string doc = "# Main Section\n\nSome paragraph.";
            PreviewSelectionController controller = new(scroller, blocksControl, () => [heading], getMarkdownText: () => doc);

            heading.SetSelectionRange(0, heading.GetTextLength());

            string markdown = controller.GetSelectedMarkdown();
            Assert.Equal("# Main Section", markdown);
        });
    }

    [Fact]
    public async Task GetSelectedMarkdown_MultiBlock_ExtractsFullMarkdownSnippet()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();

            string doc = "# Title\n\nThis is **bold** text.\n\n- Item 1";

            PreviewTextBlock heading = new()
            {
                Text = "Title",
                Tag = new Valeria.Src.Core.Markdown.BlockSourceSpan(0, 6, 1, 1)
            };

            PreviewTextBlock paragraph = new()
            {
                Text = "This is bold text.",
                Tag = new Valeria.Src.Core.Markdown.BlockSourceSpan(9, 31, 3, 3)
            };

            PreviewTextBlock listItem = new()
            {
                Text = "Item 1",
                Tag = new Valeria.Src.Core.Markdown.BlockSourceSpan(34, 41, 5, 5)
            };

            PreviewSelectionController controller = new(
                scroller,
                blocksControl,
                () => [heading, paragraph, listItem],
                getMarkdownText: () => doc);

            heading.SetSelectionRange(0, heading.GetTextLength());
            paragraph.SetSelectionRange(0, paragraph.GetTextLength());
            listItem.SetSelectionRange(0, listItem.GetTextLength());

            string markdown = controller.GetSelectedMarkdown();
            Assert.Equal(doc, markdown);
        });
    }

    [Fact]
    public async Task GetSelectedMarkdown_IntraBlockInline_ExtractsFormattedMarkdown()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();

            string doc = "Here is **strong** emphasis.";
            PreviewTextBlock paragraph = new()
            {
                Text = "Here is strong emphasis.",
                Tag = new Valeria.Src.Core.Markdown.BlockSourceSpan(0, 27, 1, 1)
            };

            PreviewSelectionController controller = new(
                scroller,
                blocksControl,
                () => [paragraph],
                getMarkdownText: () => doc);

            paragraph.SetSelectionRange(8, 14);

            string markdown = controller.GetSelectedMarkdown();
            Assert.Equal("**strong**", markdown);
        });
    }

    [Fact]
    public async Task GetSelectedMarkdown_WhenNoMarkdownProvided_FallsBackToPlainText()
    {
        await EvaluateAsync(() =>
        {
            ScrollViewer scroller = new();
            ItemsControl blocksControl = new();
            PreviewTextBlock paragraph = new() { Text = "Just plain rendered text." };

            PreviewSelectionController controller = new(scroller, blocksControl, () => [paragraph]);

            paragraph.SetSelectionRange(0, paragraph.GetTextLength());

            string markdown = controller.GetSelectedMarkdown();
            Assert.Equal("Just plain rendered text.", markdown);
        });
    }

    [Fact]
    public async Task DragAcrossBlocks_InEditorView_SelectsBlocksAndExtractsText()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Avalonia.Controls.Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string doc = "# Heading One\n\nFirst paragraph text here.\n\nSecond paragraph text here.";

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText(doc);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (bool hasSelection, string selectedText) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")!;
                ItemsControl blocksControl = view.FindControl<ItemsControl>("PreviewBlocksControl")!;

                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Control headingBlock = editor.State.PreviewBlocks[0];
                Control paragraphBlock = editor.State.PreviewBlocks[1];

                PreviewTextBlock? headingText = headingBlock as PreviewTextBlock ?? headingBlock.FindDescendantOfType<PreviewTextBlock>();
                PreviewTextBlock? paragraphText = paragraphBlock as PreviewTextBlock ?? paragraphBlock.FindDescendantOfType<PreviewTextBlock>();

                Assert.NotNull(headingText);
                Assert.NotNull(paragraphText);

                Avalonia.Input.Pointer mousePointer = new(0, Avalonia.Input.PointerType.Mouse, true);
                Avalonia.Input.PointerPointProperties pressProps = new(Avalonia.Input.RawInputModifiers.LeftMouseButton, Avalonia.Input.PointerUpdateKind.LeftButtonPressed);

                Avalonia.Point headingPt = headingBlock.TranslatePoint(new Avalonia.Point(10, 5), scroller) ?? new Avalonia.Point(10, 5);
                Avalonia.Input.PointerPressedEventArgs press = new(
                    headingText,
                    mousePointer,
                    scroller,
                    headingPt,
                    0,
                    pressProps,
                    Avalonia.Input.KeyModifiers.None);

                scroller.RaiseEvent(press);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Avalonia.Point paraPt = paragraphBlock.TranslatePoint(new Avalonia.Point(10, 10), scroller) ?? new Avalonia.Point(10, 50);
                Avalonia.Input.PointerPointProperties moveProps = new(Avalonia.Input.RawInputModifiers.LeftMouseButton, Avalonia.Input.PointerUpdateKind.Other);
                Avalonia.Input.PointerEventArgs move = new(
                    Avalonia.Input.InputElement.PointerMovedEvent,
                    scroller,
                    mousePointer,
                    scroller,
                    paraPt,
                    0,
                    moveProps,
                    Avalonia.Input.KeyModifiers.None);

                scroller.RaiseEvent(move);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                return (view.PreviewSelection.HasSelection, view.PreviewSelection.GetSelectedText());
            }, CancellationToken.None);

            Assert.True(hasSelection);
            Assert.NotEmpty(selectedText);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    private static Task EvaluateAsync(Action evaluate)
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        return session.Dispatch(evaluate, CancellationToken.None);
    }
}
