using Valeria.Src.Core.Domain.Enums;

namespace Valeria.Src.Core.Domain.Models;

/// <summary>
/// Represents a selectable heading color palette option for settings UI binding.
/// </summary>
public sealed record HeadingPaletteOption(HeadingColorPalette Value, string DisplayName);
