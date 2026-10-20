using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Resolves preview block elements and calculates intra-block line and character offsets for sync.
/// </summary>
public static class EditorSyncResolver
{
    public static (Control? Control, BlockSourceSpan Span) ResolveBlockFromPreviewCoordinates(
        PointerEventArgs args,
        IReadOnlyList<Control> previewBlocks,
        Control relativeTo)
    {
        if (previewBlocks.Count == 0)
            return (null, BlockSourceSpan.Empty);

        Point clickPosition = args.GetPosition(relativeTo);
        Control? matchedBlock = FindNearestBlockAtY(previewBlocks, clickPosition.Y, relativeTo);

        if (matchedBlock is null)
            return (null, BlockSourceSpan.Empty);

        Control deepestControl = FindDeepestBlockAtY(matchedBlock, clickPosition.Y, relativeTo);
        if (TryGetBlockSpan(deepestControl, out BlockSourceSpan deepestSpan))
            return (deepestControl, deepestSpan);

        if (TryGetBlockSpan(matchedBlock, out BlockSourceSpan blockSpan))
            return (matchedBlock, blockSpan);

        return (null, BlockSourceSpan.Empty);
    }

    public static (Control? Control, BlockSourceSpan Span) FindClickedBlock(Visual sourceVisual)
    {
        Visual? current = sourceVisual;

        while (current is not null)
        {
            if (current is Control candidate && TryGetBlockSpan(candidate, out BlockSourceSpan span))
                return (candidate, span);

            current = current.GetVisualParent();
        }

        return (null, BlockSourceSpan.Empty);
    }

    public static int CalculateIntraBlockOffset(
        Control control,
        BlockSourceSpan span,
        Point clickPosition,
        TextEditor sourceEditor)
    {
        int docLength = Math.Max(0, sourceEditor.Document?.TextLength ?? 0);
        if (span.IsEmpty || docLength == 0)
            return 0;

        if (span.StartOffset >= 0 && span.EndOffset >= span.StartOffset && span.StartOffset <= docLength)
            return CalculateSpanOffset(control, span, clickPosition, docLength);

        return ResolveFallbackLineOffset(span, control, clickPosition, sourceEditor, docLength);
    }

    public static bool TryGetBlockSpan(Control control, out BlockSourceSpan span)
    {
        if (control.Tag is BlockSourceSpan directSpan)
        {
            span = directSpan;
            return true;
        }

        if (control.Tag is BlockLineRange lineRange)
        {
            span = new BlockSourceSpan(0, 0, lineRange.StartLine, lineRange.EndLine);
            return true;
        }

        span = BlockSourceSpan.Empty;
        return false;
    }

    public static bool IsInteractiveChild(Visual visual)
    {
        Visual? current = visual;

        while (current is not null)
        {
            if (current is Button or CheckBox or TextBox or ToggleButton)
                return true;

            current = current.GetVisualParent();
        }

        return false;
    }

    public static bool IsWithinSearchPane(Visual visual)
    {
        Visual? current = visual;

        while (current is not null)
        {
            if (current is Control control && (control.Name == "PreviewSearchBar" || control.Classes.Contains("search-bar")))
                return true;

            current = current.GetVisualParent();
        }

        return false;
    }

    private static int CalculateSpanOffset(Control control, BlockSourceSpan span, Point clickPosition, int docLength)
    {
        double height = control.Bounds.Height;
        if (height <= 36 || span.EndOffset <= span.StartOffset)
            return Math.Clamp(span.StartOffset, 0, docLength);

        double fraction = Math.Clamp(clickPosition.Y / height, 0.0, 1.0);
        int computedOffset = span.StartOffset + (int)Math.Round(fraction * (span.EndOffset - span.StartOffset));
        return Math.Clamp(computedOffset, 0, docLength);
    }

    private static int ResolveFallbackLineOffset(
        BlockSourceSpan span,
        Control control,
        Point clickPosition,
        TextEditor sourceEditor,
        int docLength)
    {
        if (sourceEditor.Document is null || span.StartLine <= 0)
            return 0;

        int targetLine = CalculateIntraBlockLine(span, control.Bounds.Height, clickPosition.Y);
        int clampedLine = Math.Clamp(targetLine, 1, sourceEditor.Document.LineCount);
        return Math.Clamp(sourceEditor.Document.GetLineByNumber(clampedLine).Offset, 0, docLength);
    }

    private static int CalculateIntraBlockLine(BlockSourceSpan span, double controlHeight, double clickY)
    {
        if (span.EndLine <= span.StartLine || controlHeight <= 36)
            return span.StartLine;

        double fraction = Math.Clamp(clickY / controlHeight, 0.0, 1.0);
        int lineCount = span.EndLine - span.StartLine + 1;
        return span.StartLine + (int)Math.Floor(fraction * lineCount);
    }

    private static Control? FindNearestBlockAtY(IReadOnlyList<Control> blocks, double clickY, Control relativeTo)
    {
        Control? nearest = null;
        double shortestDistance = double.MaxValue;

        foreach (Control block in blocks)
            EvaluateBlockProximity(block, clickY, relativeTo, ref nearest, ref shortestDistance);

        return nearest;
    }

    private static void EvaluateBlockProximity(
        Control block,
        double clickY,
        Control relativeTo,
        ref Control? nearest,
        ref double shortestDistance)
    {
        Point? position = block.TranslatePoint(new Point(0, 0), relativeTo);
        if (!position.HasValue)
            return;

        double top = position.Value.Y;
        double bottom = top + block.Bounds.Height;
        double distance = CalculateVerticalDistance(clickY, top, bottom);

        if (distance < shortestDistance)
        {
            shortestDistance = distance;
            nearest = block;
        }
    }

    private static double CalculateVerticalDistance(double y, double top, double bottom)
    {
        if (y >= top && y <= bottom)
            return 0;

        if (y < top)
            return top - y;

        return y - bottom;
    }

    private static Control FindDeepestBlockAtY(Control parent, double clickY, Control relativeTo)
    {
        foreach (Control child in ResolveChildControls(parent))
        {
            Control? match = MatchDeepestChildAtY(child, clickY, relativeTo);
            if (match is not null)
                return match;
        }

        return parent;
    }

    private static Control? MatchDeepestChildAtY(Control child, double clickY, Control relativeTo)
    {
        Point? childPoint = child.TranslatePoint(new Point(0, 0), relativeTo);
        if (!childPoint.HasValue)
            return null;

        double top = childPoint.Value.Y;
        double bottom = top + child.Bounds.Height;

        if (clickY < top || clickY > bottom)
            return null;

        if (!TryGetBlockSpan(child, out _))
            return null;

        return FindDeepestBlockAtY(child, clickY, relativeTo);
    }

    /// <summary>
    /// Finds the preview control that corresponds to the given text offset and line number.
    /// Used by <see cref="EditorScrollSyncController"/> to synchronize preview scrolling and highlighting.
    /// </summary>
    public static Control? FindBlockForOffset(int offset, int line, IReadOnlyList<Control> blocks)
    {
        Control? nearestPrecedingBlock = null;

        foreach (Control block in blocks)
        {
            if (TryMatchBlockForOffset(block, offset, line, ref nearestPrecedingBlock, out Control? matched))
                return matched;
        }

        return nearestPrecedingBlock ?? blocks.FirstOrDefault();
    }

    private static bool TryMatchBlockForOffset(
        Control block,
        int offset,
        int line,
        ref Control? nearestPreceding,
        out Control? matched)
    {
        matched = null;

        if (!TryGetBlockSpan(block, out BlockSourceSpan span) || span.IsEmpty)
            return false;

        bool matchesOffset = span.StartOffset >= 0 && span.EndOffset >= span.StartOffset && span.ContainsOffset(offset);
        bool matchesLine = line > 0 && span.ContainsLine(line);

        if (matchesOffset || matchesLine)
        {
            matched = FindDeeperBlockForOffset(block, offset, line) ?? block;
            return true;
        }

        if (span.StartOffset <= offset || (line > 0 && span.StartLine <= line))
            nearestPreceding = block;

        return false;
    }

    private static Control? FindDeeperBlockForOffset(Control parent, int offset, int line)
    {
        foreach (Control child in ResolveChildControls(parent))
        {
            Control? match = MatchDeeperChildForOffset(child, offset, line);
            if (match is not null)
                return match;
        }

        return null;
    }

    private static Control? MatchDeeperChildForOffset(Control child, int offset, int line)
    {
        if (!TryGetBlockSpan(child, out BlockSourceSpan span))
            return null;

        bool matchesOffset = span.StartOffset >= 0 && span.EndOffset >= span.StartOffset && span.ContainsOffset(offset);
        bool matchesLine = line > 0 && span.ContainsLine(line);

        if (matchesOffset || matchesLine)
            return FindDeeperBlockForOffset(child, offset, line) ?? child;

        return null;
    }

    private static IEnumerable<Control> ResolveChildControls(Control parent)
    {
        return parent switch
        {
            Panel panel => panel.Children.OfType<Control>(),
            ContentControl content => content.Content is Control control ? [control] : Enumerable.Empty<Control>(),
            Border border when border.Child is Panel innerPanel => innerPanel.Children.OfType<Control>(),
            Border border => border.Child is Control control ? [control] : Enumerable.Empty<Control>(),
            _ => parent.GetVisualChildren().OfType<Control>()
        };
    }
}
