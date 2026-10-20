using System.Collections.Immutable;
using System.Linq;
using Valeria.Src.Core.Markdown;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class MarkdownTextChunkerTests
{
    [Fact]
    public void SliceDocument_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(MarkdownTextChunker.SliceDocument(null));
        Assert.Empty(MarkdownTextChunker.SliceDocument(string.Empty));
    }

    [Fact]
    public void SliceDocument_SmallDocumentBelowThreshold_ReturnsSingleSlice()
    {
        string md = "# Heading\n\nParagraph 1\n\n- Item 1\n- Item 2";
        ImmutableList<TextChunkSlice> slices = MarkdownTextChunker.SliceDocument(md);

        Assert.Single(slices);
        Assert.Equal(1, slices[0].StartLine);
        Assert.Equal(0, slices[0].StartOffset);
        Assert.Equal(md.Length, slices[0].EndOffset);
        Assert.Equal(md, slices[0].Text);
    }

    [Fact]
    public void SliceDocument_LargeListAboveThreshold_SlicesIntoChunksOfMax40()
    {
        string md = string.Join("\n", Enumerable.Range(1, 240).Select(i => $"- Item {i}"));
        ImmutableList<TextChunkSlice> slices = MarkdownTextChunker.SliceDocument(md);

        Assert.True(slices.Count >= 6);
        int totalLines = slices.Sum(s => s.EndLine - s.StartLine + 1);
        Assert.Equal(240, totalLines);
        Assert.All(slices, slice => Assert.True(slice.EndLine - slice.StartLine + 1 <= 40));
    }

    [Fact]
    public void SliceDocument_OrderedList_TracksStartNumbersAccurately()
    {
        string md = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"{i}. Item {i}"));
        ImmutableList<TextChunkSlice> slices = MarkdownTextChunker.SliceDocument(md);

        Assert.True(slices.Count >= 5);
        Assert.Equal(1, slices[0].OrderedListStart);
        Assert.Equal(41, slices[1].OrderedListStart);
        Assert.Equal(81, slices[2].OrderedListStart);
    }

    [Fact]
    public void SliceDocument_FencedCodeBlockExceedingChunkSize_DoesNotSplitInsideCodeBlock()
    {
        string codeLines = string.Join("\n", Enumerable.Range(1, 80).Select(i => $"    int x{i} = {i};"));
        string listBefore = string.Join("\n", Enumerable.Range(1, 150).Select(i => $"- Item {i}"));
        string listAfter = string.Join("\n", Enumerable.Range(1, 150).Select(i => $"- After {i}"));
        string md = $"{listBefore}\n\n```csharp\n{codeLines}\n```\n\n{listAfter}";

        ImmutableList<TextChunkSlice> slices = MarkdownTextChunker.SliceDocument(md);

        Assert.True(slices.Count > 1);
        foreach (TextChunkSlice slice in slices)
        {
            int openCount = CountOccurrences(slice.Text, "```csharp");
            int closeCount = CountOccurrences(slice.Text, "```");
            if (openCount > 0)
                Assert.True(closeCount >= 2);
        }
    }

    private static int CountOccurrences(string source, string substring)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(substring, index, System.StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
