using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Lightweight virtualized container representing a single MarkdownChunk in the preview visual tree.
/// Purpose: acts as a fixed-height empty spacer when out of view, and holds realized controls when visible.
/// Usage: instantiated by EditorPreviewCoordinator, placed in PreviewBlocksControl.
/// Key UI elements: StackPanel base, materialized child controls when realized, empty spacer when virtualized.
/// Used In: EditorPreviewCoordinator, EditorView.
/// </summary>
public sealed class PreviewChunkContainer : StackPanel
{
    public MarkdownChunk Chunk { get; private set; }

    public bool IsRealized { get; private set; }

    public PreviewChunkContainer(MarkdownChunk chunk)
    {
        Chunk = chunk;
        Tag = chunk.Span;
        Height = chunk.EffectiveHeight;
        MinHeight = 1;
    }

    /// <summary>
    /// Materializes the chunk by attaching real Avalonia controls and releasing the fixed height constraint.
    /// Invoked by EditorPreviewCoordinator.
    /// </summary>
    public void Realize(IReadOnlyList<Control> controls)
    {
        if (IsRealized)
            return;

        IsRealized = true;
        Height = double.NaN;
        Children.Clear();
        Children.AddRange(controls);
    }

    /// <summary>
    /// Replaces child controls with updated elements while maintaining realized layout state.
    /// Invoked by EditorPreviewCoordinator.
    /// </summary>
    public void RefreshRealized(IReadOnlyList<Control> controls)
    {
        Children.Clear();
        Children.AddRange(controls);
        Height = double.NaN;
        IsRealized = true;
    }

    /// <summary>
    /// Dematerializes the chunk by saving its measured height, detaching controls and restoring spacer height.
    /// Invoked by EditorPreviewCoordinator.
    /// </summary>
    public void Dematerialize()
    {
        if (!IsRealized)
            return;

        if (Bounds.Height > 0)
            Chunk.ActualMeasuredHeight = Bounds.Height;

        Children.Clear();
        Height = Chunk.EffectiveHeight;
        IsRealized = false;
    }

    /// <summary>
    /// Updates the container with an updated chunk representation and adjusts layout heights.
    /// Invoked by EditorPreviewCoordinator.
    /// </summary>
    public void UpdateChunk(MarkdownChunk newChunk)
    {
        Chunk = newChunk;
        Tag = newChunk.Span;

        if (!IsRealized)
            Height = newChunk.EffectiveHeight;
    }

    /// <summary>
    /// Updates the cached actual measured height when layout settles.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty && IsRealized && Bounds.Height > 0)
            Chunk.ActualMeasuredHeight = Bounds.Height;
    }
}
