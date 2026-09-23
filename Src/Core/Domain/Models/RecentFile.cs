using System;

namespace Valeria.Src.Core.Domain.Models;

/// <summary>
/// Domain model representing a recently opened file entry.
/// </summary>
public sealed record RecentFile(
    int Id,
    string FilePath,
    string FileName,
    DateTime LastOpenedAtUtc);
