using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

internal static class EditorScreenTestHelpers
{
    public const int PreviewWaitMilliseconds = 1200;

    public static EditorViewModel CreateEditorWithFile(string filePath, string content)
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

    public static async Task<(Window Window, EditorViewModel Editor)> SetupEditorAsync(HeadlessUnitTestSession session)
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

    public static EditorView RequireView(Window? window)
    {
        EditorView? view = window?.GetVisualDescendants().OfType<EditorView>().FirstOrDefault();

        if (view is null)
            throw new InvalidOperationException("EditorView was not created.");

        return view;
    }

    public static TextEditor RequireSourceEditor(EditorView view)
    {
        TextEditor? source = view.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault(editor => !editor.IsReadOnly);

        if (source is null)
            throw new InvalidOperationException("Editable source TextEditor was not found.");

        return source;
    }

    public static async Task WaitForPreviewAsync(HeadlessUnitTestSession session, EditorViewModel editor, int timeoutMilliseconds = 4000)
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

internal sealed class FakeEditorFileService : IEditorFileService
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

internal sealed class FakeMarkdownPreviewBuilder : IMarkdownPreviewBuilder
{
    public PreviewBuildResult BuildBlocks(
        MarkdownContent content,
        Action<int, bool>? onTaskToggled = null,
        string? baseDirectory = null)
    {
        return new PreviewBuildResult(Array.Empty<Control>(), ImmutableList<CodeHighlightTarget>.Empty);
    }

    public PreviewBuildResult BuildChunkBlocks(
        MarkdownChunk chunk,
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

internal sealed class FakeCodeSyntaxService : ICodeSyntaxService
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

internal sealed class FakeFilesDockService : IFilesDockService
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
