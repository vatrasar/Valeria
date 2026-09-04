using System;
using System.Collections.Generic;
using Avalonia.Controls.Documents;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Builds Avalonia preview controls from parsed markdown content.
/// Styling follows the dark document theme with rich code highlighting.
/// Code blocks are rendered plain first and colorized later via
/// <see cref="ApplyHighlight"/>. Invoked by EditorViewModel.
/// </summary>
public interface IMarkdownPreviewBuilder
{
    /// <summary>
    /// Synchronously builds all preview blocks. Code blocks are created as
    /// plain selectable text and collected as <see cref="CodeHighlightTarget"/>
    /// for asynchronous highlighting.
    /// </summary>
    PreviewBuildResult BuildBlocks(MarkdownContent content, Action<int, bool>? onTaskToggled = null);

    /// <summary>
    /// Applies pre-tokenized spans onto a previously built code target.
    /// Called on the UI thread once background highlighting finished.
    /// </summary>
    void ApplyHighlight(CodeHighlightTarget target, IReadOnlyList<HighlightedLine> lines);
}
