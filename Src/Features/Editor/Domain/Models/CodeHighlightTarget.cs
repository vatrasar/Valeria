using Avalonia.Controls;

namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// Mutable handle to a rendered code block awaiting asynchronous highlighting.
/// Created by the preview builder during the synchronous placeholder pass and
/// completed by EditorViewModel once background tokenization finishes.
/// </summary>
public sealed record CodeHighlightTarget(
    SelectableTextBlock TextBlock,
    string? Language,
    string Code);
