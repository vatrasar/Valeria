using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Selectable text block customized for markdown preview rendering.
/// Exposes precise text hit-testing and preserves active text selection
/// when focus shifts across multiple rendered preview blocks.
/// </summary>
public class PreviewTextBlock : SelectableTextBlock
{
    private static readonly IBrush DefaultSelectionBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x4F, 0x78));
    private static readonly IBrush DefaultSelectionForegroundBrush = Brushes.White;

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    static PreviewTextBlock()
    {
        SelectionBrushProperty.OverrideDefaultValue<PreviewTextBlock>(DefaultSelectionBrush);
        SelectionForegroundBrushProperty.OverrideDefaultValue<PreviewTextBlock>(DefaultSelectionForegroundBrush);
    }

    /// <summary>
    /// Gets or sets a value indicating whether text selection should be retained when focus is lost.
    /// </summary>
    public bool RetainSelectionOnLostFocus { get; set; } = true;

    /// <summary>
    /// Calculates the character index corresponding to the specified point relative to this control.
    /// </summary>
    public int GetTextPositionAt(Point point)
    {
        Thickness padding = Padding;
        Point adjusted = point - new Point(padding.Left, padding.Top);
        double clampedX = Math.Clamp(adjusted.X, 0.0, Math.Max(TextLayout.WidthIncludingTrailingWhitespace, 0.0));
        double clampedY = Math.Clamp(adjusted.Y, 0.0, Math.Max(TextLayout.Height, 0.0));

        var hit = TextLayout.HitTestPoint(new Point(clampedX, clampedY));
        return Math.Clamp(hit.TextPosition, 0, GetTextLength());
    }

    /// <summary>
    /// Gets the total character length of the rendered text or inlines.
    /// </summary>
    public int GetTextLength()
    {
        return GetPlainText().Length;
    }

    /// <summary>
    /// Gets the full plain text representation of this block.
    /// </summary>
    public string GetPlainText()
    {
        if (Inlines is not null && Inlines.Count > 0)
            return Inlines.Text ?? string.Empty;

        return Text ?? string.Empty;
    }

    /// <summary>
    /// Gets the currently selected text substring within this block.
    /// </summary>
    public string GetSelectedPlainText()
    {
        string full = GetPlainText();
        int length = full.Length;

        if (length == 0)
            return string.Empty;

        int start = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, length);
        int end = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, length);

        if (start == end)
            return string.Empty;

        return full.Substring(start, end - start);
    }

    /// <summary>
    /// Sets the selection range clamped to the boundaries of the text.
    /// </summary>
    public void SetSelectionRange(int start, int end)
    {
        int length = GetTextLength();
        int clampedStart = Math.Clamp(start, 0, length);
        int clampedEnd = Math.Clamp(end, 0, length);

        SelectionStart = clampedStart;
        SelectionEnd = clampedEnd;
    }

    /// <summary>
    /// Clears any active text selection in this block.
    /// </summary>
    public new void ClearSelection()
    {
        SelectionStart = 0;
        SelectionEnd = 0;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (this.TryFindResource("EditorSelectionBrush", out object? brush) && brush is IBrush selectionBrush)
            SelectionBrush = selectionBrush;
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        if (RetainSelectionOnLostFocus)
            return;

        base.OnLostFocus(e);
    }
}
