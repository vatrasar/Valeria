using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using ReactiveUI;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorAnchorNavigatorTests
{
    [Fact]
    public async Task NavigateToAnchor_SmallDocument_ScrollsToHeading()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string markdown = "# Top\n\nParagraph\n\n# Target Section\n\nTarget content";

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText(markdown);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            bool success = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")
                    ?? throw new InvalidOperationException("PreviewScroller not found.");
                ItemsControl blocksControl = view.FindControl<ItemsControl>("PreviewBlocksControl")
                    ?? throw new InvalidOperationException("PreviewBlocksControl not found.");

                EditorScrollSyncController scrollSync = new(
                    EditorScreenTestHelpers.RequireSourceEditor(view),
                    scroller,
                    blocksControl,
                    () => editor);

                EditorAnchorNavigator navigator = new(scroller, blocksControl, scrollSync, () => editor);

                bool result = navigator.NavigateToAnchor("#target-section");
                Dispatcher.UIThread.RunJobs();
                return result;
            }, CancellationToken.None);

            Assert.True(success);
        }
        finally
        {
            if (window is not null)
            {
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task NavigateToAnchor_PolishHeadingWithSpecialChars_MatchesAndNavigates()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string markdown = "# Rozdział 1: Executive Summary & Diagnostic Dossier\n\nSome introductory content.";

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText(markdown);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            bool success = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")
                    ?? throw new InvalidOperationException("PreviewScroller not found.");
                ItemsControl blocksControl = view.FindControl<ItemsControl>("PreviewBlocksControl")
                    ?? throw new InvalidOperationException("PreviewBlocksControl not found.");

                EditorScrollSyncController scrollSync = new(
                    EditorScreenTestHelpers.RequireSourceEditor(view),
                    scroller,
                    blocksControl,
                    () => editor);

                EditorAnchorNavigator navigator = new(scroller, blocksControl, scrollSync, () => editor);

                bool result = navigator.NavigateToAnchor("#rozdział-1-executive-summary--diagnostic-dossier");
                Dispatcher.UIThread.RunJobs();
                return result;
            }, CancellationToken.None);

            Assert.True(success);
        }
        finally
        {
            if (window is not null)
            {
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task NavigateToAnchor_UnknownAnchor_ReturnsFalse()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string markdown = "# Only Heading\n\nSome text.";

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText(markdown);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            bool success = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")
                    ?? throw new InvalidOperationException("PreviewScroller not found.");
                ItemsControl blocksControl = view.FindControl<ItemsControl>("PreviewBlocksControl")
                    ?? throw new InvalidOperationException("PreviewBlocksControl not found.");

                EditorScrollSyncController scrollSync = new(
                    EditorScreenTestHelpers.RequireSourceEditor(view),
                    scroller,
                    blocksControl,
                    () => editor);

                EditorAnchorNavigator navigator = new(scroller, blocksControl, scrollSync, () => editor);

                bool result = navigator.NavigateToAnchor("#non-existent-anchor");
                Dispatcher.UIThread.RunJobs();
                return result;
            }, CancellationToken.None);

            Assert.False(success);
        }
        finally
        {
            if (window is not null)
            {
                await session.Dispatch(() =>
                {
                    window.Close();
                    Dispatcher.UIThread.RunJobs();
                }, CancellationToken.None);
            }
        }
    }
}
