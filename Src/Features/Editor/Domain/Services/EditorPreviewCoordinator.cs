using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Coordinates asynchronous markdown parsing, preview building, and background syntax highlighting.
/// </summary>
public sealed class EditorPreviewCoordinator
{
    private readonly IMarkdownPreviewBuilder _previewBuilder;
    private readonly ICodeSyntaxService _syntax;
    private readonly Func<EditorState> _getState;
    private readonly Action<Func<EditorState, EditorState>> _updateState;
    private readonly Action<int, bool> _onTaskToggled;
    private int _previewVersion;

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
    }

    public async Task ProcessPreviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            int version = Interlocked.Increment(ref _previewVersion);
            string snapshot = _getState().MarkdownText;

            MarkdownContent content = await Task.Run(() => MarkdownParser.Parse(snapshot), cancellationToken).ConfigureAwait(false);

            if (IsStale(cancellationToken, version))
                return;

            string? filePath = _getState().FilePath;
            string? baseDirectory = string.IsNullOrEmpty(filePath)
                ? null
                : Path.GetDirectoryName(filePath);

            PreviewBuildResult? built = null;
            await RunOnUiThreadAsync(async () =>
            {
                if (IsStale(cancellationToken, version))
                    return;

                built = await _previewBuilder.BuildBlocksAsync(content, _onTaskToggled, baseDirectory, cancellationToken);

                if (IsStale(cancellationToken, version) || (built.Blocks.Count == 0 && content.Blocks.Count > 0))
                    return;

                _updateState(state => state with
                {
                    PreviewBlocks = ImmutableList.CreateRange(built.Blocks),
                    IsPreviewIdle = built.CodeTargets.Count == 0
                });
            }, DispatcherPriority.Background);

            if (built is null || IsStale(cancellationToken, version) || built.CodeTargets.Count == 0)
                return;

            foreach (CodeHighlightTarget target in built.CodeTargets)
            {
                IReadOnlyList<HighlightedLine> lines = await _syntax.HighlightCodeAsync(target.Language, target.Code, cancellationToken);

                if (IsStale(cancellationToken, version))
                    return;

                await RunOnUiThreadAsync(() =>
                {
                    if (IsStale(cancellationToken, version))
                        return;

                    _previewBuilder.ApplyHighlight(target, lines);
                }, DispatcherPriority.Background);
            }

            await RunOnUiThreadAsync(() =>
            {
                if (IsStale(cancellationToken, version))
                    return;

                _updateState(state => state with { IsPreviewIdle = true });
            }, DispatcherPriority.Background);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RunOnUiThreadAsync(() => _updateState(state => state with { ErrorMessage = exception.ToString() }));
        }
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
        await Dispatcher.UIThread.InvokeAsync(action, priority);
    }

    private bool IsStale(CancellationToken cancellationToken, int version)
    {
        return cancellationToken.IsCancellationRequested || version != _previewVersion;
    }
}
