using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Core.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Fallback no-op implementation of IFilesDockService used when no dock service is registered.
/// </summary>
internal sealed class NullFilesDockService : IFilesDockService
{
    public Task<IReadOnlyList<FavoriteFile>> GetFavoritesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FavoriteFile>>(Array.Empty<FavoriteFile>());

    public Task<FavoriteFile> AddFavoriteAsync(string filePath, string customName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FavoriteFile(0, filePath, customName, DateTime.UtcNow, DateTime.UtcNow));

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
