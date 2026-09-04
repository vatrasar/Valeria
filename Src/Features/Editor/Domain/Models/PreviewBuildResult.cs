using System.Collections.Generic;
using System.Collections.Immutable;
using Avalonia.Controls;

namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// Result of the synchronous preview pass: ready to display blocks plus
/// code targets scheduled for asynchronous syntax highlighting.
/// Produced by <see cref="Services.IMarkdownPreviewBuilder"/>
/// and consumed by EditorViewModel.
/// </summary>
public sealed record PreviewBuildResult(
    IReadOnlyList<Control> Blocks,
    ImmutableList<CodeHighlightTarget> CodeTargets);
