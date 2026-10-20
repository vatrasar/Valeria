namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Represents a targeted string replacement for an ordered list item number.
/// </summary>
public sealed record ListNumberAdjustment(int Offset, int Length, string NewNumber);
