using System;

namespace Valeria.Src.Infrastructure.Data.Entities;

/// <summary>
/// Database entity storing a favorite file entry.
/// </summary>
public sealed class FavoriteFileEntity
{
    public int Id { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string CustomName { get; set; } = string.Empty;

    public DateTime AddedAtUtc { get; set; }

    public DateTime LastUsedAtUtc { get; set; }
}
