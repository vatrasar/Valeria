using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Controls cross-block text selection, continuous drag selection, and multi-block copy operations
/// across markdown preview elements without requiring a web view or HTML document.
/// </summary>
public sealed class PreviewSelectionController
{
    private const double DragThreshold = 4.0;
    private const double AutoScrollZone = 40.0;

    private readonly ScrollViewer _previewScroller;
    private readonly ItemsControl _previewBlocksControl;
    private readonly Func<IReadOnlyList<Control>> _getPreviewBlocks;
    private readonly Action<Control, Point>? _onBlockClicked;
    private readonly Action? _onSelectionStarted;
    private readonly Func<string>? _getMarkdownText;

    private bool _isMouseDown;
    private bool _isSelecting;
    private Point _pressedPoint;
    private PreviewTextBlock? _startBlock;
    private int _startCharIndex;

    public bool HasSelection => GetSelectedBlocks().Any(b => b.SelectionStart != b.SelectionEnd);

    public PreviewSelectionController(
        ScrollViewer previewScroller,
        ItemsControl previewBlocksControl,
        Func<IReadOnlyList<Control>> getPreviewBlocks,
        Action<Control, Point>? onBlockClicked = null,
        Action? onSelectionStarted = null,
        Func<string>? getMarkdownText = null)
    {
        _previewScroller = previewScroller;
        _previewBlocksControl = previewBlocksControl;
        _getPreviewBlocks = getPreviewBlocks;
        _onBlockClicked = onBlockClicked;
        _onSelectionStarted = onSelectionStarted;
        _getMarkdownText = getMarkdownText;
    }

    /// <summary>
    /// Attaches tunnel and bubble pointer handlers for preview multi-block selection.
    /// </summary>
    public void Attach(CompositeDisposable disposables)
    {
        _previewScroller.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _previewScroller.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        _previewScroller.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        _previewScroller.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

        Disposable.Create(() =>
        {
            _previewScroller.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            _previewScroller.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            _previewScroller.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            _previewScroller.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
            ClearSelection();
        }).DisposeWith(disposables);
    }

    /// <summary>
    /// Retrieves concatenated plain text of all currently selected preview blocks.
    /// </summary>
    public string GetSelectedText()
    {
        List<string> selectedParts = [];
        IReadOnlyList<PreviewTextBlock> allBlocks = CollectAllBlocks();

        foreach (PreviewTextBlock block in allBlocks)
        {
            string part = block.GetSelectedPlainText();
            if (!string.IsNullOrEmpty(part))
                selectedParts.Add(part);
        }

        return string.Join("\n\n", selectedParts);
    }

    /// <summary>
    /// Retrieves the original markdown representation of the currently selected preview text.
    /// Falls back to plain text if markdown source mapping is unavailable.
    /// </summary>
    public string GetSelectedMarkdown()
    {
        List<PreviewTextBlock> selectedBlocks = GetSelectedBlocks();
        if (selectedBlocks.Count == 0)
            return string.Empty;

        string plainFallback = GetSelectedText();
        string? markdown = _getMarkdownText?.Invoke();

        if (string.IsNullOrEmpty(markdown))
            return plainFallback;

        return PreviewMarkdownExtractor.ExtractMarkdown(selectedBlocks, markdown, plainFallback);
    }

    /// <summary>
    /// Copies all active selected preview text across blocks as markdown into the system clipboard.
    /// </summary>
    public async Task CopySelectionToClipboardAsync()
    {
        string text = GetSelectedMarkdown();
        if (string.IsNullOrEmpty(text))
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(_previewScroller);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// Clears any active selection across all rendered preview blocks.
    /// </summary>
    public void ClearSelection()
    {
        IReadOnlyList<PreviewTextBlock> allBlocks = CollectAllBlocks();
        foreach (PreviewTextBlock block in allBlocks)
            block.ClearSelection();
    }

    /// <summary>
    /// Selects all text across all rendered preview blocks.
    /// </summary>
    public void SelectAll()
    {
        IReadOnlyList<PreviewTextBlock> allBlocks = CollectAllBlocks();
        foreach (PreviewTextBlock block in allBlocks)
            block.SetSelectionRange(0, block.GetTextLength());
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        PointerPoint point = args.GetCurrentPoint(_previewBlocksControl);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (args.Source is Visual sourceVisual && (EditorSyncResolver.IsInteractiveChild(sourceVisual) || EditorSyncResolver.IsWithinSearchPane(sourceVisual)))
            return;

        _pressedPoint = args.GetPosition(_previewBlocksControl);

        PreviewTextBlock? clickedBlock = (args.Source as Visual)?.FindAncestorOfType<PreviewTextBlock>() ?? (args.Source as PreviewTextBlock);
        if (clickedBlock is not null)
        {
            _startBlock = clickedBlock;
            Point localPoint = args.GetPosition(clickedBlock);
            _startCharIndex = clickedBlock.GetTextPositionAt(localPoint);
        }
        else
        {
            (_startBlock, _startCharIndex) = ResolveBlockAndOffset(_pressedPoint);
        }

        _isMouseDown = true;
        _isSelecting = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (!_isMouseDown || !args.GetCurrentPoint(_previewBlocksControl).Properties.IsLeftButtonPressed)
            return;

        Point currentPoint = args.GetPosition(_previewBlocksControl);
        Vector delta = currentPoint - _pressedPoint;

        if (!_isSelecting && delta.Length > DragThreshold)
        {
            _isSelecting = true;
            _onSelectionStarted?.Invoke();
            args.Pointer.Capture(_previewScroller);
        }

        if (!_isSelecting)
            return;

        HandleAutoScroll(args.GetPosition(_previewScroller));

        PreviewTextBlock? currentBlock = null;
        int currentCharIndex = 0;

        Visual? hitVisual = _previewBlocksControl.InputHitTest(currentPoint) as Visual;
        if (hitVisual is not null)
        {
            currentBlock = hitVisual as PreviewTextBlock ?? hitVisual.FindAncestorOfType<PreviewTextBlock>();
            if (currentBlock is not null)
                currentCharIndex = currentBlock.GetTextPositionAt(args.GetPosition(currentBlock));
        }

        if (currentBlock is null)
            (currentBlock, currentCharIndex) = ResolveBlockAndOffset(currentPoint);

        if (_startBlock is not null && currentBlock is not null)
            UpdateSelectionRange(_startBlock, _startCharIndex, currentBlock, currentCharIndex);

        args.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (!_isMouseDown)
            return;

        bool wasSelecting = _isSelecting;
        _isMouseDown = false;
        _isSelecting = false;
        args.Pointer.Capture(null);

        if (wasSelecting)
        {
            args.Handled = true;
            return;
        }

        ClearSelection();

        if (args.Source is Visual sourceVisual && !EditorSyncResolver.IsInteractiveChild(sourceVisual) && !EditorSyncResolver.IsWithinSearchPane(sourceVisual))
        {
            Control? targetBlock = sourceVisual as Control ?? sourceVisual.FindAncestorOfType<Control>();
            if (targetBlock is not null)
                _onBlockClicked?.Invoke(targetBlock, _pressedPoint);
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args)
    {
        _isMouseDown = false;
        _isSelecting = false;
    }

    private void HandleAutoScroll(Point scrollerPoint)
    {
        double viewportHeight = _previewScroller.Viewport.Height;
        if (viewportHeight <= 0)
            return;

        if (scrollerPoint.Y < AutoScrollZone)
        {
            double step = Math.Max(10, AutoScrollZone - scrollerPoint.Y);
            _previewScroller.Offset = new Vector(_previewScroller.Offset.X, Math.Max(0, _previewScroller.Offset.Y - step));
            return;
        }

        if (scrollerPoint.Y > viewportHeight - AutoScrollZone)
        {
            double step = Math.Max(10, scrollerPoint.Y - (viewportHeight - AutoScrollZone));
            _previewScroller.Offset = new Vector(_previewScroller.Offset.X, _previewScroller.Offset.Y + step);
        }
    }

    private (PreviewTextBlock? Block, int CharIndex) ResolveBlockAndOffset(Point point)
    {
        IReadOnlyList<PreviewTextBlock> allBlocks = CollectAllBlocks();
        if (allBlocks.Count == 0)
            return (null, 0);

        PreviewTextBlock? hitBlock = FindBlockUnderPoint(point, allBlocks);
        if (hitBlock is not null)
            return (hitBlock, CalculateLocalCharIndex(hitBlock, point));

        PreviewTextBlock nearest = FindNearestBlockVertically(point.Y, allBlocks);
        return (nearest, CalculateClampedCharIndex(nearest, point));
    }

    private PreviewTextBlock? FindBlockUnderPoint(Point point, IReadOnlyList<PreviewTextBlock> blocks)
    {
        foreach (PreviewTextBlock block in blocks)
        {
            Point? origin = block.TranslatePoint(new Point(0, 0), _previewBlocksControl);
            if (!origin.HasValue)
                continue;

            Rect bounds = new(origin.Value, block.Bounds.Size);
            if (bounds.Contains(point))
                return block;
        }

        return null;
    }

    private PreviewTextBlock FindNearestBlockVertically(double y, IReadOnlyList<PreviewTextBlock> blocks)
    {
        PreviewTextBlock nearest = blocks[0];
        double minDistance = double.MaxValue;

        foreach (PreviewTextBlock block in blocks)
        {
            Point? origin = block.TranslatePoint(new Point(0, 0), _previewBlocksControl);
            if (!origin.HasValue)
                continue;

            double top = origin.Value.Y;
            double bottom = top + block.Bounds.Height;
            double distance = y < top ? top - y : (y > bottom ? y - bottom : 0);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = block;
            }
        }

        return nearest;
    }

    private int CalculateLocalCharIndex(PreviewTextBlock block, Point pointInContainer)
    {
        Point? origin = block.TranslatePoint(new Point(0, 0), _previewBlocksControl);
        Point localPoint = origin.HasValue ? pointInContainer - origin.Value : pointInContainer;
        return block.GetTextPositionAt(localPoint);
    }

    private int CalculateClampedCharIndex(PreviewTextBlock block, Point pointInContainer)
    {
        Point? origin = block.TranslatePoint(new Point(0, 0), _previewBlocksControl);
        if (!origin.HasValue)
            return 0;

        if (pointInContainer.Y < origin.Value.Y)
            return 0;

        if (pointInContainer.Y > origin.Value.Y + block.Bounds.Height)
            return block.GetTextLength();

        Point localPoint = pointInContainer - origin.Value;
        return block.GetTextPositionAt(localPoint);
    }

    private void UpdateSelectionRange(
        PreviewTextBlock startBlock,
        int startChar,
        PreviewTextBlock endBlock,
        int endChar)
    {
        IReadOnlyList<PreviewTextBlock> allBlocks = CollectAllBlocks();
        int startIndex = IndexOfBlock(allBlocks, startBlock);
        int endIndex = IndexOfBlock(allBlocks, endBlock);

        if (startIndex < 0 || endIndex < 0)
            return;

        if (startIndex == endIndex)
        {
            ApplySingleBlockSelection(allBlocks, startIndex, startChar, endChar);
            return;
        }

        if (startIndex < endIndex)
        {
            ApplyDownwardSelection(allBlocks, startIndex, startChar, endIndex, endChar);
            return;
        }

        ApplyUpwardSelection(allBlocks, startIndex, startChar, endIndex, endChar);
    }

    private static int IndexOfBlock(IReadOnlyList<PreviewTextBlock> blocks, PreviewTextBlock target)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (ReferenceEquals(blocks[i], target))
                return i;
        }

        return -1;
    }

    private static void ApplySingleBlockSelection(
        IReadOnlyList<PreviewTextBlock> blocks,
        int index,
        int startChar,
        int endChar)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (i == index)
                blocks[i].SetSelectionRange(Math.Min(startChar, endChar), Math.Max(startChar, endChar));
            else
                blocks[i].ClearSelection();
        }
    }

    private static void ApplyDownwardSelection(
        IReadOnlyList<PreviewTextBlock> blocks,
        int startIndex,
        int startChar,
        int endIndex,
        int endChar)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (i < startIndex || i > endIndex)
                blocks[i].ClearSelection();
            else if (i == startIndex)
                blocks[i].SetSelectionRange(startChar, blocks[i].GetTextLength());
            else if (i == endIndex)
                blocks[i].SetSelectionRange(0, endChar);
            else
                blocks[i].SetSelectionRange(0, blocks[i].GetTextLength());
        }
    }

    private static void ApplyUpwardSelection(
        IReadOnlyList<PreviewTextBlock> blocks,
        int startIndex,
        int startChar,
        int endIndex,
        int endChar)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (i < endIndex || i > startIndex)
                blocks[i].ClearSelection();
            else if (i == endIndex)
                blocks[i].SetSelectionRange(endChar, blocks[i].GetTextLength());
            else if (i == startIndex)
                blocks[i].SetSelectionRange(0, startChar);
            else
                blocks[i].SetSelectionRange(0, blocks[i].GetTextLength());
        }
    }

    private List<PreviewTextBlock> GetSelectedBlocks()
    {
        return CollectAllBlocks().Where(b => b.SelectionStart != b.SelectionEnd).ToList();
    }

    private IReadOnlyList<PreviewTextBlock> CollectAllBlocks()
    {
        List<PreviewTextBlock> result = [];
        IReadOnlyList<Control> previewBlocks = _getPreviewBlocks();

        foreach (Control rootBlock in previewBlocks)
            CollectTextBlocksRecursive(rootBlock, result);

        return result;
    }

    private static void CollectTextBlocksRecursive(Control control, List<PreviewTextBlock> result)
    {
        if (control is PreviewTextBlock previewText)
        {
            result.Add(previewText);
            return;
        }

        if (control is Panel panel)
        {
            foreach (Control child in panel.Children)
                CollectTextBlocksRecursive(child, result);
            return;
        }

        if (control is Border border && border.Child is not null)
        {
            CollectTextBlocksRecursive(border.Child, result);
            return;
        }

        if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            CollectTextBlocksRecursive(contentChild, result);
            return;
        }

        if (control is ScrollViewer scrollViewer && scrollViewer.Content is Control scrollChild)
        {
            CollectTextBlocksRecursive(scrollChild, result);
            return;
        }

        foreach (Control child in control.GetVisualChildren().OfType<Control>())
            CollectTextBlocksRecursive(child, result);
    }
}
