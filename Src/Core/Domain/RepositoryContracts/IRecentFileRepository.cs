using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Core.Domain.Models;

namespace Valeria.Src.Core.Domain.RepositoryContracts;

/// <summary>
/// Repository interface managing persistent tracking of recently opened files.
/// </summary>
public interface IRecentFileRepository
{
    /// <summary>
    /// Records that a file was opened, updating its last opened timestamp to now.
    /// Invoked by FilesDockService.
    /// </summary>
    Task RecordFileOpenAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all files opened within the specified maximum age window.
    /// Invoked by FilesDockService.
    /// </summary>
    Task<IReadOnlyList<RecentFile>> GetRecentFilesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the single most recently opened file whose path is not present in the given favorite paths.
    /// Invoked by FilesDockService.
    /// </summary>
    Task<RecentFile?> GetLastOpenedNonFavoriteAsync(IReadOnlyCollection<string> favoritePaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all recent file records whose last opened timestamp is older than the specified age window.
    /// Invoked by FilesDockService.
    /// </summary>
    Task CleanupOldRecordsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}
