using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Core.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Domain service coordinating favorite and recent file operations for the dock.
/// </summary>
public interface IFilesDockService
{
    /// <summary>
    /// Retrieves all favorite files sorted descending by their last used timestamp.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    Task<IReadOnlyList<FavoriteFile>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a favorite file with a user-defined custom name.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    Task<FavoriteFile> AddFavoriteAsync(string filePath, string customName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a favorite file by its database identifier.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    Task<bool> RemoveFavoriteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the given file path is currently in the favorites list.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    Task<bool> IsFavoriteAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a file was opened and triggers automatic cleanup of entries older than 24 hours.
    /// Also updates the favorite's last used timestamp if the file is in favorites.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    Task RecordFileOpenAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all files opened within the last 24 hours.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    Task<IReadOnlyList<RecentFile>> GetRecentFilesWithin24HoursAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recently opened file that is not in the favorites list.
    /// Invoked by FilesDockViewModel.
    /// </summary>
    Task<RecentFile?> GetLastOpenedNonFavoriteAsync(CancellationToken cancellationToken = default);
}
