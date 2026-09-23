using System;

namespace Valeria.Src.Core.Domain.Models;

/// <summary>
/// Domain model representing a favorite file entry with a user-defined alias.
/// </summary>
public sealed record FavoriteFile(
    int Id,
    string FilePath,
    string CustomName,
    DateTime AddedAtUtc,
    DateTime LastUsedAtUtc);
