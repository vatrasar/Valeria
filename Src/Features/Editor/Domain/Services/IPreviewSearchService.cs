using System.Collections.Generic;
using Avalonia.Controls;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Contract for text search and highlight management within rendered markdown preview blocks.
/// </summary>
public interface IPreviewSearchService
{
    /// <summary>
    /// Searches preview blocks for the query and highlights matching text runs.
    /// Used by EditorView search operations.
    /// </summary>
    PreviewSearchResult Search(IEnumerable<Control> blocks, string query, bool matchCase);

    /// <summary>
    /// Moves the active highlight to the next match and scrolls it into view.
    /// Used by EditorView search operations.
    /// </summary>
    PreviewSearchResult NavigateNext();

    /// <summary>
    /// Moves the active highlight to the previous match and scrolls it into view.
    /// Used by EditorView search operations.
    /// </summary>
    PreviewSearchResult NavigatePrevious();

    /// <summary>
    /// Clears all highlights and restores all preview blocks to their original state.
    /// Used by EditorView search operations.
    /// </summary>
    void Clear();
}
