using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Valeria.Src.Core.Domain;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Core.Mvvm;
using Valeria.Src.Features.Editor.Domain.Enums;
using Valeria.Src.Features.Editor.Domain.Services;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen.ScreenComponents.FilesDock;

/// <summary>
/// Smart component view model managing favorites, recent files, mode switches, and inline adding.
/// </summary>
public partial class FilesDockViewModel : ViewModelBase<FilesDockState>
{
    private readonly IFilesDockService _filesDockService;
    private readonly Subject<string> _fileSelectedSubject = new();

    public IObservable<string> FileSelected => _fileSelectedSubject;

    public FilesDockViewModel(IFilesDockService filesDockService, bool isDockExpanded = true)
        : base(new FilesDockState { IsDockExpanded = isDockExpanded })
    {
        _filesDockService = filesDockService;
    }

    /// <summary>
    /// Refreshes favorites, recent files, and the top non-favorite item from persistent storage.
    /// Invoked during screen initialization and after file state changes.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<FavoriteFile> favorites = await _filesDockService.GetFavoritesAsync(cancellationToken);
            IReadOnlyList<RecentFile> recentFiles = await _filesDockService.GetRecentFilesWithin24HoursAsync(cancellationToken);
            RecentFile? nonFavorite = await _filesDockService.GetLastOpenedNonFavoriteAsync(cancellationToken);
            bool isCurrentFavorite = await CheckIsCurrentFavoriteAsync(State.CurrentFilePath, cancellationToken);

            UpdateState(state => state with
            {
                Favorites = ImmutableList.CreateRange(favorites),
                RecentFiles = ImmutableList.CreateRange(recentFiles),
                LastOpenedNonFavorite = nonFavorite,
                IsCurrentFileFavorite = isCurrentFavorite,
                CanAddCurrentFile = !string.IsNullOrEmpty(state.CurrentFilePath) && !isCurrentFavorite,
                ErrorMessage = null
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UpdateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    /// <summary>
    /// Notifies the dock that a file has been opened or saved in the editor.
    /// Invoked by EditorViewModel when a document is loaded or saved with a file path.
    /// </summary>
    public async Task NotifyFileOpenedAsync(string? filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || SampleDocumentFilter.IsSampleOrTestDocument(filePath))
        {
            UpdateState(state => state with
            {
                CurrentFilePath = null,
                CanAddCurrentFile = false,
                IsCurrentFileFavorite = false,
                IsAddFavoriteFormOpen = false
            });
            return;
        }

        UpdateState(state => state with
        {
            CurrentFilePath = filePath,
            IsAddFavoriteFormOpen = false
        });

        await _filesDockService.RecordFileOpenAsync(filePath, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Updates the custom name entered in the inline favorite creation form.
    /// Invoked by FilesDockView when the text box changes.
    /// </summary>
    public void SetNewFavoriteCustomName(string customName)
    {
        UpdateState(state => state with
        {
            NewFavoriteCustomName = customName ?? string.Empty
        });
    }

    [ReactiveCommand]
    private void ToggleDockExpansion()
    {
        UpdateState(state => state with
        {
            IsDockExpanded = !state.IsDockExpanded
        });
    }

    [ReactiveCommand]
    private void SetFavoritesMode()
    {
        UpdateState(state => state with
        {
            Mode = FilesDockMode.Favorites
        });
    }

    [ReactiveCommand]
    private void SetRecentMode()
    {
        UpdateState(state => state with
        {
            Mode = FilesDockMode.Recent
        });
    }

    [ReactiveCommand]
    private void SwitchMode()
    {
        FilesDockMode nextMode = State.Mode == FilesDockMode.Favorites
            ? FilesDockMode.Recent
            : FilesDockMode.Favorites;

        UpdateState(state => state with
        {
            Mode = nextMode
        });
    }

    [ReactiveCommand]
    private void OpenAddFavoriteForm()
    {
        if (string.IsNullOrEmpty(State.CurrentFilePath))
            return;

        string defaultName = Path.GetFileNameWithoutExtension(State.CurrentFilePath);

        UpdateState(state => state with
        {
            IsAddFavoriteFormOpen = true,
            NewFavoriteCustomName = defaultName,
            Mode = FilesDockMode.Favorites
        });
    }

    [ReactiveCommand]
    private void CancelAddFavoriteForm()
    {
        UpdateState(state => state with
        {
            IsAddFavoriteFormOpen = false,
            NewFavoriteCustomName = string.Empty
        });
    }

    [ReactiveCommand]
    private async Task ConfirmAddFavorite(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(State.CurrentFilePath))
            return;

        string customName = string.IsNullOrWhiteSpace(State.NewFavoriteCustomName)
            ? Path.GetFileNameWithoutExtension(State.CurrentFilePath)
            : State.NewFavoriteCustomName.Trim();

        try
        {
            await _filesDockService.AddFavoriteAsync(State.CurrentFilePath, customName, cancellationToken);
            UpdateState(state => state with
            {
                IsAddFavoriteFormOpen = false,
                NewFavoriteCustomName = string.Empty
            });
            await RefreshAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UpdateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    [ReactiveCommand]
    private async Task RemoveFavorite(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _filesDockService.RemoveFavoriteAsync(id, cancellationToken);
            await RefreshAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            UpdateState(state => state with { ErrorMessage = exception.Message });
        }
    }

    [ReactiveCommand]
    private void OpenFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        _fileSelectedSubject.OnNext(filePath);
    }

    private async Task<bool> CheckIsCurrentFavoriteAsync(string? filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        return await _filesDockService.IsFavoriteAsync(filePath, cancellationToken);
    }
}
