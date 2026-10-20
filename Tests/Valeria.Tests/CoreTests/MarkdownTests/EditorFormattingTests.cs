using System;
using Valeria.Src.Core.Markdown;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

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

    [Fact]
    public void ToggleTask_ChangesSelectedTaskOnly()
    {
        FormattingResult? result = EditorFormatting.ToggleTask("- [ ] first\n- [x] second", 0, true);

        Assert.NotNull(result);
        Assert.Equal("- [x] first\n- [x] second", result!.Text);
    }

    [Fact]
    public void ToggleTask_IgnoresTaskLikeTextInsideCodeFence()
    {
        const string markdown = "```\n- [ ] code\n```\n\n- [ ] document";

        FormattingResult? result = EditorFormatting.ToggleTask(markdown, 0, true);

        Assert.NotNull(result);
        Assert.Equal("```\n- [ ] code\n```\n\n- [x] document", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_MiddleItemDeleted_AdjustsSubsequentNumbers()
    {
        string text = "1. First\n2. Second\n3. Third\n4. Fourth\n6. Sixth\n7. Seventh";
        int offset = text.IndexOf("6. Sixth", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1. First\n2. Second\n3. Third\n4. Fourth\n5. Sixth\n6. Seventh", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_SecondItemDeleted_AdjustsThirdToSecond()
    {
        string text = "1. Alpha\n3. Gamma";
        int offset = text.IndexOf("3. Gamma", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1. Alpha\n2. Gamma", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_FirstItemDeleted_RenumbersStartingFromOne()
    {
        string text = "2. Second\n3. Third\n4. Fourth";

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, 0);

        Assert.NotNull(result);
        Assert.Equal("1. Second\n2. Third\n3. Fourth", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_MultipleItemsDeleted_AdjustsToNextSequential()
    {
        string text = "1. One\n2. Two\n3. Three\n6. Six\n7. Seven";
        int offset = text.IndexOf("6. Six", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1. One\n2. Two\n3. Three\n4. Six\n5. Seven", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_ParenDelimiter_PreservesDelimiter()
    {
        string text = "1) A\n2) B\n4) D";
        int offset = text.IndexOf("4) D", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1) A\n2) B\n3) D", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_TaskList_PreservesTaskBrackets()
    {
        string text = "1. [ ] A\n2. [x] B\n4. [ ] D";
        int offset = text.IndexOf("4. [ ] D", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1. [ ] A\n2. [x] B\n3. [ ] D", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_NestedSublist_AdjustsSublistOnly()
    {
        string text = "1. Parent 1\n   1. Sub 1\n   3. Sub 3\n2. Parent 2";
        int offset = text.IndexOf("3. Sub 3", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1. Parent 1\n   1. Sub 1\n   2. Sub 3\n2. Parent 2", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_ParentDeleted_SublistUntouched()
    {
        string text = "1. Parent 1\n   1. Sub 1\n   2. Sub 2\n3. Parent 3";
        int offset = text.IndexOf("3. Parent 3", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1. Parent 1\n   1. Sub 1\n   2. Sub 2\n2. Parent 3", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_LooseList_RenumbersAcrossBlankLines()
    {
        string text = "1. First\n\n2. Second\n\n4. Fourth";
        int offset = text.IndexOf("4. Fourth", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("1. First\n\n2. Second\n\n3. Fourth", result!.Text);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_LastItemDeleted_ReturnsNull()
    {
        string text = "1. First\n2. Second";

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, text.Length);

        Assert.Null(result);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_SequentialList_ReturnsNull()
    {
        string text = "1. First\n2. Second\n3. Third";

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, 10);

        Assert.Null(result);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_LazyCommonMarkList_ReturnsNull()
    {
        string text = "1. First\n1. Second\n1. Third";

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, 10);

        Assert.Null(result);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_InsideFence_ReturnsNull()
    {
        string text = "```\n1. First\n3. Third\n```";
        int offset = text.IndexOf("3. Third", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.Null(result);
    }

    [Fact]
    public void RenumberOrderedListAfterDeletion_SeparateSections_AffectsOnlyTargetList()
    {
        string text = "# Section 1\n1. A\n3. C\n\n# Section 2\n1. X\n3. Z";
        int offset = text.IndexOf("3. C", StringComparison.Ordinal);

        FormattingResult? result = EditorFormatting.RenumberOrderedListAfterDeletion(text, offset);

        Assert.NotNull(result);
        Assert.Equal("# Section 1\n1. A\n2. C\n\n# Section 2\n1. X\n3. Z", result!.Text);
    }
}
