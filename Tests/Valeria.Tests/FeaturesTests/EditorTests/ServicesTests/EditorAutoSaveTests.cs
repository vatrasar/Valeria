using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.Options;
using ReactiveUI;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Core.Services;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;
using Valeria.Src.Infrastructure.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorAutoSaveTests
{
    [Fact]
    public async Task AutoSave_WhenEnabledAndFileHasPath_SavesChangesAutomatically()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        FakeEditorFileService files = new("initial content");
        FakeSettingsService settings = new(true);
        EditorViewModel editor = CreateEditor(files, settings, "/path/to/test.md");

        await session.Dispatch(async () =>
        {
            await editor.InitializeAsync(CancellationToken.None);
            editor.SetMarkdownText("updated content");
        }, CancellationToken.None);

        Assert.True(editor.State.IsDirty);

        await WaitForConditionAsync(session, () => !editor.State.IsDirty && files.WrittenContent == "updated content", 3000);

        Assert.False(editor.State.IsDirty);
        Assert.Equal("updated content", files.WrittenContent);
        Assert.Equal("/path/to/test.md", files.WrittenPath);
    }

    [Fact]
    public async Task AutoSave_WhenDisabled_DoesNotSaveFileAutomatically()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        FakeEditorFileService files = new("initial content");
        FakeSettingsService settings = new(false);
        EditorViewModel editor = CreateEditor(files, settings, "/path/to/test.md");

        await session.Dispatch(async () =>
        {
            await editor.InitializeAsync(CancellationToken.None);
            editor.SetMarkdownText("updated content");
        }, CancellationToken.None);

        Assert.True(editor.State.IsDirty);

        await Task.Delay(400);

        await session.Dispatch(() =>
        {
            Dispatcher.UIThread.RunJobs();
            Assert.True(editor.State.IsDirty);
            Assert.Null(files.WrittenContent);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task AutoSave_WhenFilePathIsNull_DoesNotSaveFileAutomatically()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        FakeEditorFileService files = new("welcome content");
        FakeSettingsService settings = new(true);
        EditorViewModel editor = CreateEditor(files, settings, initialFilePath: string.Empty);

        await session.Dispatch(async () =>
        {
            await editor.InitializeAsync(CancellationToken.None);
            editor.SetMarkdownText("typed text on untitled document");
        }, CancellationToken.None);

        Assert.True(editor.State.IsDirty);
        Assert.Null(editor.State.FilePath);

        await Task.Delay(400);

        await session.Dispatch(() =>
        {
            Dispatcher.UIThread.RunJobs();
            Assert.True(editor.State.IsDirty);
            Assert.Null(files.WrittenContent);
        }, CancellationToken.None);
    }

    [Fact]
    public void NavigateToSettings_WhenExecuted_NavigatesRouterToSettingsViewModel()
    {
        FakeEditorFileService files = new("content");
        FakeSettingsService settings = new(true);
        TestScreen hostScreen = new();
        EditorViewModel editor = CreateEditor(files, settings, "/path/to/file.md", hostScreen);

        editor.NavigateToSettingsCommand.Execute().Subscribe();

        IRoutableViewModel? currentViewModel = hostScreen.Router.GetCurrentViewModel();
        Assert.NotNull(currentViewModel);
        Assert.IsType<SettingsViewModel>(currentViewModel);
    }

    private static EditorViewModel CreateEditor(
        FakeEditorFileService files,
        FakeSettingsService settings,
        string initialFilePath,
        IScreen? hostScreen = null)
    {
        IOptions<AppConfig> config = Options.Create(new AppConfig
        {
            Editor = new EditorOptions
            {
                PreviewDebounceMilliseconds = 50,
                AutoSave = settings.IsAutoSaveEnabled,
                AutoSaveDelayMilliseconds = 150
            }
        });

        return new EditorViewModel(
            hostScreen ?? new TestScreen(),
            files,
            new FakePreviewBuilder(),
            new FakeSyntaxService(),
            new FakeFileDialogService(),
            settings,
            config,
            initialFilePath);
    }

    private static async Task WaitForConditionAsync(HeadlessUnitTestSession session, Func<bool> condition, int timeoutMilliseconds)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

        while (DateTime.UtcNow < deadline)
        {
            bool satisfied = await session.Dispatch(() =>
            {
                Dispatcher.UIThread.RunJobs();
                return condition();
            }, CancellationToken.None);

            if (satisfied)
                return;

            await Task.Delay(30);
        }

        await session.Dispatch(() =>
        {
            Dispatcher.UIThread.RunJobs();
            Assert.True(condition());
        }, CancellationToken.None);
    }

    private sealed class TestScreen : IScreen
    {
        public RoutingState Router { get; } = new();
    }

    private sealed class FakeEditorFileService : IEditorFileService
    {
        private readonly string _initialContent;

        public string? WrittenPath { get; private set; }

        public string? WrittenContent { get; private set; }

        public FakeEditorFileService(string initialContent)
        {
            _initialContent = initialContent;
        }

        public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(_initialContent);
        }

        public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
        {
            WrittenPath = path;
            WrittenContent = content;
            return Task.CompletedTask;
        }

        public Task<string> LoadWelcomeDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_initialContent);
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly BehaviorSubject<bool> _subject;
        private readonly BehaviorSubject<int> _delaySubject;
        private readonly BehaviorSubject<double> _fontSubject;
        private readonly BehaviorSubject<int> _tabSubject;

        public bool IsAutoSaveEnabled => _subject.Value;
        public IObservable<bool> AutoSaveEnabledObservable => _subject;

        public int AutoSaveDelaySeconds => _delaySubject.Value;
        public IObservable<int> AutoSaveDelaySecondsObservable => _delaySubject;

        public double EditorFontSize => _fontSubject.Value;
        public IObservable<double> EditorFontSizeObservable => _fontSubject;

        public int EditorTabWidth => _tabSubject.Value;
        public IObservable<int> EditorTabWidthObservable => _tabSubject;

        public FakeSettingsService(bool initialAutoSave, int initialDelay = 0, double initialFont = 14, int initialTab = 4)
        {
            _subject = new BehaviorSubject<bool>(initialAutoSave);
            _delaySubject = new BehaviorSubject<int>(initialDelay);
            _fontSubject = new BehaviorSubject<double>(initialFont);
            _tabSubject = new BehaviorSubject<int>(initialTab);
        }

        public void SetAutoSaveEnabled(bool enabled) => _subject.OnNext(enabled);

        public void SetAutoSaveDelaySeconds(int delaySeconds) => _delaySubject.OnNext(delaySeconds);

        public void SetEditorFontSize(double fontSize) => _fontSubject.OnNext(fontSize);

        public void SetEditorTabWidth(int tabWidth) => _tabSubject.OnNext(tabWidth);
    }

    private sealed class FakePreviewBuilder : IMarkdownPreviewBuilder
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

    private sealed class FakeSyntaxService : ICodeSyntaxService
    {
        public string? ResolveScope(string? language) => null;

        public IReadOnlyList<CodeLanguageSuggestion> GetLanguageSuggestions() => Array.Empty<CodeLanguageSuggestion>();

        public string GetLanguageDisplayName(string? language) => string.Empty;

        public Task<IReadOnlyList<HighlightedLine>> HighlightCodeAsync(string? language, string code, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HighlightedLine>>(Array.Empty<HighlightedLine>());

        public Task PrewarmAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public void SetTopLevelProvider(Func<TopLevel?> provider)
        {
        }

        public Task<string?> PickMarkdownFileToOpenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<string?> PickMarkdownFileToSaveAsync(string? suggestedFileName, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }
}
