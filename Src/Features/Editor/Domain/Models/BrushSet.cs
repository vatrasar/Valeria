using Avalonia.Media;
using Valeria.Src.Features.Editor.Domain.Services;

namespace Valeria.Src.Features.Editor.Domain.Models;

internal sealed record BrushSet(IBrush Body, IBrush Heading)
{
    public static BrushSet Default(ThemeResources theme)
    {
        return new BrushSet(theme.Body, theme.Heading);
    }

    public static BrushSet Quote(ThemeResources theme)
    {
        return new BrushSet(theme.QuoteForeground, theme.QuoteForeground);
    }
}
