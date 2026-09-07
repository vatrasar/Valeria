using Avalonia.Controls;

namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// Result of a search query in the markdown preview panel.
/// </summary>
public record PreviewSearchResult
{
    public int TotalMatches { get; init; }

    public int CurrentMatchIndex { get; init; }

    public SelectableTextBlock? ActiveTextBlock { get; init; }

    public int ActiveStartIndex { get; init; }

    public int ActiveLength { get; init; }

    public static PreviewSearchResult Empty => new()
    {
        TotalMatches = 0,
        CurrentMatchIndex = 0,
        ActiveTextBlock = null,
        ActiveStartIndex = 0,
        ActiveLength = 0
    };
}
