using System;
using System.Collections.Immutable;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.Options;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using NewMarkText.Src.Core.Config;
using NewMarkText.Src.Core.Markdown;
using NewMarkText.Src.Core.Mvvm;
using NewMarkText.Src.Features.Editor.Domain.Services;
using NewMarkText.Src.Infrastructure.Services;
using NewMarkText.Src.Shared.Resources;

namespace NewMarkText.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// View model of the split markdown editor screen with live MarkText styled preview.
/// </summary>
public partial class EditorViewModel : ViewModelBase<EditorState>, IRoutableViewModel
{
    private readonly IEditorFileService _files;
    private readonly IMarkdownPreviewBuilder _previewBuilder;
    private readonly IFileDialogService _dialogs;
    private readonly int _previewDebounceMilliseconds;
    private string _savedSnapshot = string.Empty;
    private bool _isInitialized;

    public string? UrlPathSegment => "editor";

    public IScreen HostScreen { get; }

    public double EditorFontSize { get; }

    public int EditorTabWidth { get; }

    public int PreviewMaxWidth { get; }

    public EditorViewModel(
        IScreen hostScreen,
        IEditorFileService files,
        IMarkdownPreviewBuilder previewBuilder,
        IFileDialogService dialogs,
        IOptions<AppConfig> config)
        : base(new EditorState())
    {
        HostScreen = hostScreen;
        _files = files;
        _previewBuilder = previewBuilder;
        _dialogs = dialogs;
        _previewDebounceMilliseconds = config.Value.Editor.PreviewDebounceMilliseconds;
        EditorFontSize = config.Value.Editor.FontSize;
        EditorTabWidth = config.Value.Editor.TabWidth;
        PreviewMaxWidth = config.Value.Preview.MaxWidth;

        this.WhenAnyValue(viewModel => viewModel.State.MarkdownText)
            .Throttle(TimeSpan.FromMilliseconds(_previewDebounceMilliseconds), RxApp.TaskpoolScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => RefreshPreview())
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
    /// Loads the bundled welcome document on first activation when empty.
    /// Invoked once by EditorView code-behind.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        if (!string.IsNullOrEmpty(State.MarkdownText))
        {
            RefreshPreview();
            return;
        }

        try
        {
            string welcome = await _files.LoadWelcomeDocumentAsync(cancellationToken);
            ApplyLoadedDocument(welcome, null);
        }
        catch (Exception exception)
        {
            UpdateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    /// <summary>
    /// Replaces the whole document text, updating dirty flag, title and word count.
    /// Invoked by EditorView when the AvaloniaEdit content changes.
    /// </summary>
    public void SetMarkdownText(string text)
    {
        string safeText = text ?? string.Empty;

        UpdateState(state => state with
        {
            MarkdownText = safeText,
            IsDirty = !string.Equals(safeText, _savedSnapshot, StringComparison.Ordinal),
            WordCount = MarkdownStats.CountWords(safeText),
            DocumentTitle = BuildDocumentTitle(state.FilePath, !string.Equals(safeText, _savedSnapshot, StringComparison.Ordinal)),
            ErrorMessage = null
        });
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
    /// Toggles inline wrapping and returns the editor selection to apply.
    /// Invoked by EditorView formatting toolbar buttons.
    /// </summary>
    public FormattingResult ApplyInlineWrap(string marker, string placeholder, int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleWrap(State.MarkdownText, selectionStart, selectionLength, marker, placeholder);
        SetMarkdownText(result.Text);

        return result;
    }

    /// <summary>
    /// Applies link formatting and returns the editor selection to apply.
    /// Invoked by EditorView link toolbar button.
    /// </summary>
    public FormattingResult ApplyLink(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.WrapLink(State.MarkdownText, selectionStart, selectionLength, Resources.EditorStrings.PlaceholderLinkText);
        SetMarkdownText(result.Text);

        return result;
    }

    /// <summary>
    /// Toggles a heading level and returns the editor selection to apply.
    /// Invoked by EditorView heading toolbar buttons.
    /// </summary>
    public FormattingResult ApplyHeading(int level, int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleHeading(State.MarkdownText, selectionStart, selectionLength, level);
        SetMarkdownText(result.Text);

        return result;
    }

    /// <summary>
    /// Toggles a bullet list and returns the editor selection to apply.
    /// Invoked by EditorView bullet list toolbar button.
    /// </summary>
    public FormattingResult ApplyBulletList(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleBulletList(State.MarkdownText, selectionStart, selectionLength);
        SetMarkdownText(result.Text);

        return result;
    }

    /// <summary>
    /// Toggles a numbered list and returns the editor selection to apply.
    /// Invoked by EditorView numbered list toolbar button.
    /// </summary>
    public FormattingResult ApplyOrderedList(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleOrderedList(State.MarkdownText, selectionStart, selectionLength);
        SetMarkdownText(result.Text);

        return result;
    }

    /// <summary>
    /// Toggles a quote and returns the editor selection to apply.
    /// Invoked by EditorView quote toolbar button.
    /// </summary>
    public FormattingResult ApplyQuote(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleQuote(State.MarkdownText, selectionStart, selectionLength);
        SetMarkdownText(result.Text);

        return result;
    }

    /// <summary>
    /// Inserts a code fence and returns the caret to apply.
    /// Invoked by EditorView code block toolbar button.
    /// </summary>
    public FormattingResult ApplyCodeFence(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.InsertCodeFence(State.MarkdownText, selectionStart, selectionLength, string.Empty);
        SetMarkdownText(result.Text);

        return result;
    }

    /// <summary>
    /// Inserts a starter table and returns the caret to apply.
    /// Invoked by EditorView table toolbar button.
    /// </summary>
    public FormattingResult ApplyTable(int caretOffset)
    {
        FormattingResult result = EditorFormatting.InsertTable(State.MarkdownText, caretOffset);
        SetMarkdownText(result.Text);

        return result;
    }

    [ReactiveCommand]
    private async Task OpenFile(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickMarkdownFileToOpenAsync(cancellationToken);

        if (path is null)
            return;

        try
        {
            string content = await _files.ReadTextAsync(path, cancellationToken);
            ApplyLoadedDocument(content, path);
        }
        catch (Exception exception)
        {
            UpdateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    [ReactiveCommand]
    private async Task SaveFile(CancellationToken cancellationToken)
    {
        if (State.FilePath is null)
        {
            await SaveFileAs(cancellationToken);
            return;
        }

        await WriteToPath(State.FilePath, cancellationToken);
    }

    [ReactiveCommand]
    private async Task SaveFileAs(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickMarkdownFileToSaveAsync(SuggestFileName(), cancellationToken);

        if (path is null)
            return;

        await WriteToPath(path, cancellationToken);
    }

    [ReactiveCommand]
    private void TogglePreview()
    {
        UpdateState(state => state with { IsPreviewVisible = !state.IsPreviewVisible });
    }

    private async Task WriteToPath(string path, CancellationToken cancellationToken)
    {
        try
        {
            await _files.WriteTextAsync(path, State.MarkdownText, cancellationToken);
            _savedSnapshot = State.MarkdownText;

            UpdateState(state => state with
            {
                FilePath = path,
                IsDirty = false,
                DocumentTitle = BuildDocumentTitle(path, false),
                ErrorMessage = null
            });
        }
        catch (Exception exception)
        {
            UpdateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    private void ApplyLoadedDocument(string content, string? path)
    {
        _savedSnapshot = content ?? string.Empty;

        UpdateState(state => state with
        {
            MarkdownText = _savedSnapshot,
            FilePath = path,
            IsDirty = false,
            WordCount = MarkdownStats.CountWords(_savedSnapshot),
            DocumentTitle = BuildDocumentTitle(path, false),
            ErrorMessage = null
        });

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        try
        {
            MarkdownContent content = MarkdownParser.Parse(State.MarkdownText);

            UpdateState(state => state with
            {
                PreviewBlocks = ImmutableList.CreateRange(_previewBuilder.BuildBlocks(content))
            });
        }
        catch (Exception exception)
        {
            UpdateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    private static string BuildDocumentTitle(string? path, bool isDirty)
    {
        string name = path is null ? GlobalStrings.UntitledDocument : Path.GetFileName(path);

        if (string.IsNullOrEmpty(name))
            name = GlobalStrings.UntitledDocument;

        return isDirty ? name + "*" : name;
    }

    private string SuggestFileName()
    {
        if (State.FilePath is not null)
            return Path.GetFileName(State.FilePath);

        return "untitled.md";
    }
}
