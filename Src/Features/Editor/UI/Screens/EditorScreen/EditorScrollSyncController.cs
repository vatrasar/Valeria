using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Controls two-way click and scroll synchronization between the text editor and preview pane.
/// </summary>
public sealed class EditorScrollSyncController
{
    private readonly TextEditor _sourceEditor;
    private readonly ScrollViewer _previewScroller;
    private readonly ItemsControl _previewBlocksControl;
    private readonly Func<EditorViewModel?> _getViewModel;
    private bool _syncingEditor;
    private CancellationTokenSource? _highlightCts;
    private Control? _currentHighlightedControl;
    private int _selectionPulseId;

    public EditorScrollSyncController(
        TextEditor sourceEditor,
        ScrollViewer previewScroller,
        ItemsControl previewBlocksControl,
        Func<EditorViewModel?> getViewModel)
    {
        _sourceEditor = sourceEditor;
        _previewScroller = previewScroller;
        _previewBlocksControl = previewBlocksControl;
        _getViewModel = getViewModel;
    }

    public void Attach(CompositeDisposable disposables)
    {
        _sourceEditor.TextArea.AddHandler(InputElement.PointerReleasedEvent, OnEditorPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        _previewScroller.AddHandler(InputElement.PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);

        Disposable.Create(() =>
        {
            _sourceEditor.TextArea.RemoveHandler(InputElement.PointerReleasedEvent, OnEditorPointerReleased);
            _previewScroller.RemoveHandler(InputElement.PointerPressedEvent, OnPreviewPointerPressed);
            _highlightCts?.Cancel();
            _highlightCts?.Dispose();
            _highlightCts = null;
        }).DisposeWith(disposables);
    }

    public void HandleEditorPointerReleased(int caretLineOrOffset)
    {
        if (_sourceEditor.Document is not null && caretLineOrOffset >= 1 && caretLineOrOffset <= _sourceEditor.Document.LineCount)
        {
            int offset = _sourceEditor.Document.GetLineByNumber(caretLineOrOffset).Offset;
            SyncPreviewToOffset(offset);
            return;
        }

        SyncPreviewToOffset(caretLineOrOffset);
    }

    public void HandlePreviewBlockClicked(Control clickedControl, BlockLineRange range, Point positionInBlock)
    {
        BlockSourceSpan span = EditorSyncResolver.TryGetBlockSpan(clickedControl, out BlockSourceSpan resolved)
            ? resolved
            : new BlockSourceSpan(0, 0, range.StartLine, range.EndLine);

        HandlePreviewBlockClicked(clickedControl, span, positionInBlock);
    }

    public void HandlePreviewBlockClicked(Control clickedControl, BlockSourceSpan span, Point positionInBlock)
    {
        int targetOffset = EditorSyncResolver.CalculateIntraBlockOffset(clickedControl, span, positionInBlock, _sourceEditor);
        JumpEditorToOffset(targetOffset, span);
        ApplyPreviewHighlight(clickedControl);
    }

    private void OnEditorPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_syncingEditor)
            return;

        SyncPreviewToOffset(_sourceEditor.CaretOffset);
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (args.Source is not Visual sourceVisual || EditorSyncResolver.IsInteractiveChild(sourceVisual) || EditorSyncResolver.IsWithinSearchPane(sourceVisual))
            return;

        (Control? clickedControl, BlockSourceSpan span) = EditorSyncResolver.FindClickedBlock(sourceVisual);

        if (clickedControl is null || span.IsEmpty)
        {
            EditorViewModel? viewModel = _getViewModel();
            IReadOnlyList<Control> blocks = viewModel?.State.PreviewBlocks ?? ImmutableList<Control>.Empty;
            (clickedControl, span) = EditorSyncResolver.ResolveBlockFromPreviewCoordinates(args, blocks, _previewBlocksControl);
        }

        if (clickedControl is null || span.IsEmpty)
            return;

        Point position = args.GetPosition(clickedControl);
        HandlePreviewBlockClicked(clickedControl, span, position);
    }

    private void SyncPreviewToOffset(int caretOffset)
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null || viewModel.State.PreviewBlocks.Count == 0 || _sourceEditor.Document is null)
            return;

        int line = _sourceEditor.Document.GetLineByOffset(Math.Clamp(caretOffset, 0, _sourceEditor.Document.TextLength)).LineNumber;
        Control? matchingControl = EditorSyncResolver.FindBlockForOffset(caretOffset, line, viewModel.State.PreviewBlocks);

        if (matchingControl is null)
            return;

        ScrollPreviewBlockIntoView(matchingControl);
        ApplyPreviewHighlight(matchingControl);
    }

    private void ScrollPreviewBlockIntoView(Control control)
    {
        Point? position = control.TranslatePoint(new Point(0, 0), _previewBlocksControl);
        if (!position.HasValue)
            return;

        double targetY = position.Value.Y;
        double currentY = _previewScroller.Offset.Y;
        double viewportHeight = _previewScroller.Viewport.Height;

        if (targetY < currentY || targetY > currentY + viewportHeight - 50)
        {
            double desiredY = Math.Max(0, targetY - (viewportHeight * 0.3));
            _previewScroller.Offset = new Vector(_previewScroller.Offset.X, desiredY);
        }
    }

    private void ApplyPreviewHighlight(Control block)
    {
        if (_currentHighlightedControl is not null && _currentHighlightedControl != block)
            _currentHighlightedControl.Classes.Remove("sync-highlight");

        _currentHighlightedControl = block;
        block.Classes.Add("sync-highlight");

        _highlightCts?.Cancel();
        _highlightCts?.Dispose();
        _highlightCts = new CancellationTokenSource();
        CancellationToken token = _highlightCts.Token;

        _ = Task.Delay(1400, token).ContinueWith(_ =>
        {
            if (token.IsCancellationRequested)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (!token.IsCancellationRequested && _currentHighlightedControl == block)
                {
                    block.Classes.Remove("sync-highlight");
                    _currentHighlightedControl = null;
                }
            });
        }, TaskScheduler.Default);
    }

    private void JumpEditorToOffset(int targetOffset, BlockSourceSpan span)
    {
        if (_sourceEditor.Document is null)
            return;

        _syncingEditor = true;

        try
        {
            int docLength = _sourceEditor.Document.TextLength;
            int clampedTarget = Math.Clamp(targetOffset, 0, docLength);

            SelectAndScrollEditor(clampedTarget, span, docLength);
            TriggerSelectionPulse(clampedTarget);
        }
        finally
        {
            _syncingEditor = false;
        }

        _sourceEditor.Focus();
    }

    private void SelectAndScrollEditor(int clampedTarget, BlockSourceSpan span, int docLength)
    {
        if (span.StartOffset >= 0 && span.EndOffset > span.StartOffset && span.EndOffset <= docLength)
        {
            _sourceEditor.Select(span.StartOffset, span.EndOffset - span.StartOffset);
            _sourceEditor.TextArea.Caret.Offset = clampedTarget;
        }
        else
        {
            _sourceEditor.CaretOffset = clampedTarget;
            _sourceEditor.SelectionLength = 0;
        }

        _sourceEditor.ScrollTo(_sourceEditor.TextArea.Caret.Line, _sourceEditor.TextArea.Caret.Column);
    }

    private void TriggerSelectionPulse(int finalOffset)
    {
        int pulseId = Interlocked.Increment(ref _selectionPulseId);

        _ = Task.Delay(800).ContinueWith(_ =>
        {
            if (pulseId != _selectionPulseId)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (pulseId == _selectionPulseId)
                    _sourceEditor.Select(finalOffset, 0);
            });
        }, TaskScheduler.Default);
    }
}
