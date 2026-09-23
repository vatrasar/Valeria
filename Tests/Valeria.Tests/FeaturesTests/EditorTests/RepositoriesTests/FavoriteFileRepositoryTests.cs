using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Infrastructure.Data;
using Valeria.Src.Infrastructure.Data.Repositories;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.RepositoriesTests;

public sealed class FavoriteFileRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _factory;
    private readonly FavoriteFileRepository _repository;

    public FavoriteFileRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new TestDbContextFactory(_connection);
        _repository = new FavoriteFileRepository(_factory);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task AddOrUpdateAsync_WhenNewFileAdded_SavesFavoriteSuccessfully()
    {
        FavoriteFile added = await _repository.AddOrUpdateAsync("/path/to/doc.md", "My Document");

        Assert.NotNull(added);
        Assert.Equal("/path/to/doc.md", added.FilePath);
        Assert.Equal("My Document", added.CustomName);
        Assert.True(added.Id > 0);

        FavoriteFile? retrieved = await _repository.GetByPathAsync("/path/to/doc.md");
        Assert.NotNull(retrieved);
        Assert.Equal("My Document", retrieved.CustomName);
    }

    [Fact]
    public async Task AddOrUpdateAsync_WhenExistingFileUpdated_UpdatesCustomNameAndLastUsed()
    {
        await _repository.AddOrUpdateAsync("/path/to/doc.md", "Initial Name");
        FavoriteFile updated = await _repository.AddOrUpdateAsync("/path/to/doc.md", "Updated Name");

        Assert.Equal("Updated Name", updated.CustomName);

        IReadOnlyList<FavoriteFile> all = await _repository.GetAllSortedByLastUsedAsync();
        Assert.Single(all);
        Assert.Equal("Updated Name", all[0].CustomName);
    }

    [Fact]
    public async Task GetAllSortedByLastUsedAsync_WhenMultipleFavoritesExist_ReturnsSortedByLastUsedDescending()
    {
        await _repository.AddOrUpdateAsync("/path/to/first.md", "First");
        await Task.Delay(20);
        await _repository.AddOrUpdateAsync("/path/to/second.md", "Second");
        await Task.Delay(20);
        await _repository.AddOrUpdateAsync("/path/to/third.md", "Third");

        IReadOnlyList<FavoriteFile> all = await _repository.GetAllSortedByLastUsedAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal("/path/to/third.md", all[0].FilePath);
        Assert.Equal("/path/to/second.md", all[1].FilePath);
        Assert.Equal("/path/to/first.md", all[2].FilePath);
    }

    [Fact]
    public async Task UpdateLastUsedAsync_WhenFavoriteOpened_MovesToTopOfSortedList()
    {
        await _repository.AddOrUpdateAsync("/path/to/first.md", "First");
        await Task.Delay(20);
        await _repository.AddOrUpdateAsync("/path/to/second.md", "Second");

        await Task.Delay(20);
        await _repository.UpdateLastUsedAsync("/path/to/first.md");

        IReadOnlyList<FavoriteFile> all = await _repository.GetAllSortedByLastUsedAsync();

        Assert.Equal("/path/to/first.md", all[0].FilePath);
        Assert.Equal("/path/to/second.md", all[1].FilePath);
    }

    [Fact]
    public async Task RemoveAsync_WhenFavoriteExists_DeletesFromDatabase()
    {
        FavoriteFile added = await _repository.AddOrUpdateAsync("/path/to/delete.md", "To Delete");

        bool removed = await _repository.RemoveAsync(added.Id);

        Assert.True(removed);
        FavoriteFile? retrieved = await _repository.GetByPathAsync("/path/to/delete.md");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task RemoveByPathAsync_WhenFavoriteExists_DeletesFromDatabase()
    {
        await _repository.AddOrUpdateAsync("/path/to/delete_by_path.md", "To Delete");

        bool removed = await _repository.RemoveByPathAsync("/path/to/delete_by_path.md");

        Assert.True(removed);
        FavoriteFile? retrieved = await _repository.GetByPathAsync("/path/to/delete_by_path.md");
        Assert.Null(retrieved);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ValeriaDbContext>
    {
        private readonly DbContextOptions<ValeriaDbContext> _options;

        public TestDbContextFactory(SqliteConnection connection)
        {
            _options = new DbContextOptionsBuilder<ValeriaDbContext>()
                .UseSqlite(connection)
                .Options;

            using ValeriaDbContext context = new(_options);
            context.Database.EnsureCreated();
        }

        public ValeriaDbContext CreateDbContext() => new(_options);
    }
}
