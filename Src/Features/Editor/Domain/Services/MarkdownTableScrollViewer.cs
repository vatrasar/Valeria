using System;
using Avalonia;
using Avalonia.Controls;

namespace Valeria.Src.Features.Editor.Domain.Services;

internal sealed class MarkdownTableScrollViewer : ScrollViewer
{
    private const double DefaultMinColumnWidth = 60;
    private const double FallbackWidth = 800;

    public int ColumnCount { get; init; } = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Content is Control child)
            AdjustChildWidth(child, availableSize.Width);

        return base.MeasureOverride(availableSize);
    }

    private void AdjustChildWidth(Control child, double availableWidth)
    {
        double minTableWidth = ColumnCount * DefaultMinColumnWidth;
        double viewportWidth = ResolveViewportWidth(availableWidth, minTableWidth);
        double targetWidth = Math.Max(minTableWidth, viewportWidth);

        if (double.IsNaN(child.Width) || Math.Abs(child.Width - targetWidth) > 0.5)
            child.Width = targetWidth;
    }

    private double ResolveViewportWidth(double availableWidth, double minTableWidth)
    {
        if (!double.IsInfinity(availableWidth))
            return availableWidth;

        if (Bounds.Width > 0)
            return Bounds.Width;

        return minTableWidth > 0 ? minTableWidth : FallbackWidth;
    }
}
