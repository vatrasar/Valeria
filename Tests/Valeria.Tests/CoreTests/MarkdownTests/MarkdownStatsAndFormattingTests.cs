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
}

public sealed class EditorFormattingTests
{
    [Fact]
    public void ToggleWrap_EmptySelection_InsertsPlaceholderAndSelectsIt()
    {
        FormattingResult result = EditorFormatting.ToggleWrap("hello", 5, 0, "**", "bold text");

        Assert.Equal("hello**bold text**", result.Text);
        Assert.Equal(7, result.CaretOffset);
        Assert.Equal("bold text".Length, result.SelectionLength);
    }

    [Fact]
    public void ToggleWrap_WrappedSelection_UnwrapsText()
    {
        FormattingResult result = EditorFormatting.ToggleWrap("a **bold** b", 2, 8, "**", "bold text");

        Assert.Equal("a bold b", result.Text);
    }

    [Fact]
    public void ToggleWrap_PlainSelection_WrapsWholeSelection()
    {
        FormattingResult result = EditorFormatting.ToggleWrap("a bold b", 2, 4, "**", "bold text");

        Assert.Equal("a **bold** b", result.Text);
        Assert.Equal(2, result.CaretOffset);
    }

    [Fact]
    public void ToggleHeading_PlainLine_AddsPrefix()
    {
        FormattingResult result = EditorFormatting.ToggleHeading("line", 0, 4, 2);

        Assert.Equal("## line", result.Text);
    }

    [Fact]
    public void ToggleHeading_SameLevel_RemovesPrefix()
    {
        FormattingResult result = EditorFormatting.ToggleHeading("## line", 0, 7, 2);

        Assert.Equal("line", result.Text);
    }

    [Fact]
    public void ToggleHeading_OtherLevel_ReplacesPrefix()
    {
        FormattingResult result = EditorFormatting.ToggleHeading("# line", 0, 6, 2);

        Assert.Equal("## line", result.Text);
    }

    [Fact]
    public void ToggleOrderedList_PlainLines_NumbersSequentially()
    {
        FormattingResult result = EditorFormatting.ToggleOrderedList("a\nb", 0, 3);

        Assert.Equal("1. a\n2. b", result.Text);
    }

    [Fact]
    public void InsertCodeFence_SelectedLines_WrapsInFence()
    {
        FormattingResult result = EditorFormatting.InsertCodeFence("var x = 1;", 0, 10, "csharp");

        Assert.Equal("```csharp\nvar x = 1;\n```", result.Text);
    }

    [Fact]
    public void InsertTable_MidDocument_InsertsTemplateAfterCaret()
    {
        FormattingResult result = EditorFormatting.InsertTable("# Title\n", 8);

        Assert.Contains("| Header 1 | Header 2 |", result.Text);
        Assert.StartsWith("# Title\n", result.Text);
    }

    [Fact]
    public void ContinueListItem_BulletLine_InsertsNewEmptyItem()
    {
        FormattingResult? result = EditorFormatting.ContinueListItem("- item", 6);

        Assert.NotNull(result);
        Assert.Equal("- item\n- ", result!.Text);
        Assert.Equal(9, result.CaretOffset);
    }

    [Fact]
    public void ContinueListItem_OrderedLine_IncrementsNumber()
    {
        FormattingResult? result = EditorFormatting.ContinueListItem("1. item", 7);

        Assert.NotNull(result);
        Assert.Equal("1. item\n2. ", result!.Text);
    }

    [Fact]
    public void ContinueListItem_MidLine_InsertsAfterLine()
    {
        FormattingResult? result = EditorFormatting.ContinueListItem("- hello world", 3);

        Assert.NotNull(result);
        Assert.Equal("- hello world\n- ", result!.Text);
    }

    [Fact]
    public void ContinueListItem_OrderedParenMarker_PreservesMarker()
    {
        FormattingResult? result = EditorFormatting.ContinueListItem("1) item", 7);

        Assert.NotNull(result);
        Assert.Equal("1) item\n2) ", result!.Text);
    }

    [Fact]
    public void ContinueListItem_BulletCharacter_PreservesBullet()
    {
        FormattingResult? result = EditorFormatting.ContinueListItem("* star\n* star", 11);

        Assert.NotNull(result);
        Assert.Equal("* star\n* star\n* ", result!.Text);
    }

    [Fact]
    public void ContinueListItem_IndentedItem_KeepsIndentation()
    {
        FormattingResult? result = EditorFormatting.ContinueListItem("  - nested item", 15);

        Assert.NotNull(result);
        Assert.Equal("  - nested item\n  - ", result!.Text);
    }

    [Fact]
    public void ContinueListItem_TabIndentedItem_KeepsTabIndentation()
    {
        FormattingResult? result = EditorFormatting.ContinueListItem("\t- nested item", 14);

        Assert.NotNull(result);
        Assert.Equal("\t- nested item\n\t- ", result!.Text);
    }

    [Fact]
    public void ContinueListItem_PlainLine_ReturnsNull()
    {
        Assert.Null(EditorFormatting.ContinueListItem("plain text", 5));
    }
}
