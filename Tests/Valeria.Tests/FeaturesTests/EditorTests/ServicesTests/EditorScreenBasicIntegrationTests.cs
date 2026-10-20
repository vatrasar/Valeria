using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReactiveUI;
using Splat;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure;
using Valeria.Src.Infrastructure.Navigation;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorScreenBasicIntegrationTests
{
    [Fact]
    public async Task ViewModelText_PopulatesSourceEditorAndPreview()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                Assert.False(editor.State.IsEditorVisible);
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText("# Hi\n\nHello **bold**");
            }, CancellationToken.None);
            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (string viewModelText, string sourceText, int previewCount, bool hasItemsSource, string? error, double editorWidth, string titleLabel) =
                await session.Dispatch(() =>
                {
                    EditorView view = EditorScreenTestHelpers.RequireView(window);
                    TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
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
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                source.Text = "# typed\n\nsome text";
            }, CancellationToken.None);

            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

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
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

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
            EditorViewModel editor = EditorScreenTestHelpers.CreateEditorWithFile(filePath, expectedContent);
            await editor.InitializeAsync(CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(editor.State.ErrorMessage), $"ErrorMessage: {editor.State.ErrorMessage}");
            Assert.Equal(expectedContent, editor.State.MarkdownText);
            Assert.Equal(filePath, editor.State.FilePath);
        }, CancellationToken.None);
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

            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            await session.Dispatch(() =>
            {
                EditorView view1 = EditorScreenTestHelpers.RequireView(window);
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

                EditorView view2 = EditorScreenTestHelpers.RequireView(window);
                ItemsControl preview2 = view2.FindControl<ItemsControl>("PreviewBlocksControl")!;
                TextEditor source2 = EditorScreenTestHelpers.RequireSourceEditor(view2);

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
                EditorView view = EditorScreenTestHelpers.RequireView(window);
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
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string longDoc = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"# Section {i}\n\nContent for section {i} with additional text to expand height."));

            await session.Dispatch(() =>
            {
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText(longDoc);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await EditorScreenTestHelpers.WaitForPreviewAsync(session, editor);

            (double editorY, double previewY) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
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
    public async Task DeletingListItemInSourceEditor_RenumbersSubsequentListItemsAutomatically()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await EditorScreenTestHelpers.SetupEditorAsync(session);

            string initialText = "1. First\n2. Second\n3. Third\n4. Fourth\n5. Fifth\n6. Sixth\n7. Seventh";

            await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                source.Text = initialText;
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                int offset = source.Document.Text.IndexOf("5. Fifth\n", StringComparison.Ordinal);
                source.Document.Remove(offset, "5. Fifth\n".Length);
                Dispatcher.UIThread.RunJobs();
            }, CancellationToken.None);

            (string sourceText, string viewModelText) = await session.Dispatch(() =>
            {
                EditorView view = EditorScreenTestHelpers.RequireView(window);
                TextEditor source = EditorScreenTestHelpers.RequireSourceEditor(view);
                Dispatcher.UIThread.RunJobs();
                return (source.Text, editor.State.MarkdownText);
            }, CancellationToken.None);

            const string expected = "1. First\n2. Second\n3. Third\n4. Fourth\n5. Sixth\n6. Seventh";
            Assert.Equal(expected, sourceText);
            Assert.Equal(expected, viewModelText);
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
    public void EditorViewModel_RenumberOrderedListAfterDeletion_UpdatesState()
    {
        EditorViewModel editor = EditorScreenTestHelpers.CreateEditorWithFile(string.Empty, string.Empty);
        const string text = "1. A\n2. B\n3. C\n4. D\n6. F\n7. G";
        editor.SetMarkdownText(text);
        FormattingResult? result = editor.RenumberOrderedListAfterDeletion(text.IndexOf("6. F", StringComparison.Ordinal));

        Assert.NotNull(result);
        Assert.Equal("1. A\n2. B\n3. C\n4. D\n5. F\n6. G", editor.State.MarkdownText);
    }
}
