using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ReactiveUI;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorScreenSyncIntegrationTests
{
    [Fact]
    public async Task ClickInSourceEditor_NavigatesPreviewAndAppliesHighlight()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string longDoc = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"# Section {i}\n\nContent for section {i} with additional text to expand height."));

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(longDoc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (double previewOffsetBefore, double previewOffsetAfter, bool hasHighlightClass) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")
                    ?? throw new InvalidOperationException("PreviewScroller not found.");

                Dispatcher.UIThread.RunJobs();
                double before = scroller.Offset.Y;

                source.TextArea.Caret.Line = 80;
                view.HandleEditorPointerReleased(80);
                Dispatcher.UIThread.RunJobs();

                double after = scroller.Offset.Y;
                bool highlightFound = editor.State.PreviewBlocks.Any(b => b.Classes.Contains("sync-highlight"));

                return (before, after, highlightFound);
            }, CancellationToken.None);

            Assert.Equal(0, previewOffsetBefore);
            Assert.True(previewOffsetAfter > 100);
            Assert.True(hasHighlightClass);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ClickInSourceEditor_OnListItem_HighlightsListItemInPreview()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string doc = "# Title\n\n- Item 1\n- Item 2\n- Item 3";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (bool rowHighlighted, string? highlightedTag) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);

                source.TextArea.Caret.Line = 4;
                view.HandleEditorPointerReleased(4);
                Dispatcher.UIThread.RunJobs();

                Control? highlighted = view.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(c => c.Classes.Contains("sync-highlight"));

                return (highlighted is Grid, highlighted?.Tag?.ToString());
            }, CancellationToken.None);

            Assert.True(rowHighlighted);
            Assert.NotNull(highlightedTag);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ClickInPreview_NavigatesEditorToMatchingLine()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string longDoc = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"# Section {i}\n\nContent for section {i} with additional text to expand height."));

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(longDoc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (int targetExpectedLine, int caretLineAfter, bool highlightFound, bool isCaretVisibleInViewport, double caretY, double scrollY, double viewportH) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);

                Control targetBlock = editor.State.PreviewBlocks[40];
                BlockSourceSpan span = (BlockSourceSpan)targetBlock.Tag!;

                view.HandlePreviewBlockClicked(targetBlock, span, new Point(0, 0));
                Dispatcher.UIThread.RunJobs();

                ScrollViewer? inner = source.FindDescendantOfType<ScrollViewer>();
                Rect r = source.TextArea.Caret.CalculateCaretRectangle();
                double scroll = inner!.Offset.Y;
                double viewH = inner.Viewport.Height;

                bool isVisible = r.Y >= scroll && r.Y <= scroll + viewH;

                return (span.StartLine, source.TextArea.Caret.Line, targetBlock.Classes.Contains("sync-highlight"), isVisible, r.Y, scroll, viewH);
            }, CancellationToken.None);

            Assert.Equal(targetExpectedLine, caretLineAfter);
            Assert.True(highlightFound);
            Assert.True(isCaretVisibleInViewport, $"Caret at Y={caretY} was not within visible range [{scrollY}, {scrollY + viewportH}]");
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ClickInPreview_WithIntraBlockOffset_NavigatesToCalculatedLine()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string paragraphLines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line {i} of giant paragraph with long words to test resolution."));
            string doc = $"# Heading\n\n{paragraphLines}";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (int startLine, int endLine, int selectionStart, int selectionLength) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);

                Control targetBlock = editor.State.PreviewBlocks[1];
                BlockSourceSpan span = (BlockSourceSpan)targetBlock.Tag!;

                double halfwayY = targetBlock.Bounds.Height * 0.5;
                view.HandlePreviewBlockClicked(targetBlock, span, new Point(0, halfwayY));
                Dispatcher.UIThread.RunJobs();

                return (span.StartLine, span.EndLine, source.SelectionStart, source.SelectionLength);
            }, CancellationToken.None);

            Assert.Equal(3, startLine);
            Assert.Equal(22, endLine);
            Assert.True(selectionLength > 500);

            await Task.Delay(900);

            int caretLineAfterPulse = await session.Dispatch(() =>
            {
                Dispatcher.UIThread.RunJobs();
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                return source.TextArea.Caret.Line;
            }, CancellationToken.None);

            Assert.InRange(caretLineAfterPulse, 11, 14);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ClickInPreview_SingleLineParagraph_NavigatesToCalculatedCharacterOffset()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string hugeParagraph = "Start of huge paragraph. " + new string('a', 600) + " Middle of paragraph. " + new string('b', 600) + " End of paragraph.";
            string doc = $"# Heading\n\n{hugeParagraph}";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (int startOffset, int endOffset, int selectionStart, int selectionLength) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);

                Control targetBlock = editor.State.PreviewBlocks[1];
                BlockSourceSpan span = (BlockSourceSpan)targetBlock.Tag!;

                double halfwayY = targetBlock.Bounds.Height * 0.5;
                view.HandlePreviewBlockClicked(targetBlock, span, new Point(0, halfwayY));
                Dispatcher.UIThread.RunJobs();

                return (span.StartOffset, span.EndOffset, source.SelectionStart, source.SelectionLength);
            }, CancellationToken.None);

            int expectedMidpoint = startOffset + (endOffset - startOffset) / 2;
            Assert.True(endOffset - startOffset > 1000);
            Assert.Equal(startOffset, selectionStart);
            Assert.True(selectionLength > 1000);

            await Task.Delay(900);

            int caretOffsetAfterPulse = await session.Dispatch(() =>
            {
                Dispatcher.UIThread.RunJobs();
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                return source.CaretOffset;
            }, CancellationToken.None);

            Assert.InRange(caretOffsetAfterPulse, expectedMidpoint - 100, expectedMidpoint + 100);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ClickInPreview_ShortHeading_NavigatesToStartOfHeading()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string doc = "# Main Heading\n\nShort first paragraph.\n\n## Sub Heading\n\nAnother short paragraph.";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (int caretOffsetAfter, int caretLineAfter, int expectedOffset, int expectedLine) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);

                Control subHeadingBlock = editor.State.PreviewBlocks[2];
                BlockSourceSpan span = (BlockSourceSpan)subHeadingBlock.Tag!;

                view.HandlePreviewBlockClicked(subHeadingBlock, span, new Point(0, 15));
                Dispatcher.UIThread.RunJobs();

                return (source.SelectionStart, source.TextArea.Caret.Line, span.StartOffset, span.StartLine);
            }, CancellationToken.None);

            Assert.Equal(expectedOffset, caretOffsetAfter);
            Assert.Equal(expectedLine, caretLineAfter);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ClickInPreview_MarginOrPadding_NavigatesToClosestBlock()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string doc = "# First Section\n\nContent 1.\n\n# Second Section\n\nContent 2.";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (int caretLineAfter, bool highlightFound, int expectedTargetLine) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")!;
                Control secondHeading = editor.State.PreviewBlocks[2];
                BlockSourceSpan span = (BlockSourceSpan)secondHeading.Tag!;

                Point? headingPt = secondHeading.TranslatePoint(new Point(0, 0), scroller);
                double targetY = headingPt?.Y ?? 80;

                Avalonia.Input.Pointer mousePointer = new(0, PointerType.Mouse, true);
                PointerPointProperties pressProps = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
                PointerPressedEventArgs pressedArgs = new(
                    scroller,
                    mousePointer,
                    scroller,
                    new Point(10, targetY),
                    0,
                    pressProps,
                    KeyModifiers.None);

                scroller.RaiseEvent(pressedArgs);
                Dispatcher.UIThread.RunJobs();

                return (source.TextArea.Caret.Line, secondHeading.Classes.Contains("sync-highlight"), span.StartLine);
            }, CancellationToken.None);

            Assert.Equal(expectedTargetLine, caretLineAfter);
            Assert.True(highlightFound);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ClickInPreview_SelectsEntireBlock_NotJustSingleWord()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string doc = "# First Heading\n\nThis is a full paragraph with multiple words to test full block selection.\n\n```csharp\nint x = 42;\n```";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (string selectedParagraphText, string expectedParagraphText) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);

                Control paragraphBlock = editor.State.PreviewBlocks[1];
                BlockSourceSpan span = (BlockSourceSpan)paragraphBlock.Tag!;

                view.HandlePreviewBlockClicked(paragraphBlock, span, new Point(50, 10));
                Dispatcher.UIThread.RunJobs();

                return (source.SelectedText, "This is a full paragraph with multiple words to test full block selection.");
            }, CancellationToken.None);

            Assert.Equal(expectedParagraphText, selectedParagraphText);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
        }
    }
}
