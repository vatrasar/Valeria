using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Infrastructure.Data;
using Valeria.Src.Infrastructure.Data.Entities;
using Valeria.Src.Infrastructure.Data.Repositories;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.RepositoriesTests;

public sealed class RecentFileRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _factory;
    private readonly RecentFileRepository _repository;

    public RecentFileRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = new TestDbContextFactory(_connection);
        _repository = new RecentFileRepository(_factory);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task RecordFileOpenAsync_WhenCalled_SavesRecentFileRecord()
    {
        await _repository.RecordFileOpenAsync("/path/to/notes.md");

        IReadOnlyList<RecentFile> recent = await _repository.GetRecentFilesAsync(TimeSpan.FromHours(24));

        Assert.Single(recent);
        Assert.Equal("/path/to/notes.md", recent[0].FilePath);
        Assert.Equal("notes.md", recent[0].FileName);
    }

    [Fact]
    public async Task RecordFileOpenAsync_WhenOpenedAgain_UpdatesLastOpenedTimestamp()
    {
        await _repository.RecordFileOpenAsync("/path/to/notes.md");
        DateTime firstOpen = (await _repository.GetRecentFilesAsync(TimeSpan.FromHours(24)))[0].LastOpenedAtUtc;

        await Task.Delay(20);
        await _repository.RecordFileOpenAsync("/path/to/notes.md");

        IReadOnlyList<RecentFile> recent = await _repository.GetRecentFilesAsync(TimeSpan.FromHours(24));
        Assert.Single(recent);
        Assert.True(recent[0].LastOpenedAtUtc >= firstOpen);
    }

    [Fact]
    public async Task GetRecentFilesAsync_WhenFilesOlderThan24HoursExist_OnlyReturnsFilesWithin24Hours()
    {
        await _repository.RecordFileOpenAsync("/path/to/fresh.md");

        await using (ValeriaDbContext context = _factory.CreateDbContext())
        {
            context.RecentFiles.Add(new RecentFileEntity
            {
                FilePath = "/path/to/old.md",
                FileName = "old.md",
                LastOpenedAtUtc = DateTime.UtcNow.AddHours(-26)
            });
            await context.SaveChangesAsync();
        }

        IReadOnlyList<RecentFile> recent = await _repository.GetRecentFilesAsync(TimeSpan.FromHours(24));

        Assert.Single(recent);
        Assert.Equal("/path/to/fresh.md", recent[0].FilePath);
    }

    [Fact]
    public async Task CleanupOldRecordsAsync_WhenExpiredRecordsExist_DeletesExpiredRecords()
    {
        await using (ValeriaDbContext context = _factory.CreateDbContext())
        {
            context.RecentFiles.Add(new RecentFileEntity
            {
                FilePath = "/path/to/expired.md",
                FileName = "expired.md",
                LastOpenedAtUtc = DateTime.UtcNow.AddHours(-25)
            });
            context.RecentFiles.Add(new RecentFileEntity
            {
                FilePath = "/path/to/active.md",
                FileName = "active.md",
                LastOpenedAtUtc = DateTime.UtcNow.AddHours(-2)
            });
            await context.SaveChangesAsync();
        }

        await _repository.CleanupOldRecordsAsync(TimeSpan.FromHours(24));

        await using (ValeriaDbContext context = _factory.CreateDbContext())
        {
            List<RecentFileEntity> remaining = await context.RecentFiles.ToListAsync();
            Assert.Single(remaining);
            Assert.Equal("/path/to/active.md", remaining[0].FilePath);
        }
    }

    [Fact]
    public async Task GetLastOpenedNonFavoriteAsync_WhenNonFavoriteExists_ReturnsMostRecentNonFavorite()
    {
        await _repository.RecordFileOpenAsync("/path/to/favorite1.md");
        await Task.Delay(20);
        await _repository.RecordFileOpenAsync("/path/to/non_favorite.md");
        await Task.Delay(20);
        await _repository.RecordFileOpenAsync("/path/to/favorite2.md");

        List<string> favoritePaths = new() { "/path/to/favorite1.md", "/path/to/favorite2.md" };

        RecentFile? lastNonFavorite = await _repository.GetLastOpenedNonFavoriteAsync(favoritePaths);

        Assert.NotNull(lastNonFavorite);
        Assert.Equal("/path/to/non_favorite.md", lastNonFavorite.FilePath);
    }

    [Fact]
    public async Task GetLastOpenedNonFavoriteAsync_WhenAllRecentFilesAreFavorites_ReturnsNull()
    {
        await _repository.RecordFileOpenAsync("/path/to/favorite1.md");
        await _repository.RecordFileOpenAsync("/path/to/favorite2.md");

        List<string> favoritePaths = new() { "/path/to/favorite1.md", "/path/to/favorite2.md" };

        RecentFile? lastNonFavorite = await _repository.GetLastOpenedNonFavoriteAsync(favoritePaths);

        Assert.Null(lastNonFavorite);
    }

    [Theory]
    [InlineData("test-sample-document.md")]
    [InlineData("/path/to/test-sample-document.md")]
    [InlineData("/tmp/sample-document.md")]
    [InlineData("Welcome.md")]
    [InlineData("/project/Tests/bin/Debug/net10.0/test-file.md")]
    public async Task RecordFileOpenAsync_WhenSampleOrTestDocument_DoesNotSaveRecord(string samplePath)
    {
        await _repository.RecordFileOpenAsync(samplePath);

        IReadOnlyList<RecentFile> recent = await _repository.GetRecentFilesAsync(TimeSpan.FromHours(24));

        Assert.Empty(recent);
    }

    [Fact]
    public async Task GetRecentFilesAsync_WhenSampleEntityExistsInDatabase_IgnoresSampleEntity()
    {
        await _repository.RecordFileOpenAsync("/path/to/valid.md");

        await using (ValeriaDbContext context = _factory.CreateDbContext())
        {
            context.RecentFiles.Add(new RecentFileEntity
            {
                FilePath = "/home/bin/Debug/test-sample-document.md",
                FileName = "test-sample-document.md",
                LastOpenedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        IReadOnlyList<RecentFile> recent = await _repository.GetRecentFilesAsync(TimeSpan.FromHours(24));

        Assert.Single(recent);
        Assert.Equal("/path/to/valid.md", recent[0].FilePath);
    }

    [Fact]
    public async Task GetLastOpenedNonFavoriteAsync_WhenNonFavoriteIsSampleDocument_IgnoresSampleDocument()
    {
        await using (ValeriaDbContext context = _factory.CreateDbContext())
        {
            context.RecentFiles.Add(new RecentFileEntity
            {
                FilePath = "/path/to/test-sample-document.md",
                FileName = "test-sample-document.md",
                LastOpenedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        RecentFile? lastNonFavorite = await _repository.GetLastOpenedNonFavoriteAsync(Array.Empty<string>());

        Assert.Null(lastNonFavorite);
    }

    [Fact]
    public async Task CleanupOldRecordsAsync_WhenSampleEntitiesExist_DeletesSampleEntities()
    {
        await using (ValeriaDbContext context = _factory.CreateDbContext())
        {
            context.RecentFiles.Add(new RecentFileEntity
            {
                FilePath = "test-sample-document.md",
                FileName = "test-sample-document.md",
                LastOpenedAtUtc = DateTime.UtcNow
            });
            context.RecentFiles.Add(new RecentFileEntity
            {
                FilePath = "/path/to/regular.md",
                FileName = "regular.md",
                LastOpenedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await _repository.CleanupOldRecordsAsync(TimeSpan.FromHours(24));

        await using (ValeriaDbContext context = _factory.CreateDbContext())
        {
            List<RecentFileEntity> remaining = await context.RecentFiles.ToListAsync();
            Assert.Single(remaining);
            Assert.Equal("/path/to/regular.md", remaining[0].FilePath);
        }
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
