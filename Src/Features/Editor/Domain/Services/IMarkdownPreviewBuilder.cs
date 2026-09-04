using System.Collections.Generic;
using Avalonia.Controls;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Builds Avalonia preview controls from parsed markdown content.
/// Styling follows the dark document theme with rich code highlighting.
/// </summary>
public interface IMarkdownPreviewBuilder
{
    IReadOnlyList<Control> BuildBlocks(MarkdownContent content);
}
