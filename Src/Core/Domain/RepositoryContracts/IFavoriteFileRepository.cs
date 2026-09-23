using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Core.Domain.Models;

namespace Valeria.Src.Core.Domain.RepositoryContracts;

/// <summary>
/// Repository interface managing persistent storage for favorite file records.
/// </summary>
public interface IFavoriteFileRepository
{
    /// <summary>
    /// Retrieves all favorite files sorted descending by their last used timestamp.
    /// Invoked by FilesDockService.
    /// </summary>
    Task<IReadOnlyList<FavoriteFile>> GetAllSortedByLastUsedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a favorite file by its full file path, or null if not found.
    /// Invoked by FilesDockService.
    /// </summary>
    Task<FavoriteFile?> GetByPathAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new favorite file or updates an existing one with the given custom name.
    /// Sets LastUsedAtUtc to the current UTC timestamp.
    /// Invoked by FilesDockService.
    /// </summary>
    Task<FavoriteFile> AddOrUpdateAsync(string filePath, string customName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last used timestamp of a favorite file to the current UTC timestamp.
    /// Invoked by FilesDockService.
    /// </summary>
    Task UpdateLastUsedAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a favorite file by its database identifier.
    /// Invoked by FilesDockService.
    /// </summary>
    Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a favorite file by its file path.
    /// Invoked by FilesDockService.
    /// </summary>
    Task<bool> RemoveByPathAsync(string filePath, CancellationToken cancellationToken = default);
}
