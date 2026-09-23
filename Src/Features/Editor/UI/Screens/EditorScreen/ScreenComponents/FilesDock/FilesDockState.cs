using System.Collections.Immutable;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Enums;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen.ScreenComponents.FilesDock;

/// <summary>
/// Immutable state of the files dock component.
/// Holds favorite files, recent files opened within 24h, the top non-favorite item, and inline form state.
/// </summary>
public sealed record FilesDockState
{
    public bool IsDockExpanded { get; init; } = true;

    public FilesDockMode Mode { get; init; } = FilesDockMode.Favorites;

    public ImmutableList<FavoriteFile> Favorites { get; init; } = ImmutableList<FavoriteFile>.Empty;

    public ImmutableList<RecentFile> RecentFiles { get; init; } = ImmutableList<RecentFile>.Empty;

    public RecentFile? LastOpenedNonFavorite { get; init; }

    public string? CurrentFilePath { get; init; }

    public bool IsCurrentFileFavorite { get; init; }

    public bool CanAddCurrentFile { get; init; }

    public bool IsAddFavoriteFormOpen { get; init; }

    public string NewFavoriteCustomName { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    public bool HasFavorites => !Favorites.IsEmpty;

    public bool HasRecentFiles => !RecentFiles.IsEmpty;

    public bool HasLastOpenedNonFavorite => LastOpenedNonFavorite is not null;
}
