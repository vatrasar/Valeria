using System.Collections.Generic;
using Avalonia.Controls;
using NewMarkText.Src.Core.Markdown;

namespace NewMarkText.Src.Features.Editor.Domain.Services;

/// <summary>
/// Builds Avalonia preview controls from parsed markdown content.
/// Styling follows the MarkText look adapted to a dark theme.
/// </summary>
public interface IMarkdownPreviewBuilder
{
    IReadOnlyList<Control> BuildBlocks(MarkdownContent content);
}
