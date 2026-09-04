using System.Collections.Immutable;
using Avalonia.Controls;
using Valeria.Src.Shared.Resources;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Immutable state of the markdown editor screen.
/// </summary>
public record EditorState
{
    public string MarkdownText { get; init; } = string.Empty;

    public string? FilePath { get; init; }

    public bool IsDirty { get; init; }

    public int WordCount { get; init; }

    public int CaretLine { get; init; } = 1;

    public int CaretColumn { get; init; } = 1;

    public string DocumentTitle { get; init; } = GlobalStrings.UntitledDocument;

    public string? ErrorMessage { get; init; }

    public bool IsPreviewVisible { get; init; } = true;

    public bool IsPreviewIdle { get; init; } = true;

    public ImmutableList<Control> PreviewBlocks { get; init; } = ImmutableList<Control>.Empty;

    public bool IsEmpty => string.IsNullOrWhiteSpace(MarkdownText);
}
