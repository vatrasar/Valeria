using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Core.Services;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen.ScreenComponents.FilesDock;
using Valeria.Src.Infrastructure.Services;
using Valeria.Src.Shared.Resources;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Coordinates document file operations (loading, saving, auto-saving) for EditorViewModel.
/// </summary>
public sealed class EditorDocumentCoordinator
{
    private readonly IEditorFileService _files;
    private readonly IFileDialogService _dialogs;
    private readonly ISettingsService _settings;
    private readonly FilesDockViewModel _filesDock;
    private readonly Func<EditorState> _getState;
    private readonly Action<Func<EditorState, EditorState>> _updateState;
    private readonly Func<Task> _triggerPreview;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private string _savedSnapshot = string.Empty;

    public string SavedSnapshot => _savedSnapshot;

    public EditorDocumentCoordinator(
        IEditorFileService files,
        IFileDialogService dialogs,
        ISettingsService settings,
        FilesDockViewModel filesDock,
        Func<EditorState> getState,
        Action<Func<EditorState, EditorState>> updateState,
        Func<Task> triggerPreview)
    {
        _files = files;
        _dialogs = dialogs;
        _settings = settings;
        _filesDock = filesDock;
        _getState = getState;
        _updateState = updateState;
        _triggerPreview = triggerPreview;
    }

    public async Task OpenFileAsync(CancellationToken cancellationToken)
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
            _updateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    public async Task OpenFileFromPathAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            string content = await _files.ReadTextAsync(path, cancellationToken);
            ApplyLoadedDocument(content, path);
        }
        catch (Exception exception)
        {
            _updateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    public async Task SaveFileAsync(CancellationToken cancellationToken)
    {
        EditorState state = _getState();
        if (state.FilePath is null)
        {
            await SaveFileAsAsync(cancellationToken);
            return;
        }

        await WriteToPathAsync(state.FilePath, cancellationToken);
    }

    public async Task SaveFileAsAsync(CancellationToken cancellationToken)
    {
        string? path = await _dialogs.PickMarkdownFileToSaveAsync(SuggestFileName(), cancellationToken);

        if (path is null)
            return;

        await WriteToPathAsync(path, cancellationToken, notifyDock: true);
    }

    public async Task ProcessAutoSaveAsync(CancellationToken cancellationToken)
    {
        if (!_settings.IsAutoSaveEnabled)
            return;

        EditorState state = _getState();
        if (!state.IsDirty || string.IsNullOrEmpty(state.FilePath))
            return;

        if (cancellationToken.IsCancellationRequested)
            return;

        await WriteToPathAsync(state.FilePath, cancellationToken);
    }

    public async Task LoadInitialDocumentAsync(string? initialFilePath, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(initialFilePath))
            {
                string welcome = await _files.LoadWelcomeDocumentAsync(cancellationToken);
                ApplyLoadedDocument(welcome, null);
                return;
            }

            string content = await _files.ReadTextAsync(initialFilePath, cancellationToken);
            ApplyLoadedDocument(content, initialFilePath);
        }
        catch (Exception exception)
        {
            _updateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    public void ApplyLoadedDocument(string content, string? path)
    {
        _savedSnapshot = content ?? string.Empty;

        _updateState(state => state with
        {
            MarkdownText = _savedSnapshot,
            FilePath = path,
            IsDirty = false,
            WordCount = MarkdownStats.CountWords(_savedSnapshot),
            DocumentTitle = BuildDocumentTitle(path, false),
            DocumentPath = BuildDocumentPath(path, false),
            ErrorMessage = null
        });

        _ = _filesDock.NotifyFileOpenedAsync(path);
        _ = _triggerPreview();
    }

    public bool IsDirty(string text)
    {
        return !string.Equals(text, _savedSnapshot, StringComparison.Ordinal);
    }

    public static string BuildDocumentTitle(string? path, bool isDirty)
    {
        string name = path is null ? GlobalStrings.UntitledDocument : Path.GetFileName(path);

        if (string.IsNullOrEmpty(name))
            name = GlobalStrings.UntitledDocument;

        return isDirty ? name + "*" : name;
    }

    public static string BuildDocumentPath(string? path, bool isDirty)
    {
        if (string.IsNullOrWhiteSpace(path))
            return isDirty ? GlobalStrings.UntitledDocument + "*" : GlobalStrings.UntitledDocument;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            fullPath = path;
        }

        return isDirty ? fullPath + "*" : fullPath;
    }

    private async Task WriteToPathAsync(string path, CancellationToken cancellationToken, bool notifyDock = false)
    {
        await _saveGate.WaitAsync(cancellationToken);

        try
        {
            string contentToSave = _getState().MarkdownText;
            await _files.WriteTextAsync(path, contentToSave, cancellationToken);
            _savedSnapshot = contentToSave;

            bool isStillDirty = !string.Equals(_getState().MarkdownText, contentToSave, StringComparison.Ordinal);

            await RunOnUiThread(() =>
            {
                _updateState(state => state with
                {
                    FilePath = path,
                    IsDirty = isStillDirty,
                    DocumentTitle = BuildDocumentTitle(path, isStillDirty),
                    DocumentPath = BuildDocumentPath(path, isStillDirty),
                    ErrorMessage = null
                });
            });

            if (notifyDock)
            {
                await RunOnUiThread(() => _ = _filesDock.NotifyFileOpenedAsync(path));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RunOnUiThread(() => _updateState(state => state with { ErrorMessage = exception.Message }));
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private string SuggestFileName()
    {
        string? currentPath = _getState().FilePath;
        if (currentPath is not null)
            return Path.GetFileName(currentPath);

        return "untitled.md";
    }

    private static async Task RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal);
    }
}
