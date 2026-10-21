using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Coordinates asynchronous markdown parsing, chunked preview virtualization, and background syntax highlighting.
/// </summary>
public sealed class EditorPreviewCoordinator
{
    private readonly IMarkdownPreviewBuilder _previewBuilder;
    private readonly ICodeSyntaxService _syntax;
    private readonly Func<EditorState> _getState;
    private readonly Action<Func<EditorState, EditorState>> _updateState;
    private readonly Action<int, bool> _onTaskToggled;
    private readonly List<PreviewChunkContainer> _chunkContainers = [];
    private PreviewLayoutMetrics _metrics = PreviewLayoutMetrics.CreateDefault();
    private string? _activeBaseDirectory;
    private string? _activeFilePath;
    private int _previewVersion;
    private bool _isChunkedMode;
    private double _lastViewportY;
    private double _lastViewportHeight = 800;

    public EditorPreviewCoordinator(
        IMarkdownPreviewBuilder previewBuilder,
        ICodeSyntaxService syntax,
        Func<EditorState> getState,
        Action<Func<EditorState, EditorState>> updateState,
        Action<int, bool> onTaskToggled)
    {
        _previewBuilder = previewBuilder;
        _syntax = syntax;
        _getState = getState;
        _updateState = updateState;
        _onTaskToggled = onTaskToggled;

        if (Dispatcher.UIThread.CheckAccess())
            _metrics = ThemeResources.Resolve().CreateMetrics();
    }

    /// <summary>
    /// Parses the document in the background and builds the preview representation.
    /// Uses multithreaded chunked virtualization for large files and direct rendering for small files.
    /// Invoked by EditorViewModel when document content changes.
    /// </summary>
    public async Task ProcessPreviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            int version = Interlocked.Increment(ref _previewVersion);
            string snapshot = _getState().MarkdownText;

            ImmutableList<MarkdownChunk> chunks = await Task.Run(
                () => MarkdownParallelParser.ParseDocument(snapshot, _metrics, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (IsStale(cancellationToken, version))
                return;

            string? filePath = _getState().FilePath;
            if (filePath != _activeFilePath)
            {
                _activeFilePath = filePath;
                _chunkContainers.Clear();
            }

            _activeBaseDirectory = string.IsNullOrEmpty(filePath) ? null : Path.GetDirectoryName(filePath);

            if (chunks.Count <= 1)
            {
                MarkdownContent directContent = chunks.Count == 1
                    ? new MarkdownContent(chunks[0].Blocks)
                    : MarkdownContent.Empty;
                await ProcessDirectPreviewAsync(directContent, version, cancellationToken);
                return;
            }

            await ProcessChunkedPreviewAsync(chunks, version, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RunOnUiThreadAsync(() => _updateState(state => state with { ErrorMessage = exception.ToString() }));
        }
    }

    /// <summary>
    /// Materializes visible chunks and dematerializes distant chunks based on viewport scrolling.
    /// Invoked by EditorView when the preview scroll offset changes.
    /// </summary>
    public void UpdateVisibleChunks(double viewportY, double viewportHeight)
    {
        _lastViewportY = viewportY;
        _lastViewportHeight = viewportHeight;

        if (!_isChunkedMode || _chunkContainers.Count == 0)
            return;

        List<CodeHighlightTarget> newTargets = [];
        RealizeChunksInRange(viewportY, viewportHeight, newTargets);

        if (newTargets.Count > 0)
            _ = HighlightCodeTargetsAsync(newTargets, CancellationToken.None);
    }

    private async Task ProcessDirectPreviewAsync(MarkdownContent content, int version, CancellationToken cancellationToken)
    {
        _isChunkedMode = false;
        _chunkContainers.Clear();

        PreviewBuildResult? built = null;
        await RunOnUiThreadAsync(async () =>
        {
            if (IsStale(cancellationToken, version))
                return;

            built = await _previewBuilder.BuildBlocksAsync(content, _onTaskToggled, _activeBaseDirectory, cancellationToken);

            if (IsStale(cancellationToken, version) || (built.Blocks.Count == 0 && content.Blocks.Count > 0))
                return;

            _metrics = ThemeResources.Resolve().CreateMetrics();

            _updateState(state => state with
            {
                PreviewBlocks = ImmutableList.CreateRange(built.Blocks),
                IsPreviewIdle = built.CodeTargets.Count == 0
            });
        }, DispatcherPriority.Normal);

        if (built is null || IsStale(cancellationToken, version) || built.CodeTargets.Count == 0)
            return;

        await HighlightCodeTargetsAsync(built.CodeTargets, cancellationToken);
    }

    private async Task ProcessChunkedPreviewAsync(ImmutableList<MarkdownChunk> chunks, int version, CancellationToken cancellationToken)
    {
        _isChunkedMode = true;
        List<CodeHighlightTarget> initialTargets = [];
        bool collectionChanged = false;

        await RunOnUiThreadAsync(() =>
        {
            if (IsStale(cancellationToken, version))
                return;

            _metrics = ThemeResources.Resolve().CreateMetrics();

            collectionChanged = SyncChunkContainers(chunks, initialTargets);

            RealizeChunksInRange(_lastViewportY, _lastViewportHeight, initialTargets);

            if (collectionChanged || _getState().PreviewBlocks.Count == 0)
            {
                _updateState(state => state with
                {
                    PreviewBlocks = ImmutableList.CreateRange<Control>(_chunkContainers),
                    IsPreviewIdle = initialTargets.Count == 0
                });
            }
            else
            {
                _updateState(state => state with
                {
                    IsPreviewIdle = initialTargets.Count == 0
                });
            }
        }, DispatcherPriority.Normal);

        if (initialTargets.Count > 0 && !IsStale(cancellationToken, version))
            await HighlightCodeTargetsAsync(initialTargets, cancellationToken);
    }

    private bool SyncChunkContainers(ImmutableList<MarkdownChunk> newChunks, List<CodeHighlightTarget> targetAccumulator)
    {
        int existingCount = _chunkContainers.Count;
        int newCount = newChunks.Count;

        if (existingCount == 0)
        {
            for (int i = 0; i < newCount; i++)
                _chunkContainers.Add(new PreviewChunkContainer(newChunks[i]));
            return true;
        }

        Dictionary<int, double> measuredHeightsByHash = CollectMeasuredHeights();

        int commonCount = Math.Min(existingCount, newCount);
        for (int i = 0; i < commonCount; i++)
            UpdateExistingContainer(i, newChunks[i], measuredHeightsByHash, targetAccumulator);

        if (newCount > existingCount)
        {
            AppendNewContainers(existingCount, newCount, newChunks, measuredHeightsByHash);
            return true;
        }

        if (newCount < existingCount)
        {
            _chunkContainers.RemoveRange(newCount, existingCount - newCount);
            return true;
        }

        return false;
    }

    private Dictionary<int, double> CollectMeasuredHeights()
    {
        Dictionary<int, double> measured = new();
        for (int i = 0; i < _chunkContainers.Count; i++)
        {
            PreviewChunkContainer container = _chunkContainers[i];
            double? height = container.IsRealized && container.Bounds.Height > 0
                ? container.Bounds.Height
                : container.Chunk.ActualMeasuredHeight;

            if (height.HasValue && container.Chunk.ContentHash != 0)
                measured.TryAdd(container.Chunk.ContentHash, height.Value);
        }

        return measured;
    }

    private void UpdateExistingContainer(
        int index,
        MarkdownChunk newChunk,
        Dictionary<int, double> measuredHeightsByHash,
        List<CodeHighlightTarget> targetAccumulator)
    {
        PreviewChunkContainer container = _chunkContainers[index];
        MarkdownChunk oldChunk = container.Chunk;

        bool sameHash = oldChunk.ContentHash != 0 && oldChunk.ContentHash == newChunk.ContentHash;

        if (sameHash && oldChunk.ActualMeasuredHeight.HasValue)
            newChunk.ActualMeasuredHeight = oldChunk.ActualMeasuredHeight;
        else if (newChunk.ContentHash != 0 && measuredHeightsByHash.TryGetValue(newChunk.ContentHash, out double cachedHeight))
            newChunk.ActualMeasuredHeight = cachedHeight;

        container.UpdateChunk(newChunk);

        bool headingsChanged = MarkdownHeadingNumberer.HeadingNumbersChanged(oldChunk.Blocks, newChunk.Blocks);

        if ((!sameHash || headingsChanged) && container.IsRealized)
        {
            PreviewBuildResult result = _previewBuilder.BuildChunkBlocks(newChunk, _onTaskToggled, _activeBaseDirectory);
            container.RefreshRealized(result.Blocks);

            if (result.CodeTargets.Count > 0)
                targetAccumulator.AddRange(result.CodeTargets);
        }
    }

    private void AppendNewContainers(
        int start,
        int end,
        ImmutableList<MarkdownChunk> newChunks,
        Dictionary<int, double> measuredHeightsByHash)
    {
        for (int i = start; i < end; i++)
        {
            MarkdownChunk chunk = newChunks[i];
            if (chunk.ContentHash != 0 && measuredHeightsByHash.TryGetValue(chunk.ContentHash, out double cachedHeight))
                chunk.ActualMeasuredHeight = cachedHeight;

            _chunkContainers.Add(new PreviewChunkContainer(chunk));
        }
    }

    private void RealizeChunksInRange(double viewportY, double viewportHeight, List<CodeHighlightTarget> targetAccumulator)
    {
        double effectiveHeight = Math.Max(viewportHeight, 600);
        double rangeStart = Math.Max(0, viewportY - (effectiveHeight * 1.5));
        double rangeEnd = viewportY + (effectiveHeight * 2.5);

        double currentY = 0;
        bool anyRealized = false;

        for (int i = 0; i < _chunkContainers.Count; i++)
        {
            PreviewChunkContainer container = _chunkContainers[i];
            double chunkHeight = container.Chunk.EffectiveHeight;
            double chunkTop = currentY;
            double chunkBottom = currentY + chunkHeight;
            currentY += chunkHeight;

            bool isVisible = chunkBottom >= rangeStart && chunkTop <= rangeEnd;

            if (isVisible)
            {
                TryRealizeChunk(container, targetAccumulator);
                anyRealized = true;
            }
            else if (container.IsRealized)
            {
                container.Dematerialize();
            }
        }

        if (!anyRealized && _chunkContainers.Count > 0)
        {
            int fallbackCount = Math.Min(3, _chunkContainers.Count);
            for (int i = 0; i < fallbackCount; i++)
                TryRealizeChunk(_chunkContainers[i], targetAccumulator);
        }
    }

    private void TryRealizeChunk(PreviewChunkContainer container, List<CodeHighlightTarget> targetAccumulator)
    {
        if (container.IsRealized)
            return;

        PreviewBuildResult result = _previewBuilder.BuildChunkBlocks(container.Chunk, _onTaskToggled, _activeBaseDirectory);
        container.Realize(result.Blocks);

        if (result.CodeTargets.Count > 0)
            targetAccumulator.AddRange(result.CodeTargets);
    }

    private async Task HighlightCodeTargetsAsync(IReadOnlyList<CodeHighlightTarget> targets, CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
            return;

        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
            async (target, ct) =>
            {
                IReadOnlyList<HighlightedLine> lines = await _syntax.HighlightCodeAsync(target.Language, target.Code, ct);

                await RunOnUiThreadAsync(() =>
                {
                    if (!ct.IsCancellationRequested)
                        _previewBuilder.ApplyHighlight(target, lines);
                }, DispatcherPriority.Background);
            });

        await RunOnUiThreadAsync(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
                _updateState(state => state with { IsPreviewIdle = true });
        }, DispatcherPriority.Normal);
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        return RunOnUiThreadAsync(action, DispatcherPriority.Normal);
    }

    private static async Task RunOnUiThreadAsync(Action action, DispatcherPriority priority)
    {
        if (Dispatcher.UIThread.CheckAccess() && priority == DispatcherPriority.Normal)
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action, priority);
    }

    private static async Task RunOnUiThreadAsync(Func<Task> action, DispatcherPriority priority)
    {
        if (Dispatcher.UIThread.CheckAccess() && priority == DispatcherPriority.Normal)
        {
            await action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action, priority);
    }

    private bool IsStale(CancellationToken cancellationToken, int version)
    {
        return cancellationToken.IsCancellationRequested || version != _previewVersion;
    }
}
