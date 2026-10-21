using System;
using Avalonia.Media;
using Valeria.Src.Features.Editor.Domain.Services;

namespace Valeria.Src.Features.Editor.Domain.Models;

internal sealed record BrushSet(IBrush Body, IBrush Heading, Func<int, IBrush>? HeadingResolver = null)
{
    public IBrush GetHeadingBrush(int level) => HeadingResolver?.Invoke(level) ?? Heading;

    public static BrushSet Default(ThemeResources theme)
    {
        return new BrushSet(theme.Body, theme.Heading, theme.HeadingBrush);
    }

    public static BrushSet Quote(ThemeResources theme)
    {
        return new BrushSet(theme.QuoteForeground, theme.QuoteForeground, _ => theme.QuoteForeground);
    }
}
