using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure;
using Valeria.Src.Infrastructure.Navigation;
using ReactiveUI;
using Splat;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorScreenIntegrationTests
{
    private const int PreviewWaitMilliseconds = 1200;

    [Fact]
    public async Task ViewModelText_PopulatesSourceEditorAndPreview()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                Assert.False(editor.State.IsEditorVisible);
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText("# Hi\n\nHello **bold**");
            }, CancellationToken.None);
            await WaitForPreviewAsync(session, editor);

            (string viewModelText, string sourceText, int previewCount, bool hasItemsSource, string? error, double editorWidth, string titleLabel) =
                await session.Dispatch(() =>
                {
                    EditorView view = RequireView(window);
                    TextEditor source = RequireSourceEditor(view);
                    ItemsControl preview = view.FindControl<ItemsControl>("PreviewBlocksControl")
                        ?? throw new InvalidOperationException("PreviewBlocksControl not found.");
                    TextBlock title = view.FindControl<TextBlock>("DocumentTitleLabel")
                        ?? throw new InvalidOperationException("DocumentTitleLabel not found.");

                    return (
                        editor.State.MarkdownText,
                        source.Text,
                        editor.State.PreviewBlocks.Count,
                        preview.ItemsSource is not null,
                        editor.State.ErrorMessage,
                        source.Bounds.Width,
                        title.Text ?? string.Empty);
                }, CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(error), $"ErrorMessage: {error}");
            Assert.True(titleLabel.Length > 0, "Status bar was never rendered, view activation did not run.");
            Assert.True(viewModelText.Contains("# Hi"), $"viewModelText was '{viewModelText}'");
            Assert.True(sourceText.Contains("# Hi"), $"sourceText was '{sourceText}'");
            Assert.True(previewCount > 0);
            Assert.True(hasItemsSource);
            Assert.True(editorWidth > 100);
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
    public async Task TypingInSourceEditor_UpdatesViewModelAndPreview()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);
                source.Text = "# typed\n\nsome text";
            }, CancellationToken.None);

            await WaitForPreviewAsync(session, editor);

            (string viewModelText, int previewCount, string? error) = await session.Dispatch(() =>
            {
                Dispatcher.UIThread.RunJobs();
                return (editor.State.MarkdownText, editor.State.PreviewBlocks.Count, editor.State.ErrorMessage);
            }, CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(error), $"Error: {error}");
            Assert.Contains("typed", viewModelText);
            Assert.True(previewCount > 0);
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
    public async Task ToggleEditor_SwitchesVisibility()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            bool initial = await session.Dispatch(() => editor.State.IsEditorVisible, CancellationToken.None);
            Assert.False(initial);

            await session.Dispatch(() => editor.ToggleEditorCommand.Execute().Subscribe(), CancellationToken.None);
            bool afterFirstToggle = await session.Dispatch(() => editor.State.IsEditorVisible, CancellationToken.None);
            Assert.True(afterFirstToggle);

            await session.Dispatch(() => editor.ToggleEditorCommand.Execute().Subscribe(), CancellationToken.None);
            bool afterSecondToggle = await session.Dispatch(() => editor.State.IsEditorVisible, CancellationToken.None);
            Assert.False(afterSecondToggle);
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
    public async Task InitializeAsync_ForCommandLineFilePath_LoadsFileContent()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        string filePath = Path.Combine(Path.GetTempPath(), "valeria-command-line-test.md");
        const string expectedContent = "# Z pliku\n\nWczytane z argumentu.";

        await session.Dispatch(async () =>
        {
            EditorViewModel editor = CreateEditorWithFile(filePath, expectedContent);
            await editor.InitializeAsync(CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(editor.State.ErrorMessage), $"ErrorMessage: {editor.State.ErrorMessage}");
            Assert.Equal(expectedContent, editor.State.MarkdownText);
            Assert.Equal(filePath, editor.State.FilePath);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task CtrlF_WhenEditorNotFocused_OpensPreviewSearchBar()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                Assert.True(editor.State.IsEditorVisible);

                editor.ToggleEditorCommand.Execute().Subscribe();
                Assert.False(editor.State.IsEditorVisible);

                EditorView view = RequireView(window);
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText("# Test Heading\n\nThis is a test paragraph with test word.");
            }, CancellationToken.None);
            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                editor.SetMarkdownText("# Hello world\n\nSome preview content.");
            }, CancellationToken.None);
            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
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

    [Fact]
    public async Task Navigation_ToSettingsAndBack_PreservesPreview()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await session.Dispatch(() =>
            {
                RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
                Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));

                IConfiguration configuration = new ConfigurationBuilder().Build();
                ServiceCollection services = new();
                services.AddValeria(configuration);
                services.RemoveAll<IFilesDockService>();
                services.AddSingleton<IFilesDockService>(new FakeFilesDockService());
                ServiceProvider provider = services.BuildServiceProvider();

                AppBootstrapper.RegisterFeatureModules();

                MainWindowViewModel shell = provider.GetRequiredService<MainWindowViewModel>();
                MainWindow mainWindow = new() { ViewModel = shell, Width = 1280, Height = 800 };
                mainWindow.Show();
                Dispatcher.UIThread.RunJobs();

                EditorViewModel? editorVm = shell.Router.GetCurrentViewModel() as EditorViewModel;
                return (mainWindow, editorVm!);
            }, CancellationToken.None);

            await session.Dispatch(async () =>
            {
                await editor.InitializeAsync(CancellationToken.None);
                editor.SetMarkdownText("# Title\n\nSome preview content");
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await WaitForPreviewAsync(session, editor);

            await session.Dispatch(() =>
            {
                EditorView view1 = RequireView(window);
                ItemsControl preview1 = view1.FindControl<ItemsControl>("PreviewBlocksControl")!;
                int preview1VisualCount = preview1.GetVisualDescendants().Count();
                Assert.True(preview1VisualCount > 5);

                editor.NavigateToSettingsCommand.Execute().Subscribe();
                Dispatcher.UIThread.RunJobs();

                MainWindowViewModel shell = ((MainWindow)window).ViewModel!;
                SettingsViewModel settings = (SettingsViewModel)shell.Router.GetCurrentViewModel()!;
                settings.SetFontSize(18);
                settings.SaveSettingsCommand.Execute().Subscribe();
                Dispatcher.UIThread.RunJobs();

                settings.NavigateBackCommand.Execute().Subscribe();
                Dispatcher.UIThread.RunJobs();

                EditorView view2 = RequireView(window);
                ItemsControl preview2 = view2.FindControl<ItemsControl>("PreviewBlocksControl")!;
                TextEditor source2 = RequireSourceEditor(view2);

                Assert.Same(view1, view2);
                Assert.True(preview2.GetVisualDescendants().Count() > 5);
                Assert.Equal(18, source2.FontSize);
                Assert.Equal("# Title\n\nSome preview content", source2.Text);
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
    public async Task StatusBar_DisplaysFullPathAndWrapsWhenTooLong()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;
        string tempFilePath = Path.GetFullPath("test-sample-document.md");

        try
        {
            (window, EditorViewModel editor) = await session.Dispatch(() =>
            {
                RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
                Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));

                IConfiguration configuration = new ConfigurationBuilder().Build();
                ServiceCollection services = new();
                services.AddValeria(configuration);
                services.RemoveAll<IFilesDockService>();
                services.AddSingleton<IFilesDockService>(new FakeFilesDockService());
                services.RemoveAll<IEditorFileService>();
                services.AddSingleton<IEditorFileService>(new FakeEditorFileService("# Test file"));
                ServiceProvider provider = services.BuildServiceProvider();

                MainWindowViewModel shell = provider.GetRequiredService<MainWindowViewModel>();
                EditorViewModel editor = ActivatorUtilities.CreateInstance<EditorViewModel>(provider, shell, tempFilePath);
                EditorView view = new() { ViewModel = editor };

                Window window = new() { Content = view, Width = 1280, Height = 800 };
                window.Show();
                Dispatcher.UIThread.RunJobs();

                return (window, editor);
            }, CancellationToken.None);

            await session.Dispatch(async () =>
            {
                await editor.InitializeAsync(CancellationToken.None);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextBlock title = view.FindControl<TextBlock>("DocumentTitleLabel")
                    ?? throw new InvalidOperationException("DocumentTitleLabel not found.");

                Assert.Equal(tempFilePath, editor.State.DocumentPath);
                Assert.Equal(tempFilePath, title.Text);
                Assert.Equal(Avalonia.Media.TextWrapping.Wrap, title.TextWrapping);

                editor.SetMarkdownText("# Modified");
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(tempFilePath + "*", editor.State.DocumentPath);
                Assert.Equal(tempFilePath + "*", title.Text);
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
    public async Task ScrollPanels_AreIndependent_DoNotSynchronizeOnScroll()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string longDoc = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"# Section {i}\n\nContent for section {i} with additional text to expand height."));

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(longDoc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await WaitForPreviewAsync(session, editor);

            (double editorY, double previewY) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")
                    ?? throw new InvalidOperationException("PreviewScroller not found.");
                ScrollViewer innerScroller = source.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()
                    ?? throw new InvalidOperationException("SourceEditor inner ScrollViewer not found.");

                Dispatcher.UIThread.RunJobs();

                double eMax = source.ExtentHeight - source.ViewportHeight;
                innerScroller.Offset = new Avalonia.Vector(0, eMax * 0.5);
                Dispatcher.UIThread.RunJobs();

                return (innerScroller.Offset.Y, scroller.Offset.Y);
            }, CancellationToken.None);

            Assert.True(editorY > 100);
            Assert.Equal(0, previewY);
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
    public async Task ClickInSourceEditor_NavigatesPreviewAndAppliesHighlight()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string longDoc = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"# Section {i}\n\nContent for section {i} with additional text to expand height."));

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(longDoc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            (double previewOffsetBefore, double previewOffsetAfter, bool hasHighlightClass) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);
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
    public async Task ClickInPreview_NavigatesEditorToMatchingLine()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string longDoc = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"# Section {i}\n\nContent for section {i} with additional text to expand height."));

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(longDoc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            (int targetExpectedLine, int caretLineAfter, bool highlightFound, bool isCaretVisibleInViewport, double caretY, double scrollY, double viewportH) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);

                Control targetBlock = editor.State.PreviewBlocks[40];
                BlockSourceSpan span = (BlockSourceSpan)targetBlock.Tag!;

                view.HandlePreviewBlockClicked(targetBlock, span, new Avalonia.Point(0, 0));
                Dispatcher.UIThread.RunJobs();

                ScrollViewer? inner = source.FindDescendantOfType<ScrollViewer>();
                Avalonia.Rect r = source.TextArea.Caret.CalculateCaretRectangle();
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string paragraphLines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line {i} of giant paragraph with long words to test resolution."));
            string doc = $"# Heading\n\n{paragraphLines}";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            (int startLine, int endLine, int selectionStart, int selectionLength) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);

                Control targetBlock = editor.State.PreviewBlocks[1];
                BlockSourceSpan span = (BlockSourceSpan)targetBlock.Tag!;

                double halfwayY = targetBlock.Bounds.Height * 0.5;
                view.HandlePreviewBlockClicked(targetBlock, span, new Avalonia.Point(0, halfwayY));
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
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string hugeParagraph = "Start of huge paragraph. " + new string('a', 600) + " Middle of paragraph. " + new string('b', 600) + " End of paragraph.";
            string doc = $"# Heading\n\n{hugeParagraph}";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            (int startOffset, int endOffset, int selectionStart, int selectionLength) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);

                Control targetBlock = editor.State.PreviewBlocks[1];
                BlockSourceSpan span = (BlockSourceSpan)targetBlock.Tag!;

                double halfwayY = targetBlock.Bounds.Height * 0.5;
                view.HandlePreviewBlockClicked(targetBlock, span, new Avalonia.Point(0, halfwayY));
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
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string doc = "# Main Heading\n\nShort first paragraph.\n\n## Sub Heading\n\nAnother short paragraph.";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            (int caretOffsetAfter, int caretLineAfter, int expectedOffset, int expectedLine) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);

                Control subHeadingBlock = editor.State.PreviewBlocks[2];
                BlockSourceSpan span = (BlockSourceSpan)subHeadingBlock.Tag!;

                view.HandlePreviewBlockClicked(subHeadingBlock, span, new Avalonia.Point(0, 15));
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string doc = "# First Section\n\nContent 1.\n\n# Second Section\n\nContent 2.";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            (int caretLineAfter, bool highlightFound, int expectedTargetLine) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);
                ScrollViewer scroller = view.FindControl<ScrollViewer>("PreviewScroller")!;
                Control secondHeading = editor.State.PreviewBlocks[2];
                BlockSourceSpan span = (BlockSourceSpan)secondHeading.Tag!;

                Avalonia.Point? headingPt = secondHeading.TranslatePoint(new Avalonia.Point(0, 0), scroller);
                double targetY = headingPt?.Y ?? 80;

                Avalonia.Input.Pointer mousePointer = new(0, PointerType.Mouse, true);
                PointerPointProperties pressProps = new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
                PointerPressedEventArgs pressedArgs = new(
                    scroller,
                    mousePointer,
                    scroller,
                    new Avalonia.Point(10, targetY),
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
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            string doc = "# First Heading\n\nThis is a full paragraph with multiple words to test full block selection.\n\n```csharp\nint x = 42;\n```";

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(doc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);
            await WaitForPreviewAsync(session, editor);

            (string selectedParagraphText, string expectedParagraphText) = await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);

                Control paragraphBlock = editor.State.PreviewBlocks[1];
                BlockSourceSpan span = (BlockSourceSpan)paragraphBlock.Tag!;

                view.HandlePreviewBlockClicked(paragraphBlock, span, new Avalonia.Point(50, 10));
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



    private static EditorViewModel CreateEditorWithFile(string filePath, string content)
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddValeria(configuration);
        services.RemoveAll<IFilesDockService>();
        services.AddSingleton<IFilesDockService>(new FakeFilesDockService());
        services.RemoveAll<IEditorFileService>();
        services.AddSingleton<IEditorFileService>(new FakeEditorFileService(content));
        services.RemoveAll<IMarkdownPreviewBuilder>();
        services.AddSingleton<IMarkdownPreviewBuilder>(new FakeMarkdownPreviewBuilder());
        services.RemoveAll<ICodeSyntaxService>();
        services.AddSingleton<ICodeSyntaxService>(new FakeCodeSyntaxService());
        ServiceProvider provider = services.BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<EditorViewModel>(
            provider,
            new MainWindowViewModel(provider, filePath),
            filePath);
    }

    private static async Task<(Window Window, EditorViewModel Editor)> SetupEditorAsync(HeadlessUnitTestSession session)
    {
        (Window window, EditorViewModel editor) = await session.Dispatch(() =>
        {
            RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
            Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));

            IConfiguration configuration = new ConfigurationBuilder().Build();
            ServiceCollection services = new();
            services.AddValeria(configuration);
            services.RemoveAll<IFilesDockService>();
            services.AddSingleton<IFilesDockService>(new FakeFilesDockService());
            ServiceProvider provider = services.BuildServiceProvider();

            MainWindowViewModel shell = provider.GetRequiredService<MainWindowViewModel>();
            EditorViewModel editor = ActivatorUtilities.CreateInstance<EditorViewModel>(provider, shell, string.Empty);
            EditorView view = new() { ViewModel = editor };

            Window window = new() { Content = view, Width = 1280, Height = 800 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            return (window, editor);
        }, CancellationToken.None);

        await session.Dispatch(async () =>
        {
            await editor.InitializeAsync(CancellationToken.None);
            Dispatcher.UIThread.RunJobs();
        }, CancellationToken.None);

        return (window, editor);
    }

    private static EditorView RequireView(Window? window)
    {
        EditorView? view = window?.GetVisualDescendants().OfType<EditorView>().FirstOrDefault();

        if (view is null)
            throw new InvalidOperationException("EditorView was not created.");

        return view;
    }

    private static TextEditor RequireSourceEditor(EditorView view)
    {
        TextEditor? source = view.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault(editor => !editor.IsReadOnly);

        if (source is null)
            throw new InvalidOperationException("Editable source TextEditor was not found.");

        return source;
    }

    private sealed class FakeEditorFileService : IEditorFileService
    {
        private readonly string _content;

        public FakeEditorFileService(string content)
        {
            _content = content;
        }

        public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(_content);
        }

        public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string> LoadWelcomeDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_content);
        }
    }

    private sealed class FakeMarkdownPreviewBuilder : IMarkdownPreviewBuilder
    {
        public PreviewBuildResult BuildBlocks(
            MarkdownContent content,
            Action<int, bool>? onTaskToggled = null,
            string? baseDirectory = null)
        {
            return new PreviewBuildResult(Array.Empty<Control>(), ImmutableList<CodeHighlightTarget>.Empty);
        }

        public Task<PreviewBuildResult> BuildBlocksAsync(
            MarkdownContent content,
            Action<int, bool>? onTaskToggled = null,
            string? baseDirectory = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BuildBlocks(content, onTaskToggled, baseDirectory));
        }

        public void ApplyHighlight(CodeHighlightTarget target, IReadOnlyList<HighlightedLine> lines)
        {
        }
    }

    private sealed class FakeCodeSyntaxService : ICodeSyntaxService
    {
        public string? ResolveScope(string? language)
        {
            return null;
        }

        public IReadOnlyList<CodeLanguageSuggestion> GetLanguageSuggestions()
        {
            return Array.Empty<CodeLanguageSuggestion>();
        }

        public string GetLanguageDisplayName(string? language)
        {
            return string.Empty;
        }

        public Task<IReadOnlyList<HighlightedLine>> HighlightCodeAsync(string? language, string code, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<HighlightedLine>>(Array.Empty<HighlightedLine>());
        }

        public Task PrewarmAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private static async Task WaitForPreviewAsync(HeadlessUnitTestSession session, EditorViewModel editor, int timeoutMilliseconds = 4000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);

            bool isReady = await session.Dispatch(() =>
            {
                Dispatcher.UIThread.RunJobs();
                return editor.State.PreviewBlocks.Count > 0 && editor.State.IsPreviewIdle;
            }, CancellationToken.None);

            if (isReady)
                return;
        }

        await session.Dispatch(() => Dispatcher.UIThread.RunJobs(), CancellationToken.None);
    }

    private sealed class FakeFilesDockService : IFilesDockService
    {
        public Task<IReadOnlyList<FavoriteFile>> GetFavoritesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FavoriteFile>>(Array.Empty<FavoriteFile>());

        public Task<FavoriteFile> AddFavoriteAsync(string filePath, string customName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FavoriteFile(1, filePath, customName, DateTime.UtcNow, DateTime.UtcNow));

        public Task<bool> RemoveFavoriteAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> IsFavoriteAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task RecordFileOpenAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<RecentFile>> GetRecentFilesWithin24HoursAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RecentFile>>(Array.Empty<RecentFile>());

        public Task<RecentFile?> GetLastOpenedNonFavoriteAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<RecentFile?>(null);
    }
}
