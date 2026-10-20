using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class AsjaLargeFileBenchmarkTests
{
    private const string AsjaFilePath = "/home/vatrasar/Dokumenty/rozmowaZAsja/rozmowy/asja.md";

    [Fact]
    public void ParseAndSplit_AsjaLargeFile_ExecutesRapidlyWithLowMemory()
    {
        if (!File.Exists(AsjaFilePath))
            return;

        string content = File.ReadAllText(AsjaFilePath);
        long memBefore = GC.GetTotalMemory(true);
        Stopwatch sw = Stopwatch.StartNew();
        MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UsePreciseSourceLocation().Build();
        Markdig.Syntax.MarkdownDocument document = Markdig.Markdown.Parse(content, pipeline);
        long markdigMs = sw.ElapsedMilliseconds;

        sw.Restart();
        MarkdownContent parsed = MarkdownParser.Parse(content);
        long totalMs = sw.ElapsedMilliseconds;
        long conversionMs = totalMs - markdigMs;

        sw.Restart();
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        ImmutableList<MarkdownChunk> chunks = MarkdownChunkSplitter.Split(parsed, metrics);
        long splitMs = sw.ElapsedMilliseconds;

        long memAfter = GC.GetTotalMemory(false);
        long memDeltaMb = (memAfter - memBefore) / (1024 * 1024);

        // Verification:
        Assert.True(chunks.Count > 1000, $"Expected > 1000 chunks for 44k items, got {chunks.Count}");
        Assert.True(splitMs < 1000, $"Split took {splitMs}ms (expected < 1000ms)");
        Assert.True(memDeltaMb < 350, $"Allocated {memDeltaMb}MB (expected < 350MB, was 9000MB previously)");
        Assert.True(totalMs < 15000, $"Total: {totalMs}ms (Markdig: {markdigMs}ms, Conversion: {conversionMs}ms, Split: {splitMs}ms)");
    }

    [Fact]
    public async Task EditorPreviewCoordinator_ProcessChunkedPreview_CreatesVirtualizedChunks()
    {
        if (!File.Exists(AsjaFilePath))
            return;

        Avalonia.Headless.HeadlessUnitTestSession session = Avalonia.Headless.HeadlessUnitTestSession.GetOrStartForAssembly(
            System.Reflection.Assembly.GetExecutingAssembly());

        string content = File.ReadAllText(AsjaFilePath);
        EditorState state = new() { MarkdownText = content, FilePath = AsjaFilePath };
        IMarkdownPreviewBuilder builder = new MarkdownPreviewBuilder(
            new CodeSyntaxService(),
            Microsoft.Extensions.Options.Options.Create(new Valeria.Src.Core.Config.AppConfig()),
            new MarkdownImageLoader());

        EditorPreviewCoordinator coordinator = new(
            builder,
            new FakeCodeSyntaxService(),
            () => state,
            update => state = update(state),
            (_, _) => { });

        Task processTask = coordinator.ProcessPreviewAsync(CancellationToken.None);

        while (!processTask.IsCompleted)
        {
            await session.Dispatch(() => Avalonia.Threading.Dispatcher.UIThread.RunJobs(), CancellationToken.None);
            await Task.Delay(50);
        }

        await processTask;

        Assert.True(state.PreviewBlocks.Count > 1000, $"Expected > 1000 chunks, got {state.PreviewBlocks.Count}");
        Assert.All(state.PreviewBlocks, b => Assert.IsType<PreviewChunkContainer>(b));

        await session.Dispatch(() =>
        {
            coordinator.UpdateVisibleChunks(500 * 1200, 800);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }, CancellationToken.None);

        int realizedCount = System.Linq.Enumerable.Count(
            System.Linq.Enumerable.OfType<PreviewChunkContainer>(state.PreviewBlocks),
            c => c.IsRealized);

        Assert.True(realizedCount <= 10, $"Expected <= 10 realized chunks, but found {realizedCount}");
    }

    [Fact]
    public void ParallelParse_AsjaLargeFile_ExecutesInUnder600Milliseconds()
    {
        if (!File.Exists(AsjaFilePath))
            return;

        string content = File.ReadAllText(AsjaFilePath);
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();

        Stopwatch sw = Stopwatch.StartNew();
        ImmutableList<MarkdownChunk> chunks = MarkdownParallelParser.ParseDocument(content, metrics);
        long elapsedMs = sw.ElapsedMilliseconds;

        Assert.True(chunks.Count > 1000, $"Expected > 1000 chunks, got {chunks.Count}");
        Assert.True(elapsedMs < 600, $"Expected < 600ms, but took {elapsedMs}ms");
    }

    [Fact]
    public async Task EditorPreviewCoordinator_ProcessChunkedPreview_PreservesViewportRealization()
    {
        if (!File.Exists(AsjaFilePath))
            return;

        Avalonia.Headless.HeadlessUnitTestSession session = Avalonia.Headless.HeadlessUnitTestSession.GetOrStartForAssembly(
            System.Reflection.Assembly.GetExecutingAssembly());

        string content = File.ReadAllText(AsjaFilePath);
        EditorState state = new() { MarkdownText = content, FilePath = AsjaFilePath };
        IMarkdownPreviewBuilder builder = new MarkdownPreviewBuilder(
            new CodeSyntaxService(),
            Microsoft.Extensions.Options.Options.Create(new Valeria.Src.Core.Config.AppConfig()),
            new MarkdownImageLoader());

        EditorPreviewCoordinator coordinator = new(
            builder,
            new FakeCodeSyntaxService(),
            () => state,
            update => state = update(state),
            (_, _) => { });

        double midDocY = 50000;
        coordinator.UpdateVisibleChunks(midDocY, 800);

        Task processTask = coordinator.ProcessPreviewAsync(CancellationToken.None);

        while (!processTask.IsCompleted)
        {
            await session.Dispatch(() => Avalonia.Threading.Dispatcher.UIThread.RunJobs(), CancellationToken.None);
            await Task.Delay(50);
        }

        await processTask;

        var containers = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OfType<PreviewChunkContainer>(state.PreviewBlocks));
        var realizedContainers = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(containers, c => c.IsRealized));

        Assert.NotEmpty(realizedContainers);
        Assert.Contains(realizedContainers, c => c.Chunk.Span.StartLine > 1000);
    }

    [Fact]
    public async Task EditorPreviewCoordinator_EditInChunk_PreservesMeasuredHeightOfPrecedingChunks()
    {
        if (!File.Exists(AsjaFilePath))
            return;

        Avalonia.Headless.HeadlessUnitTestSession session = Avalonia.Headless.HeadlessUnitTestSession.GetOrStartForAssembly(
            System.Reflection.Assembly.GetExecutingAssembly());

        string initialContent = File.ReadAllText(AsjaFilePath);
        EditorState state = new() { MarkdownText = initialContent, FilePath = AsjaFilePath };
        IMarkdownPreviewBuilder builder = new MarkdownPreviewBuilder(
            new CodeSyntaxService(),
            Microsoft.Extensions.Options.Options.Create(new Valeria.Src.Core.Config.AppConfig()),
            new MarkdownImageLoader());

        EditorPreviewCoordinator coordinator = new(
            builder,
            new FakeCodeSyntaxService(),
            () => state,
            update => state = update(state),
            (_, _) => { });

        Task processTask = coordinator.ProcessPreviewAsync(CancellationToken.None);
        while (!processTask.IsCompleted)
        {
            await session.Dispatch(() => Avalonia.Threading.Dispatcher.UIThread.RunJobs(), CancellationToken.None);
            await Task.Delay(20);
        }
        await processTask;

        var containers = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OfType<PreviewChunkContainer>(state.PreviewBlocks));
        Assert.NotEmpty(containers);

        PreviewChunkContainer firstContainer = containers[0];
        firstContainer.Chunk.ActualMeasuredHeight = 1350.0;

        state = state with { MarkdownText = initialContent + "\n\nAdditional text edited at bottom." };

        Task secondProcessTask = coordinator.ProcessPreviewAsync(CancellationToken.None);
        while (!secondProcessTask.IsCompleted)
        {
            await session.Dispatch(() => Avalonia.Threading.Dispatcher.UIThread.RunJobs(), CancellationToken.None);
            await Task.Delay(20);
        }
        await secondProcessTask;

        var updatedContainers = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OfType<PreviewChunkContainer>(state.PreviewBlocks));
        Assert.Same(firstContainer, updatedContainers[0]);
        Assert.Equal(1350.0, updatedContainers[0].Chunk.ActualMeasuredHeight);
        Assert.Equal(1350.0, updatedContainers[0].Chunk.EffectiveHeight);
    }
}
