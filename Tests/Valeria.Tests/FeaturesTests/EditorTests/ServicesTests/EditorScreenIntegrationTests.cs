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
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure;
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
            await Task.Delay(PreviewWaitMilliseconds);

            (string viewModelText, string sourceText, int previewCount, string? error, double editorWidth, string titleLabel) =
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
                        preview.ItemCount,
                        editor.State.ErrorMessage,
                        source.Bounds.Width,
                        title.Text ?? string.Empty);
                }, CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(error), $"ErrorMessage: {error}");
            Assert.True(titleLabel.Length > 0, "Status bar was never rendered, view activation did not run.");
            Assert.True(viewModelText.Contains("# Hi"), $"viewModelText was '{viewModelText}'");
            Assert.True(sourceText.Contains("# Hi"), $"sourceText was '{sourceText}'");
            Assert.True(previewCount > 0);
            Assert.True(editorWidth > 100);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() => window.Close(), CancellationToken.None);
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

            await Task.Delay(PreviewWaitMilliseconds);

            (string viewModelText, int previewCount) = await session.Dispatch(() =>
                (editor.State.MarkdownText, editor.State.PreviewBlocks.Count), CancellationToken.None);

            Assert.Contains("typed", viewModelText);
            Assert.True(previewCount > 0);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() => window.Close(), CancellationToken.None);
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
                await session.Dispatch(() => window.Close(), CancellationToken.None);
        }
    }

    [Fact]
    public async Task InitializeAsync_ForCommandLineFilePath_LoadsFileContent()
    {
        IScheduler originalScheduler = RxApp.MainThreadScheduler;
        string filePath = Path.Combine(Path.GetTempPath(), "valeria-command-line-test.md");
        const string expectedContent = "# Z pliku\n\nWczytane z argumentu.";

        try
        {
            RxApp.MainThreadScheduler = CurrentThreadScheduler.Instance;

            EditorViewModel editor = CreateEditorWithFile(filePath, expectedContent);

            await editor.InitializeAsync(CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(editor.State.ErrorMessage), $"ErrorMessage: {editor.State.ErrorMessage}");
            Assert.Equal(expectedContent, editor.State.MarkdownText);
            Assert.Equal(filePath, editor.State.FilePath);
        }
        finally
        {
            RxApp.MainThreadScheduler = originalScheduler;
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
        return await session.Dispatch(() =>
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
        public PreviewBuildResult BuildBlocks(MarkdownContent content)
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
}
