using System.Linq;
using NewMarkText.Src.Core.Markdown;
using Xunit;

namespace NewMarkText.Tests.CoreTests.MarkdownTests;

public sealed class MarkdownParserTests
{
    [Fact]
    public void Parse_NullInput_ReturnsEmptyContent()
    {
        MarkdownContent content = MarkdownParser.Parse(null);

        Assert.Empty(content.Blocks);
    }

    [Fact]
    public void Parse_Headings_ReturnsLevelsAndText()
    {
        MarkdownContent content = MarkdownParser.Parse("# Title\n\n### Sub");

        Assert.Equal(2, content.Blocks.Count);
        HeadingBlock first = Assert.IsType<HeadingBlock>(content.Blocks[0]);
        HeadingBlock second = Assert.IsType<HeadingBlock>(content.Blocks[1]);

        Assert.Equal(1, first.Level);
        Assert.Equal("Title", FlattenText(first.Inlines));
        Assert.Equal(3, second.Level);
        Assert.Equal("Sub", FlattenText(second.Inlines));
    }

    [Fact]
    public void Parse_Emphasis_ReturnsBoldItalicStrikeSpans()
    {
        MarkdownContent content = MarkdownParser.Parse("**bold** *italic* ~~strike~~ `code`");

        ParagraphBlock paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(content.Blocks));

        Assert.Contains(paragraph.Inlines, inline => inline is BoldSpan);
        Assert.Contains(paragraph.Inlines, inline => inline is ItalicSpan);
        Assert.Contains(paragraph.Inlines, inline => inline is StrikethroughSpan);
        Assert.Contains(paragraph.Inlines, inline => inline is CodeSpan code && code.Code == "code");
    }

    [Fact]
    public void Parse_FencedCode_ReturnsLanguageAndCode()
    {
        MarkdownContent content = MarkdownParser.Parse("```csharp\nvar x = 1;\n```");

        CodeBlock code = Assert.IsType<CodeBlock>(Assert.Single(content.Blocks));

        Assert.Equal("csharp", code.Language);
        Assert.Contains("var x = 1;", code.Code);
    }

    [Fact]
    public void Parse_FencedCodeWithoutLanguage_ReturnsNullLanguage()
    {
        MarkdownContent content = MarkdownParser.Parse("```\nplain\n```");

        CodeBlock code = Assert.IsType<CodeBlock>(Assert.Single(content.Blocks));

        Assert.Null(code.Language);
    }

    [Fact]
    public void Parse_TaskList_ReturnsCheckedStates()
    {
        MarkdownContent content = MarkdownParser.Parse("- [x] done\n- [ ] open");

        BulletListBlock list = Assert.IsType<BulletListBlock>(Assert.Single(content.Blocks));

        Assert.Equal(2, list.Items.Count);
        Assert.Equal(true, list.Items[0].IsChecked);
        Assert.Equal(false, list.Items[1].IsChecked);
        Assert.Equal("done", FlattenBlocks(list.Items[0].Blocks));
    }

    [Fact]
    public void Parse_OrderedListWithCustomStart_ReturnsStartNumber()
    {
        MarkdownContent content = MarkdownParser.Parse("3. first\n4. second");

        OrderedListBlock list = Assert.IsType<OrderedListBlock>(Assert.Single(content.Blocks));

        Assert.Equal(3, list.StartNumber);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parse_Table_ReturnsHeaderRowsAndAlignments()
    {
        MarkdownContent content = MarkdownParser.Parse("| A | B |\n| :--- | ---: |\n| 1 | 2 |");

        TableBlock table = Assert.IsType<TableBlock>(Assert.Single(content.Blocks));

        Assert.NotNull(table.Header);
        Assert.Equal("A", FlattenText(table.Header.Cells[0].Inlines));
        Assert.Single(table.Rows);
        Assert.Equal("2", FlattenText(table.Rows[0].Cells[1].Inlines));
        Assert.Equal(TableColumnAlignment.Left, table.Alignments[0]);
        Assert.Equal(TableColumnAlignment.Right, table.Alignments[1]);
    }

    [Fact]
    public void Parse_QuoteWithNesting_ReturnsNestedBlocks()
    {
        MarkdownContent content = MarkdownParser.Parse("> quote\n>\n> - item");

        QuoteBlock quote = Assert.IsType<QuoteBlock>(Assert.Single(content.Blocks));

        Assert.Contains(quote.Blocks, block => block is ParagraphBlock);
        Assert.Contains(quote.Blocks, block => block is BulletListBlock);
    }

    [Fact]
    public void Parse_LinkAndImage_ReturnsSpansWithTargets()
    {
        MarkdownContent content = MarkdownParser.Parse("[label](https://example.com) ![alt](pic.png)");

        ParagraphBlock paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(content.Blocks));

        LinkSpan link = Assert.IsType<LinkSpan>(paragraph.Inlines.OfType<LinkSpan>().Single());
        ImageSpan image = Assert.IsType<ImageSpan>(paragraph.Inlines.OfType<ImageSpan>().Single());

        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("label", FlattenText(link.Children));
        Assert.Equal("alt", image.AlternativeText);
        Assert.Equal("pic.png", image.Url);
    }

    [Fact]
    public void Parse_HorizontalRule_ReturnsThematicBreak()
    {
        MarkdownContent content = MarkdownParser.Parse("text\n\n---\n\nmore");

        Assert.Equal(3, content.Blocks.Count);
        Assert.IsType<ThematicBreakBlock>(content.Blocks[1]);
    }

    private static string FlattenText(System.Collections.Immutable.ImmutableList<MarkdownInline> inlines)
    {
        return string.Concat(inlines.Select(FlattenInline));
    }

    private static string FlattenInline(MarkdownInline inline)
    {
        return inline switch
        {
            TextRun text => text.Text,
            BoldSpan bold => FlattenText(bold.Children),
            ItalicSpan italic => FlattenText(italic.Children),
            StrikethroughSpan strike => FlattenText(strike.Children),
            CodeSpan code => code.Code,
            LinkSpan link => FlattenText(link.Children),
            _ => string.Empty
        };
    }

    private static string FlattenBlocks(System.Collections.Immutable.ImmutableList<MarkdownBlock> blocks)
    {
        return string.Concat(blocks.Select(block => block switch
        {
            ParagraphBlock paragraph => FlattenText(paragraph.Inlines),
            _ => string.Empty
        }));
    }
}
