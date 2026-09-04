using System.Collections.Immutable;

namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// One code line with its syntax highlighted spans.
/// Produced by <see cref="Services.ICodeSyntaxService"/> on a background thread
/// and consumed by the preview builder on the UI thread.
/// </summary>
public sealed record HighlightedLine(ImmutableList<HighlightedSpan> Spans);