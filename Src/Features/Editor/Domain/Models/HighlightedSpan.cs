namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// Single syntax highlighted token span inside one code line.
/// Contains the segment text and resolved typography.
/// Produced by <see cref="Services.ICodeSyntaxService"/> on a background thread
/// and consumed by the preview builder on the UI thread.
/// </summary>
public sealed record HighlightedSpan(
    string Text,
    string? ForegroundHex,
    bool IsBold,
    bool IsItalic);
