using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Core.Mvvm;
using Valeria.Src.Core.Services;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen.ScreenComponents.FilesDock;
using Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;
using Valeria.Src.Infrastructure.Services;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// View model of the split markdown editor screen with live styled preview.
/// Preview rebuilds are debounced and cancelled while typing; code blocks
/// are highlighted in the background so typing is never blocked.
/// </summary>
public partial class EditorViewModel : ViewModelBase<EditorState>, IRoutableViewModel
{
    private readonly IEditorFileService _files;
    private readonly ICodeSyntaxService _syntax;
    private readonly IFileDialogService _dialogs;
    private readonly ISettingsService _settings;
    private readonly string _initialFilePath;
    private readonly EditorDocumentCoordinator _documentCoordinator;
    private readonly EditorPreviewCoordinator _previewCoordinator;
    private readonly EditorFormatHandler _formatHandler;
    private bool _isInitialized;
    private bool _isPrewarmed;

    public string? UrlPathSegment => "editor";

    public IScreen HostScreen { get; }

    public double EditorFontSize => State.EditorFontSize;

    public int EditorTabWidth => State.EditorTabWidth;

    public int PreviewMaxWidth { get; }

    public IReadOnlyList<CodeLanguageSuggestion> CodeLanguageSuggestions => _syntax.GetLanguageSuggestions();

    public FilesDockViewModel FilesDock { get; }

    public EditorViewModel(
        IScreen hostScreen,
        IEditorFileService files,
        IMarkdownPreviewBuilder previewBuilder,
        ICodeSyntaxService syntax,
        IFileDialogService dialogs,
        ISettingsService settings,
        IOptions<AppConfig> config,
        string initialFilePath,
        IFilesDockService? filesDockService = null)
        : base(new EditorState
        {
            EditorFontSize = settings.EditorFontSize,
            EditorTabWidth = settings.EditorTabWidth
        })
    {
        HostScreen = hostScreen;
        _files = files;
        _syntax = syntax;
        _dialogs = dialogs;
        _settings = settings;
        _initialFilePath = initialFilePath;
        PreviewMaxWidth = config.Value.Preview.MaxWidth;

        FilesDock = new FilesDockViewModel(
            filesDockService ?? new NullFilesDockService(),
            isDockExpanded: string.IsNullOrEmpty(initialFilePath));

        _formatHandler = new EditorFormatHandler(
            () => State.MarkdownText,
            SetMarkdownText);

        _previewCoordinator = new EditorPreviewCoordinator(
            previewBuilder,
            syntax,
            () => State,
            UpdateState,
            _formatHandler.ToggleTask);

        _documentCoordinator = new EditorDocumentCoordinator(
            files,
            dialogs,
            settings,
            FilesDock,
            () => State,
            UpdateState,
            () => _previewCoordinator.ProcessPreviewAsync(CancellationToken.None));

        FilesDock.FileSelected
            .Select(path => Observable.FromAsync(ct => _documentCoordinator.OpenFileFromPathAsync(path, ct)))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);

        _settings.EditorFontSizeObservable
            .DistinctUntilChanged()
            .Subscribe(fontSize => UpdateState(state => state with { EditorFontSize = fontSize }))
            .DisposeWith(Disposables);

        _settings.EditorTabWidthObservable
            .DistinctUntilChanged()
            .Subscribe(tabWidth => UpdateState(state => state with { EditorTabWidth = tabWidth }))
            .DisposeWith(Disposables);

        _settings.HeadingPaletteObservable
            .DistinctUntilChanged()
            .Do(_ => _previewCoordinator.InvalidateContainers())
            .Select(_ => Observable.FromAsync(_previewCoordinator.ProcessPreviewAsync))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.State.MarkdownText)
            .Throttle(TimeSpan.FromMilliseconds(config.Value.Editor.PreviewDebounceMilliseconds), RxApp.TaskpoolScheduler)
            .Select(_ => Observable.FromAsync(_previewCoordinator.ProcessPreviewAsync))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);

        this.WhenAnyValue(viewModel => viewModel.State.MarkdownText)
            .Select(_ => Observable.Timer(TimeSpan.FromSeconds(_settings.AutoSaveDelaySeconds), RxApp.TaskpoolScheduler))
            .Switch()
            .Select(_ => Observable.FromAsync(_documentCoordinator.ProcessAutoSaveAsync))
            .Switch()
            .Subscribe()
            .DisposeWith(Disposables);
    }

    /// <summary>
    /// Registers the window provider used by native file dialogs.
    /// Invoked by EditorView on activation.
    /// </summary>
    public void SetTopLevelProvider(Func<TopLevel?> provider)
    {
        _dialogs.SetTopLevelProvider(provider);
    }

    /// <summary>
    /// Loads the initial document on first activation when empty, either the
    /// startup file passed on the command line or the bundled welcome document.
    /// Invoked once by EditorView code-behind.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        PrewarmHighlighter(cancellationToken);
        await FilesDock.RefreshAsync(cancellationToken);

        if (_isInitialized)
            return;

        _isInitialized = true;

        if (!string.IsNullOrEmpty(State.MarkdownText))
            return;

        await _documentCoordinator.LoadInitialDocumentAsync(_initialFilePath, cancellationToken);
    }

    /// <summary>
    /// Replaces the whole document text, updating dirty flag, title and word count.
    /// Invoked by EditorView when the AvaloniaEdit content changes.
    /// </summary>
    private CancellationTokenSource? _wordCountCts;

    /// <summary>
    /// Updates the visible range of chunks in the preview panel based on viewport scrolling.
    /// Invoked by EditorView when the preview scroll viewer scrolls.
    /// </summary>
    public void UpdatePreviewViewport(double viewportY, double viewportHeight)
    {
        _previewCoordinator.UpdateVisibleChunks(viewportY, viewportHeight);
    }

    /// <summary>
    /// Replaces the whole document text, updating dirty flag, title and word count.
    /// Invoked by EditorView when the AvaloniaEdit content changes.
    /// </summary>
    public void SetMarkdownText(string text)
    {
        string safeText = text ?? string.Empty;
        bool isDirty = _documentCoordinator.IsDirty(safeText);
        bool isLargeText = safeText.Length >= 50_000;

        int wordCount = isLargeText ? State.WordCount : MarkdownStats.CountWords(safeText);

        UpdateState(state => state with
        {
            MarkdownText = safeText,
            IsDirty = isDirty,
            WordCount = wordCount,
            DocumentTitle = EditorDocumentCoordinator.BuildDocumentTitle(state.FilePath, isDirty),
            DocumentPath = EditorDocumentCoordinator.BuildDocumentPath(state.FilePath, isDirty),
            ErrorMessage = null
        });

        if (isLargeText)
            ScheduleBackgroundWordCount(safeText);
    }

    private void ScheduleBackgroundWordCount(string text)
    {
        _wordCountCts?.Cancel();
        _wordCountCts?.Dispose();
        _wordCountCts = new CancellationTokenSource();
        CancellationToken token = _wordCountCts.Token;

        _ = Task.Run(() =>
        {
            int count = MarkdownStats.CountWords(text);
            if (!token.IsCancellationRequested)
                RxApp.MainThreadScheduler.Schedule(() => UpdateState(state => state with { WordCount = count }));
        }, token);
    }

    /// <summary>
    /// Updates the caret position shown in the status bar.
    /// Invoked by EditorView on caret changes.
    /// </summary>
    public void UpdateCaret(int line, int column)
    {
        UpdateState(state => state with { CaretLine = line, CaretColumn = column });
    }

    /// <summary>
    /// Opens a markdown file from the specified path into the editor.
    /// Invoked by FilesDock when a user selects a favorite or recent file.
    /// </summary>
    public Task OpenFileFromPathAsync(string path, CancellationToken cancellationToken)
    {
        return _documentCoordinator.OpenFileFromPathAsync(path, cancellationToken);
    }

    /// <summary>
    /// Updates one task-list marker after a checkbox is toggled in the preview.
    /// </summary>
    public void ToggleTask(int taskIndex, bool isChecked) => _formatHandler.ToggleTask(taskIndex, isChecked);

    /// <summary>
    /// Toggles inline wrapping and returns the editor selection to apply.
    /// </summary>
    public FormattingResult ApplyInlineWrap(string marker, string placeholder, int selectionStart, int selectionLength) =>
        _formatHandler.ApplyInlineWrap(marker, placeholder, selectionStart, selectionLength);

    /// <summary>
    /// Applies link formatting and returns the editor selection to apply.
    /// </summary>
    public FormattingResult ApplyLink(int selectionStart, int selectionLength) =>
        _formatHandler.ApplyLink(selectionStart, selectionLength, Resources.EditorStrings.PlaceholderLinkText);

    /// <summary>
    /// Toggles a heading level and returns the editor selection to apply.
    /// </summary>
    public FormattingResult ApplyHeading(int level, int selectionStart, int selectionLength) =>
        _formatHandler.ApplyHeading(level, selectionStart, selectionLength);

    /// <summary>
    /// Toggles a bullet list and returns the editor selection to apply.
    /// </summary>
    public FormattingResult ApplyBulletList(int selectionStart, int selectionLength) =>
        _formatHandler.ApplyBulletList(selectionStart, selectionLength);

    /// <summary>
    /// Toggles a numbered list and returns the editor selection to apply.
    /// </summary>
    public FormattingResult ApplyOrderedList(int selectionStart, int selectionLength) =>
        _formatHandler.ApplyOrderedList(selectionStart, selectionLength);

    /// <summary>
    /// Toggles a quote and returns the editor selection to apply.
    /// </summary>
    public FormattingResult ApplyQuote(int selectionStart, int selectionLength) =>
        _formatHandler.ApplyQuote(selectionStart, selectionLength);

    /// <summary>
    /// Inserts a code fence and returns the caret to apply.
    /// </summary>
    public FormattingResult ApplyCodeFence(int selectionStart, int selectionLength) =>
        _formatHandler.ApplyCodeFence(selectionStart, selectionLength);

    /// <summary>
    /// Inserts a markdown table template at the selection.
    /// </summary>
    public FormattingResult ApplyTable(int caretOffset) =>
        _formatHandler.ApplyTable(caretOffset);

    /// <summary>
    /// Continues an existing list on Enter.
    /// </summary>
    public FormattingResult? ContinueList(int caretOffset) =>
        _formatHandler.ContinueList(caretOffset);

    /// <summary>
    /// Renumbers subsequent list items in an ordered list after an item was deleted at the offset.
    /// </summary>
    public FormattingResult? RenumberOrderedListAfterDeletion(int offset) =>
        _formatHandler.RenumberOrderedListAfterDeletion(offset);

    [ReactiveCommand]
    private Task OpenFile(CancellationToken cancellationToken) => _documentCoordinator.OpenFileAsync(cancellationToken);

    [ReactiveCommand]
    private Task SaveFile(CancellationToken cancellationToken) => _documentCoordinator.SaveFileAsync(cancellationToken);

    [ReactiveCommand]
    private Task SaveFileAs(CancellationToken cancellationToken) => _documentCoordinator.SaveFileAsAsync(cancellationToken);

    [ReactiveCommand]
    private void ToggleEditor()
    {
        UpdateState(state => state with { IsEditorVisible = !state.IsEditorVisible });
    }

    [ReactiveCommand]
    private void NavigateToSettings()
    {
        SettingsViewModel settings = new(HostScreen, _settings);
        HostScreen.Router.Navigate.Execute(settings).Subscribe();
    }

    [ReactiveCommand]
    private void OpenPreviewSearch()
    {
        UpdateState(state => state with { IsPreviewSearchOpen = true });
    }

    [ReactiveCommand]
    private void ClosePreviewSearch()
    {
        UpdateState(state => state with
        {
            IsPreviewSearchOpen = false,
            PreviewSearchQuery = string.Empty,
            PreviewSearchMatchIndex = 0,
            PreviewSearchMatchCount = 0
        });
    }

    [ReactiveCommand]
    private void TogglePreviewSearchMatchCase()
    {
        UpdateState(state => state with { PreviewSearchMatchCase = !state.PreviewSearchMatchCase });
    }

    /// <summary>
    /// Updates the preview search query and resets match indices.
    /// Invoked by EditorView when the search text box input changes.
    /// </summary>
    public void SetPreviewSearchQuery(string query)
    {
        UpdateState(state => state with
        {
            PreviewSearchQuery = query ?? string.Empty
        });
    }

    /// <summary>
    /// Updates the match counter state based on active search results.
    /// Invoked by EditorView after search or navigation completes.
    /// </summary>
    public void UpdatePreviewSearchResults(int currentIndex, int totalMatches)
    {
        UpdateState(state => state with
        {
            PreviewSearchMatchIndex = currentIndex,
            PreviewSearchMatchCount = totalMatches
        });
    }

    private void PrewarmHighlighter(CancellationToken cancellationToken)
    {
        if (_isPrewarmed)
            return;

        _isPrewarmed = true;
        _ = _syntax.PrewarmAsync(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _wordCountCts?.Cancel();
            _wordCountCts?.Dispose();
            _wordCountCts = null;
        }

        base.Dispose(disposing);
    }
}
