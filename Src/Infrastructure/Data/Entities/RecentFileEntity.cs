using System;

namespace Valeria.Src.Infrastructure.Data.Entities;

/// <summary>
/// Database entity storing a logged recent file open entry.
/// </summary>
public sealed class RecentFileEntity
{
    public int Id { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public DateTime LastOpenedAtUtc { get; set; }
}
