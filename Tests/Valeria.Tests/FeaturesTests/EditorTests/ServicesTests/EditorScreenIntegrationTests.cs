using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
                ServiceProvider provider = new ServiceCollection().AddValeria(configuration).BuildServiceProvider();

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

    private static EditorViewModel CreateEditorWithFile(string filePath, string content)
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddValeria(configuration);
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
            ServiceProvider provider = new ServiceCollection().AddValeria(configuration).BuildServiceProvider();

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
}
