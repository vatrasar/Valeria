using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class MarkdownChunkSplitterTests
{
    [Fact]
    public void Split_DocumentWithFewBlocks_ReturnsSingleChunk()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        string md = "# Heading\n\nParagraph 1\n\nParagraph 2";
        MarkdownContent content = MarkdownParser.Parse(md);

        ImmutableList<MarkdownChunk> chunks = MarkdownChunkSplitter.Split(content, metrics);

        Assert.Single(chunks);
        Assert.Equal(3, chunks[0].Blocks.Count);
        Assert.True(chunks[0].EstimatedHeight > 0);
    }

    [Fact]
    public void Split_DocumentWithManyBlocksExceedingThreshold_ReturnsMultipleChunks()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        string md = string.Join("\n\n", Enumerable.Range(1, 400).Select(i => $"Paragraph {i}"));
        MarkdownContent content = MarkdownParser.Parse(md);

        ImmutableList<MarkdownChunk> chunks = MarkdownChunkSplitter.Split(content, metrics);

        Assert.True(chunks.Count > 1);
        int totalBlocks = chunks.Sum(c => c.Blocks.Count);
        Assert.Equal(400, totalBlocks);
        Assert.All(chunks, chunk => Assert.True(chunk.Blocks.Count <= MarkdownChunkSplitter.DefaultChunkSize));
    }

    [Fact]
    public void Split_GiantListExceedingChunkSize_SlicesListIntoMultipleChunksOfMax40Items()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        string md = string.Join("\n", Enumerable.Range(1, 250).Select(i => $"- Item {i}"));
        MarkdownContent content = MarkdownParser.Parse(md);

        ImmutableList<MarkdownChunk> chunks = MarkdownChunkSplitter.Split(content, metrics);

        Assert.True(chunks.Count >= 6);
        foreach (MarkdownChunk chunk in chunks)
        {
            Assert.Single(chunk.Blocks);
            BulletListBlock list = Assert.IsType<BulletListBlock>(chunk.Blocks[0]);
            Assert.True(list.Items.Count <= MarkdownChunkSplitter.DefaultChunkSize);
        }

        int totalItems = chunks.Sum(c => ((BulletListBlock)c.Blocks[0]).Items.Count);
        Assert.Equal(250, totalItems);
    }

    [Fact]
    public void Split_GiantDocument_ProducesAccurateCumulativeEstimatedHeights()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        string md = string.Join("\n", Enumerable.Range(1, 2000).Select(i => $"{i}. Item {i}"));
        MarkdownContent content = MarkdownParser.Parse(md);

        ImmutableList<MarkdownChunk> chunks = MarkdownChunkSplitter.Split(content, metrics);

        Assert.True(chunks.Count > 40);
        double totalHeight = chunks.Sum(c => c.EstimatedHeight);
        Assert.True(totalHeight > 10000);
    }
}
