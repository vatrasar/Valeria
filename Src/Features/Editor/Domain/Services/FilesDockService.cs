using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Core.Domain;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Core.Domain.RepositoryContracts;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Domain service coordinating favorite and recent file operations for the dock.
/// </summary>
public sealed class FilesDockService : IFilesDockService
{
    private static readonly TimeSpan RecentHistoryWindow = TimeSpan.FromHours(24);

    private readonly IFavoriteFileRepository _favoriteFilesRepository;
    private readonly IRecentFileRepository _recentFilesRepository;

    public FilesDockService(
        IFavoriteFileRepository favoriteFilesRepository,
        IRecentFileRepository recentFilesRepository)
    {
        _favoriteFilesRepository = favoriteFilesRepository;
        _recentFilesRepository = recentFilesRepository;
    }

    /// <summary>
    /// Retrieves all favorite files sorted descending by their last used timestamp.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    public Task<IReadOnlyList<FavoriteFile>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        return _favoriteFilesRepository.GetAllSortedByLastUsedAsync(cancellationToken);
    }

    /// <summary>
    /// Adds or updates a favorite file with a user-defined custom name.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    public Task<FavoriteFile> AddFavoriteAsync(string filePath, string customName, CancellationToken cancellationToken = default)
    {
        return _favoriteFilesRepository.AddOrUpdateAsync(filePath, customName, cancellationToken);
    }

    /// <summary>
    /// Removes a favorite file by its database identifier.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    public Task<bool> RemoveFavoriteAsync(int id, CancellationToken cancellationToken = default)
    {
        return _favoriteFilesRepository.RemoveAsync(id, cancellationToken);
    }

    /// <summary>
    /// Checks whether the given file path is currently in the favorites list.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    public async Task<bool> IsFavoriteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        FavoriteFile? match = await _favoriteFilesRepository.GetByPathAsync(filePath, cancellationToken);
        return match is not null;
    }

    /// <summary>
    /// Records that a file was opened and triggers automatic cleanup of entries older than 24 hours.
    /// Also updates the favorite's last used timestamp if the file is in favorites.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    public async Task RecordFileOpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || SampleDocumentFilter.IsSampleOrTestDocument(filePath))
            return;

        await _recentFilesRepository.RecordFileOpenAsync(filePath, cancellationToken);
        await _favoriteFilesRepository.UpdateLastUsedAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Retrieves all files opened within the last 24 hours.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    public Task<IReadOnlyList<RecentFile>> GetRecentFilesWithin24HoursAsync(CancellationToken cancellationToken = default)
    {
        return _recentFilesRepository.GetRecentFilesAsync(RecentHistoryWindow, cancellationToken);
    }

    /// <summary>
    /// Retrieves the most recently opened file that is not in the favorites list.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    public async Task<RecentFile?> GetLastOpenedNonFavoriteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FavoriteFile> favorites = await _favoriteFilesRepository.GetAllSortedByLastUsedAsync(cancellationToken);
        List<string> favoritePaths = favorites.Select(file => file.FilePath).ToList();

        RecentFile? candidate = await _recentFilesRepository.GetLastOpenedNonFavoriteAsync(favoritePaths, cancellationToken);
        if (candidate is null || SampleDocumentFilter.IsSampleOrTestDocument(candidate.FilePath))
            return null;

        return candidate;
    }
}
