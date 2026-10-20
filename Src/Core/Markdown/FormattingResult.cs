namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Result of a pure text transformation requested by a formatting action.
/// </summary>
public sealed record FormattingResult(string Text, int CaretOffset, int SelectionLength);
