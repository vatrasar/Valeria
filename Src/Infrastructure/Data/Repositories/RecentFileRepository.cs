using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Valeria.Src.Core.Domain;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Core.Domain.RepositoryContracts;
using Valeria.Src.Infrastructure.Data.Entities;

namespace Valeria.Src.Infrastructure.Data.Repositories;

/// <summary>
/// SQLite EF Core implementation of the recent files repository with automatic 24-hour cleanup.
/// </summary>
public sealed class RecentFileRepository : IRecentFileRepository
{
    private readonly IDbContextFactory<ValeriaDbContext> _contextFactory;

    public RecentFileRepository(IDbContextFactory<ValeriaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Records that a file was opened, updating its last opened timestamp to now and cleaning up expired records.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task RecordFileOpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || SampleDocumentFilter.IsSampleOrTestDocument(filePath))
            return;

        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        string fileName = Path.GetFileName(filePath);
        DateTime now = DateTime.UtcNow;

        RecentFileEntity? existing = await context.RecentFiles
            .FirstOrDefaultAsync(item => item.FilePath == filePath, cancellationToken);

        if (existing is not null)
        {
            existing.FileName = fileName;
            existing.LastOpenedAtUtc = now;
        }
        else
        {
            RecentFileEntity newEntity = new()
            {
                FilePath = filePath,
                FileName = fileName,
                LastOpenedAtUtc = now
            };
            context.RecentFiles.Add(newEntity);
        }

        await context.SaveChangesAsync(cancellationToken);
        await CleanupOldRecordsAsync(TimeSpan.FromHours(24), cancellationToken);
    }

    /// <summary>
    /// Retrieves all files opened within the specified maximum age window.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task<IReadOnlyList<RecentFile>> GetRecentFilesAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        DateTime cutoff = DateTime.UtcNow - maxAge;

        List<RecentFileEntity> entities = await context.RecentFiles
            .AsNoTracking()
            .Where(item => item.LastOpenedAtUtc >= cutoff)
            .OrderByDescending(item => item.LastOpenedAtUtc)
            .ToListAsync(cancellationToken);

        return entities
            .Where(item => !SampleDocumentFilter.IsSampleOrTestDocument(item.FilePath))
            .Select(MapToDomain)
            .ToList();
    }

    /// <summary>
    /// Retrieves the single most recently opened file whose path is not present in the given favorite paths.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task<RecentFile?> GetLastOpenedNonFavoriteAsync(IReadOnlyCollection<string> favoritePaths, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        HashSet<string> exclusionSet = new(favoritePaths, StringComparer.Ordinal);

        List<RecentFileEntity> candidates = await context.RecentFiles
            .AsNoTracking()
            .OrderByDescending(item => item.LastOpenedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        RecentFileEntity? match = candidates.FirstOrDefault(candidate =>
            !exclusionSet.Contains(candidate.FilePath) &&
            !SampleDocumentFilter.IsSampleOrTestDocument(candidate.FilePath));

        return match is null ? null : MapToDomain(match);
    }

    /// <summary>
    /// Deletes all recent file records whose last opened timestamp is older than the specified age window.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task CleanupOldRecordsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        DateTime cutoff = DateTime.UtcNow - maxAge;

        List<RecentFileEntity> expired = await context.RecentFiles
            .Where(item => item.LastOpenedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        List<RecentFileEntity> allEntities = await context.RecentFiles.ToListAsync(cancellationToken);
        List<RecentFileEntity> sampleEntities = allEntities
            .Where(item => SampleDocumentFilter.IsSampleOrTestDocument(item.FilePath) && !expired.Contains(item))
            .ToList();

        List<RecentFileEntity> toDelete = [.. expired, .. sampleEntities];

        if (toDelete.Count == 0)
            return;

        context.RecentFiles.RemoveRange(toDelete);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static RecentFile MapToDomain(RecentFileEntity entity)
    {
        return new RecentFile(
            entity.Id,
            entity.FilePath,
            entity.FileName,
            entity.LastOpenedAtUtc);
    }
}
