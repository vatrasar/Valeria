using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Valeria.Src.Infrastructure.Data.Entities;

namespace Valeria.Src.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the Valeria application.
/// Manages tables for favorite files and recent file history using SQLite.
/// </summary>
public class ValeriaDbContext : DbContext
{
    public DbSet<FavoriteFileEntity> FavoriteFiles { get; set; } = null!;

    public DbSet<RecentFileEntity> RecentFiles { get; set; } = null!;

    public ValeriaDbContext(DbContextOptions<ValeriaDbContext> options)
        : base(options)
    {
    }

    public ValeriaDbContext()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        string databasePath = GetDefaultDatabasePath();
        optionsBuilder.UseSqlite($"Data Source={databasePath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FavoriteFileEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FilePath).IsRequired();
            entity.Property(e => e.CustomName).IsRequired();
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => e.LastUsedAtUtc);
        });

        modelBuilder.Entity<RecentFileEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FilePath).IsRequired();
            entity.Property(e => e.FileName).IsRequired();
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => e.LastOpenedAtUtc);
        });
    }

    /// <summary>
    /// Computes the default database file path in the user-specific application data folder.
    /// Ensures that the parent directory exists.
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(localAppData, "valeria");

        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);

        return Path.Combine(appFolder, "valeria.db");
    }
}
