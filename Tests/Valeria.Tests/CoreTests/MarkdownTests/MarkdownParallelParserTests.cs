using System.Collections.Immutable;
using System.Linq;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class MarkdownParallelParserTests
{
    [Fact]
    public void ParseDocument_SmallDocument_ReturnsSingleChunkMatchingSequentialParse()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        string md = "# Heading 1\n\nA simple paragraph with **bold** text.\n\n- Item 1\n- Item 2";

        ImmutableList<MarkdownChunk> chunks = MarkdownParallelParser.ParseDocument(md, metrics);

        Assert.Single(chunks);
        Assert.Equal(3, chunks[0].Blocks.Count);
        Assert.IsType<HeadingBlock>(chunks[0].Blocks[0]);
        Assert.IsType<ParagraphBlock>(chunks[0].Blocks[1]);
        Assert.IsType<BulletListBlock>(chunks[0].Blocks[2]);
    }

    [Fact]
    public void ParseDocument_LargeDocument_ProducesValidContinuousChunks()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        string md = string.Join("\n", Enumerable.Range(1, 240).Select(i => $"- Item {i}"));

        ImmutableList<MarkdownChunk> chunks = MarkdownParallelParser.ParseDocument(md, metrics);

        Assert.True(chunks.Count >= 6);
        int expectedStartLine = 1;

        for (int i = 0; i < chunks.Count; i++)
        {
            MarkdownChunk chunk = chunks[i];
            Assert.Equal(expectedStartLine, chunk.StartLine);
            Assert.True(chunk.EndLine >= chunk.StartLine);
            Assert.True(chunk.Span.EndOffset >= chunk.Span.StartOffset);
            Assert.True(chunk.EstimatedHeight > 0);
            expectedStartLine = chunk.EndLine + 1;
        }

        Assert.Equal(241, expectedStartLine);
    }

    [Fact]
    public void ParseDocument_OrderedListAcrossChunks_MaintainsSequentialNumbering()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        string md = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"{i}. Item {i}"));

        ImmutableList<MarkdownChunk> chunks = MarkdownParallelParser.ParseDocument(md, metrics);

        Assert.True(chunks.Count >= 5);
        OrderedListBlock firstList = Assert.IsType<OrderedListBlock>(chunks[0].Blocks[0]);
        OrderedListBlock secondList = Assert.IsType<OrderedListBlock>(chunks[1].Blocks[0]);

        Assert.Equal(1, firstList.StartNumber);
        Assert.Equal(41, secondList.StartNumber);
    }

    [Fact]
    public void ParseSlice_SingleSlice_AdjustsBlockSourceSpansAccurately()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();
        TextChunkSlice slice = new(
            ChunkIndex: 2,
            StartLine: 101,
            EndLine: 140,
            StartOffset: 5000,
            EndOffset: 7000,
            Text: "- Item 101\n- Item 102",
            OrderedListStart: null);

        MarkdownChunk chunk = MarkdownParallelParser.ParseSlice(slice, metrics);

        Assert.Equal(2, chunk.Index);
        Assert.Equal(101, chunk.StartLine);
        Assert.Equal(140, chunk.EndLine);
        Assert.True(chunk.Span.StartOffset >= 5000);
        Assert.Equal(101, chunk.Blocks[0].SourceSpan.StartLine);
    }
}
