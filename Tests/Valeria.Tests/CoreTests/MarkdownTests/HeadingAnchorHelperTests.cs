using Valeria.Src.Core.Markdown;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class HeadingAnchorHelperTests
{
    [Theory]
    [InlineData("#section-1", "section-1")]
    [InlineData("##section-2", "#section-2")]
    [InlineData("   #my-heading   ", "my-heading")]
    [InlineData("#rozdzia%C5%82-1", "rozdział-1")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeAnchor_NormalizesCorrectly(string? input, string expected)
    {
        string result = HeadingAnchorHelper.NormalizeAnchor(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Rozdział 1: Executive Summary & Diagnostic Dossier", "rozdział-1-executive-summary-diagnostic-dossier")]
    [InlineData("1.1 Motto Otwierające i Metryka Diagnostyczna", "11-motto-otwierające-i-metryka-diagnostyczna")]
    [InlineData("Simple Title", "simple-title")]
    [InlineData("   Spaces   around   ", "spaces-around")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void GenerateSlug_ProducesExpectedSlug(string? text, string expected)
    {
        string result = HeadingAnchorHelper.GenerateSlug(text);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsMatch_MatchesExactAnchorId()
    {
        HeadingBlock heading = new(1, [new TextRun("Title")])
        {
            AnchorId = "rozdział-1-executive-summary--diagnostic-dossier"
        };

        bool matches = HeadingAnchorHelper.IsMatch(heading, "rozdział-1-executive-summary--diagnostic-dossier");
        Assert.True(matches);
    }

    [Fact]
    public void IsMatch_MatchesCaseInsensitively()
    {
        HeadingBlock heading = new(1, [new TextRun("Title")])
        {
            AnchorId = "my-section"
        };

        bool matches = HeadingAnchorHelper.IsMatch(heading, "MY-SECTION");
        Assert.True(matches);
    }

    [Fact]
    public void IsMatch_MatchesComputedSlugWhenAnchorIdEmpty()
    {
        HeadingBlock heading = new(1, [new TextRun("Executive Summary & Notes")]);

        bool matches = HeadingAnchorHelper.IsMatch(heading, "executive-summary-notes");
        Assert.True(matches);
    }
}
