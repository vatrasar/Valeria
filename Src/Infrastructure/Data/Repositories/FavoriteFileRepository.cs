using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Core.Domain.RepositoryContracts;
using Valeria.Src.Infrastructure.Data.Entities;

namespace Valeria.Src.Infrastructure.Data.Repositories;

/// <summary>
/// SQLite EF Core implementation of the favorite files repository.
/// </summary>
public sealed class FavoriteFileRepository : IFavoriteFileRepository
{
    private readonly IDbContextFactory<ValeriaDbContext> _contextFactory;

    public FavoriteFileRepository(IDbContextFactory<ValeriaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Retrieves all favorite files sorted descending by their last used timestamp.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task<IReadOnlyList<FavoriteFile>> GetAllSortedByLastUsedAsync(CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        List<FavoriteFileEntity> entities = await context.FavoriteFiles
            .AsNoTracking()
            .OrderByDescending(entity => entity.LastUsedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    /// <summary>
    /// Retrieves a favorite file by its full file path, or null if not found.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task<FavoriteFile?> GetByPathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        FavoriteFileEntity? entity = await context.FavoriteFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.FilePath == filePath, cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    /// <summary>
    /// Adds a new favorite file or updates an existing one with the given custom name.
    /// Sets LastUsedAtUtc to the current UTC timestamp.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task<FavoriteFile> AddOrUpdateAsync(string filePath, string customName, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        FavoriteFileEntity? existing = await context.FavoriteFiles
            .FirstOrDefaultAsync(item => item.FilePath == filePath, cancellationToken);

        DateTime now = DateTime.UtcNow;

        if (existing is not null)
        {
            existing.CustomName = customName;
            existing.LastUsedAtUtc = now;
            await context.SaveChangesAsync(cancellationToken);
            return MapToDomain(existing);
        }

        FavoriteFileEntity newEntity = new()
        {
            FilePath = filePath,
            CustomName = customName,
            AddedAtUtc = now,
            LastUsedAtUtc = now
        };

        context.FavoriteFiles.Add(newEntity);
        await context.SaveChangesAsync(cancellationToken);

        return MapToDomain(newEntity);
    }

    /// <summary>
    /// Updates the last used timestamp of a favorite file to the current UTC timestamp.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task UpdateLastUsedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        FavoriteFileEntity? existing = await context.FavoriteFiles
            .FirstOrDefaultAsync(item => item.FilePath == filePath, cancellationToken);

        if (existing is null)
            return;

        existing.LastUsedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a favorite file by its database identifier.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task<bool> RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        FavoriteFileEntity? entity = await context.FavoriteFiles
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (entity is null)
            return false;

        context.FavoriteFiles.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Removes a favorite file by its file path.
    /// Invoked by FilesDockService.
    /// </summary>
    public async Task<bool> RemoveByPathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using ValeriaDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        FavoriteFileEntity? entity = await context.FavoriteFiles
            .FirstOrDefaultAsync(item => item.FilePath == filePath, cancellationToken);

        if (entity is null)
            return false;

        context.FavoriteFiles.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static FavoriteFile MapToDomain(FavoriteFileEntity entity)
    {
        return new FavoriteFile(
            entity.Id,
            entity.FilePath,
            entity.CustomName,
            entity.AddedAtUtc,
            entity.LastUsedAtUtc);
    }
}
