using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ReactiveUI;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorScreenSearchIntegrationTests
{
    [Fact]
    public async Task CtrlF_WhenEditorNotFocused_OpensPreviewSearchBar()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                Border searchBar = view.FindControl<Border>("PreviewSearchBar")
                    ?? throw new InvalidOperationException("PreviewSearchBar not found.");

                Assert.False(searchBar.IsVisible);

                view.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.F,
                    KeyModifiers = KeyModifiers.Control,
                    Source = view
                });

                Dispatcher.UIThread.RunJobs();

                Assert.True(editor.State.IsPreviewSearchOpen);
                Assert.True(searchBar.IsVisible);
            }, CancellationToken.None);
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
    public async Task CtrlF_WhenEditorPanelToggledHidden_OpensPreviewSearchBar()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                Assert.True(editor.State.IsEditorVisible);

                editor.ToggleEditorCommand.Execute().Subscribe();
                Assert.False(editor.State.IsEditorVisible);

                EditorView view = EditorScreenTestHelpers.RequireView(window);
                Border searchBar = view.FindControl<Border>("PreviewSearchBar")
                    ?? throw new InvalidOperationException("PreviewSearchBar not found.");

                Assert.False(searchBar.IsVisible);

                window.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.F,
                    KeyModifiers = KeyModifiers.Control,
                    Source = window
                });

                Dispatcher.UIThread.RunJobs();

                Assert.True(editor.State.IsPreviewSearchOpen);
                Assert.True(searchBar.IsVisible);
            }, CancellationToken.None);
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
    public async Task PreviewSearch_WhenQueryEntered_FindsMatchesAndDisplaysMatchCount()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText("# Test Heading\n\nThis is a test paragraph with test word.");
            }, CancellationToken.None);
            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextBox searchBox = view.FindControl<TextBox>("PreviewSearchTextBox")
                    ?? throw new InvalidOperationException("PreviewSearchTextBox not found.");
                TextBlock countLabel = view.FindControl<TextBlock>("SearchMatchCountLabel")
                    ?? throw new InvalidOperationException("SearchMatchCountLabel not found.");

                editor.OpenPreviewSearchCommand.Execute().Subscribe();
                Dispatcher.UIThread.RunJobs();

                searchBox.Text = "test";
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("test", editor.State.PreviewSearchQuery);
                Assert.Equal(3, editor.State.PreviewSearchMatchCount);
                Assert.Equal(1, editor.State.PreviewSearchMatchIndex);
                Assert.Contains("1", countLabel.Text);
                Assert.Contains("3", countLabel.Text);
            }, CancellationToken.None);
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
    public async Task Escape_WhenPreviewSearchOpen_ClosesPreviewSearchBarAndClearsHighlights()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText("# Hello world\n\nSome preview content.");
            }, CancellationToken.None);
            await Task.Delay(EditorScreenTestHelpers.PreviewWaitMilliseconds);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                Border searchBar = view.FindControl<Border>("PreviewSearchBar")
                    ?? throw new InvalidOperationException("PreviewSearchBar not found.");
                TextBox searchBox = view.FindControl<TextBox>("PreviewSearchTextBox")
                    ?? throw new InvalidOperationException("PreviewSearchTextBox not found.");

                editor.OpenPreviewSearchCommand.Execute().Subscribe();
                searchBox.Text = "preview";
                Dispatcher.UIThread.RunJobs();

                Assert.True(searchBar.IsVisible);
                Assert.Equal(1, editor.State.PreviewSearchMatchCount);

                view.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.Escape,
                    Source = searchBox
                });
                Dispatcher.UIThread.RunJobs();

                Assert.False(editor.State.IsPreviewSearchOpen);
                Assert.False(searchBar.IsVisible);
            }, CancellationToken.None);
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
