using System.Text;
using Valeria.Src.Core.Markdown;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class MarkdownStatsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void CountWords_BlankInput_ReturnsZero(string? text)
    {
        Assert.Equal(0, MarkdownStats.CountWords(text));
    }

    [Fact]
    public void CountWords_SampleText_ReturnsWordCount()
    {
        Assert.Equal(6, MarkdownStats.CountWords("# Title\n\nsome **bold** words here"));
    }

    [Fact]
    public void CountWords_VariousWhitespaceCombinations_ReturnsAccurateCount()
    {
        string text = "   word1 \t\n word2\r\n\tword3    word4\n";
        Assert.Equal(4, MarkdownStats.CountWords(text));
    }

    [Fact]
    public void CountWords_LargeDocument_CountsAccurately()
    {
        StringBuilder sb = new();
        for (int i = 0; i < 5000; i++)
            sb.AppendLine("one two three four five");

        Assert.Equal(25000, MarkdownStats.CountWords(sb.ToString()));
    }
}
