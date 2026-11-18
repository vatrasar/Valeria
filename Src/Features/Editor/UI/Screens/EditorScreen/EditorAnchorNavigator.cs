using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Controls navigation to in-document anchors and table-of-contents targets across both virtualized chunks and direct preview modes.
/// </summary>
public sealed class EditorAnchorNavigator
{
    private const double TargetTopMargin = 20.0;

    private readonly ScrollViewer _previewScroller;
    private readonly ItemsControl _previewBlocksControl;
    private readonly EditorScrollSyncController _scrollSyncController;
    private readonly Func<EditorViewModel?> _getViewModel;

    public EditorAnchorNavigator(
        ScrollViewer previewScroller,
        ItemsControl previewBlocksControl,
        EditorScrollSyncController scrollSyncController,
        Func<EditorViewModel?> getViewModel)
    {
        _previewScroller = previewScroller;
        _previewBlocksControl = previewBlocksControl;
        _scrollSyncController = scrollSyncController;
        _getViewModel = getViewModel;
    }

    /// <summary>
    /// Navigates the preview scroller and source editor to the heading matching the specified anchor target.
    /// Returns true if a matching heading was found and navigation initiated; otherwise false.
    /// </summary>
    public bool NavigateToAnchor(string rawAnchor)
    {
        string target = HeadingAnchorHelper.NormalizeAnchor(rawAnchor);
        if (string.IsNullOrEmpty(target))
            return false;

        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return false;

        EditorPreviewCoordinator coordinator = viewModel.PreviewCoordinator;

        if (coordinator.IsChunkedMode)
            return NavigateChunkedAnchor(coordinator, target);

        return NavigateDirectAnchor(viewModel, target);
    }

    private bool NavigateChunkedAnchor(EditorPreviewCoordinator coordinator, string target)
    {
        (PreviewChunkContainer? container, HeadingBlock? heading, int containerIndex) = coordinator.FindHeadingByAnchor(target);
        if (container is null || heading is null)
            return false;

        bool wasRealized = container.IsRealized;
        if (!wasRealized)
            coordinator.RealizeContainer(container);

        Control? headingControl = FindHeadingControl(container, heading);
        if (headingControl is null)
            return false;

        if (wasRealized && headingControl.IsArrangeValid && headingControl.Bounds.Height > 0)
        {
            ScrollToHeadingDirect(headingControl, heading);
            return true;
        }

        ScrollToHeadingDeferred(coordinator.ChunkContainers, containerIndex, headingControl, heading);
        return true;
    }

    private bool NavigateDirectAnchor(EditorViewModel viewModel, string target)
    {
        IReadOnlyList<Control> blocks = viewModel.State.PreviewBlocks;
        Control? headingControl = FindHeadingControlInDirectBlocks(blocks, target, out BlockSourceSpan span);

        if (headingControl is null)
            return false;

        Point? position = headingControl.TranslatePoint(new Point(0, 0), _previewBlocksControl);
        double targetY = position?.Y ?? 0;

        ScrollToY(targetY);
        _scrollSyncController.ApplyPreviewHighlight(headingControl, force: true);

        if (viewModel.State.IsEditorVisible && !span.IsEmpty)
            _scrollSyncController.JumpEditorToOffset(span.StartOffset, span);

        return true;
    }

    private void ScrollToHeadingDirect(Control headingControl, HeadingBlock heading)
    {
        Point? position = headingControl.TranslatePoint(new Point(0, 0), _previewBlocksControl);
        if (position.HasValue)
            ScrollToY(position.Value.Y);

        _scrollSyncController.ApplyPreviewHighlight(headingControl, force: true);
        SyncEditor(heading);
    }

    private void ScrollToHeadingDeferred(
        IReadOnlyList<PreviewChunkContainer> containers,
        int containerIndex,
        Control headingControl,
        HeadingBlock heading)
    {
        double estimatedY = CalculatePrecedingHeight(containers, containerIndex);
        ScrollToY(estimatedY);

        Dispatcher.UIThread.Post(() =>
        {
            Point? exactPosition = headingControl.TranslatePoint(new Point(0, 0), _previewBlocksControl);
            if (exactPosition.HasValue)
                ScrollToY(exactPosition.Value.Y);

            _scrollSyncController.ApplyPreviewHighlight(headingControl, force: true);
            SyncEditor(heading);
        }, DispatcherPriority.Loaded);
    }

    private void ScrollToY(double y)
    {
        double desiredY = Math.Max(0, y - TargetTopMargin);
        _previewScroller.Offset = new Vector(_previewScroller.Offset.X, desiredY);
    }

    private void SyncEditor(HeadingBlock heading)
    {
        if (_getViewModel()?.State.IsEditorVisible == true && !heading.SourceSpan.IsEmpty)
            _scrollSyncController.JumpEditorToOffset(heading.SourceSpan.StartOffset, heading.SourceSpan);
    }

    private static double CalculatePrecedingHeight(IReadOnlyList<PreviewChunkContainer> containers, int targetIndex)
    {
        double cumulative = 0;
        int count = Math.Min(targetIndex, containers.Count);

        for (int i = 0; i < count; i++)
            cumulative += containers[i].Chunk.EffectiveHeight;

        return cumulative;
    }

    private static Control? FindHeadingControl(PreviewChunkContainer container, HeadingBlock heading)
    {
        foreach (Control child in container.Children)
        {
            Control? match = MatchHeadingControl(child, heading);
            if (match is not null)
                return match;
        }

        return null;
    }

    private static Control? MatchHeadingControl(Control control, HeadingBlock heading)
    {
        string? anchorId = MarkdownLink.GetAnchorId(control);
        if (!string.IsNullOrEmpty(anchorId) && !string.IsNullOrEmpty(heading.AnchorId) && string.Equals(anchorId, heading.AnchorId, StringComparison.OrdinalIgnoreCase))
            return control;

        if (control.Tag is BlockSourceSpan span && !span.IsEmpty && !heading.SourceSpan.IsEmpty && span.StartOffset == heading.SourceSpan.StartOffset)
            return control;

        if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
            return MatchHeadingControl(contentChild, heading);

        if (control is Panel panel)
        {
            foreach (Control child in panel.Children)
            {
                Control? nested = MatchHeadingControl(child, heading);
                if (nested is not null)
                    return nested;
            }
        }

        return null;
    }

    private static Control? FindHeadingControlInDirectBlocks(
        IReadOnlyList<Control> blocks,
        string target,
        out BlockSourceSpan span)
    {
        span = BlockSourceSpan.Empty;

        foreach (Control block in blocks)
        {
            string? anchorId = MarkdownLink.GetAnchorId(block);
            if (!string.IsNullOrEmpty(anchorId) && MatchesAnchorId(anchorId, target))
            {
                span = block.Tag is BlockSourceSpan directSpan ? directSpan : BlockSourceSpan.Empty;
                return block;
            }
        }

        return null;
    }

    private static bool MatchesAnchorId(string anchorId, string target)
    {
        return string.Equals(anchorId, target, StringComparison.OrdinalIgnoreCase) ||
               anchorId.StartsWith(target, StringComparison.OrdinalIgnoreCase) ||
               target.StartsWith(anchorId, StringComparison.OrdinalIgnoreCase);
    }
}
