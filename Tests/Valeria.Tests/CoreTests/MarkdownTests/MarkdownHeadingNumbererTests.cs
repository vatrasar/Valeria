using System.Collections.Generic;
using System.Collections.Immutable;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class MarkdownHeadingNumbererTests
{
    [Fact]
    public void ApplyNumbering_StandardHierarchy_TitleHasNoNumberAndLevelTwoStartsAtOne()
    {
        string md = """
            # Document Title
            ## Section One
            ### Subsection One A
            ### Subsection One B
            ## Section Two
            ### Subsection Two A
            """;

        MarkdownContent content = MarkdownParser.Parse(md);
        List<HeadingBlock> headings = [.. content.Blocks.OfType<HeadingBlock>()];

        Assert.Equal(6, headings.Count);
        Assert.Null(headings[0].SectionNumber);
        Assert.Equal("1.", headings[1].SectionNumber);
        Assert.Equal("1.1", headings[2].SectionNumber);
        Assert.Equal("1.2", headings[3].SectionNumber);
        Assert.Equal("2.", headings[4].SectionNumber);
        Assert.Equal("2.1", headings[5].SectionNumber);
    }

    [Fact]
    public void ApplyNumbering_StartsWithLevelTwo_AssignsOneDot()
    {
        string md = """
            ## Section Alpha
            ## Section Beta
            ### Subsection Beta One
            """;

        MarkdownContent content = MarkdownParser.Parse(md);
        List<HeadingBlock> headings = [.. content.Blocks.OfType<HeadingBlock>()];

        Assert.Equal(3, headings.Count);
        Assert.Equal("1.", headings[0].SectionNumber);
        Assert.Equal("2.", headings[1].SectionNumber);
        Assert.Equal("2.1", headings[2].SectionNumber);
    }

    [Fact]
    public void ApplyNumbering_SkippedLevels_InitializesMissingParentsToOne()
    {
        string md = """
            # Document Title
            #### Deep Nested
            """;

        MarkdownContent content = MarkdownParser.Parse(md);
        List<HeadingBlock> headings = [.. content.Blocks.OfType<HeadingBlock>()];

        Assert.Equal(2, headings.Count);
        Assert.Null(headings[0].SectionNumber);
        Assert.Equal("1.1.1", headings[1].SectionNumber);
    }

    [Fact]
    public void ApplyNumbering_MultipleH1Titles_RemainUnnumbered()
    {
        string md = """
            # First Title
            ## First Section
            # Second Title
            ## Second Section
            """;

        MarkdownContent content = MarkdownParser.Parse(md);
        List<HeadingBlock> headings = [.. content.Blocks.OfType<HeadingBlock>()];

        Assert.Equal(4, headings.Count);
        Assert.Null(headings[0].SectionNumber);
        Assert.Equal("1.", headings[1].SectionNumber);
        Assert.Null(headings[2].SectionNumber);
        Assert.Equal("2.", headings[3].SectionNumber);
    }

    [Fact]
    public void ApplyNumberingToChunks_AcrossMultipleChunks_NumbersContinuously()
    {
        ImmutableList<MarkdownBlock> chunk1Blocks =
        [
            new HeadingBlock(1, ImmutableList<MarkdownInline>.Empty),
            new HeadingBlock(2, ImmutableList<MarkdownInline>.Empty),
            new HeadingBlock(3, ImmutableList<MarkdownInline>.Empty)
        ];

        ImmutableList<MarkdownBlock> chunk2Blocks =
        [
            new HeadingBlock(3, ImmutableList<MarkdownInline>.Empty),
            new HeadingBlock(2, ImmutableList<MarkdownInline>.Empty),
            new HeadingBlock(3, ImmutableList<MarkdownInline>.Empty)
        ];

        BlockSourceSpan span = new(0, 10, 1, 2);
        List<MarkdownChunk> chunks =
        [
            new MarkdownChunk(0, 1, 2, span, 50.0, chunk1Blocks),
            new MarkdownChunk(1, 3, 5, span, 50.0, chunk2Blocks)
        ];

        ImmutableList<MarkdownChunk> numberedChunks = MarkdownHeadingNumberer.ApplyNumberingToChunks(chunks);

        HeadingBlock c1Title = Assert.IsType<HeadingBlock>(numberedChunks[0].Blocks[0]);
        HeadingBlock c1H2 = Assert.IsType<HeadingBlock>(numberedChunks[0].Blocks[1]);
        HeadingBlock c1H3 = Assert.IsType<HeadingBlock>(numberedChunks[0].Blocks[2]);
        HeadingBlock c2H3 = Assert.IsType<HeadingBlock>(numberedChunks[1].Blocks[0]);
        HeadingBlock c2H2 = Assert.IsType<HeadingBlock>(numberedChunks[1].Blocks[1]);
        HeadingBlock c2H3Next = Assert.IsType<HeadingBlock>(numberedChunks[1].Blocks[2]);

        Assert.Null(c1Title.SectionNumber);
        Assert.Equal("1.", c1H2.SectionNumber);
        Assert.Equal("1.1", c1H3.SectionNumber);
        Assert.Equal("1.2", c2H3.SectionNumber);
        Assert.Equal("2.", c2H2.SectionNumber);
        Assert.Equal("2.1", c2H3Next.SectionNumber);
    }

    [Fact]
    public void HeadingNumbersChanged_DetectsModifications()
    {
        ImmutableList<MarkdownBlock> oldBlocks =
        [
            new HeadingBlock(1, ImmutableList<MarkdownInline>.Empty) { SectionNumber = null },
            new HeadingBlock(2, ImmutableList<MarkdownInline>.Empty) { SectionNumber = "1." }
        ];

        ImmutableList<MarkdownBlock> sameBlocks =
        [
            new HeadingBlock(1, ImmutableList<MarkdownInline>.Empty) { SectionNumber = null },
            new HeadingBlock(2, ImmutableList<MarkdownInline>.Empty) { SectionNumber = "1." }
        ];

        ImmutableList<MarkdownBlock> changedBlocks =
        [
            new HeadingBlock(1, ImmutableList<MarkdownInline>.Empty) { SectionNumber = null },
            new HeadingBlock(2, ImmutableList<MarkdownInline>.Empty) { SectionNumber = "2." }
        ];

        Assert.False(MarkdownHeadingNumberer.HeadingNumbersChanged(oldBlocks, sameBlocks));
        Assert.True(MarkdownHeadingNumberer.HeadingNumbersChanged(oldBlocks, changedBlocks));
    }

    [Fact]
    public void HeadingNumberTracker_ClampsHeadingLevels()
    {
        HeadingNumberTracker tracker = new();

        string? levelZero = tracker.GetNextNumber(0);
        string? levelOne = tracker.GetNextNumber(1);
        string? levelFive = tracker.GetNextNumber(5);
        string? levelSix = tracker.GetNextNumber(6);
        string? levelSeven = tracker.GetNextNumber(7);

        Assert.Null(levelZero);
        Assert.Null(levelOne);
        Assert.Null(levelFive);
        Assert.Null(levelSix);
        Assert.Null(levelSeven);
    }

    [Fact]
    public void ApplyNumbering_AllHeadingLevels_OnlyLevelsTwoThroughFourReceiveNumbers()
    {
        string md = """
            # Document Title
            ## Section Two
            ### Subsection Three
            #### Subsubsection Four
            ##### Subsection Five
            ###### Subsection Six
            """;

        MarkdownContent content = MarkdownParser.Parse(md);
        List<HeadingBlock> headings = [.. content.Blocks.OfType<HeadingBlock>()];

        Assert.Equal(6, headings.Count);
        Assert.Null(headings[0].SectionNumber);
        Assert.Equal("1.", headings[1].SectionNumber);
        Assert.Equal("1.1", headings[2].SectionNumber);
        Assert.Equal("1.1.1", headings[3].SectionNumber);
        Assert.Null(headings[4].SectionNumber);
        Assert.Null(headings[5].SectionNumber);
    }
}
