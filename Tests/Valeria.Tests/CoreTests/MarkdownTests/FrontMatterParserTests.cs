using System.Linq;
using Valeria.Src.Core.Markdown;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class FrontMatterParserTests
{
    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmptyMetadata()
    {
        FrontMatterMetadata result = FrontMatterParser.Parse(null);

        Assert.True(result.IsEmpty);
        Assert.False(result.HasSemanticHeader);
    }

    [Fact]
    public void Parse_AiSkillFrontMatter_ExtractsNameDescriptionAndFlags()
    {
        string yaml = """
            name: poteto-help
            description: Guides users through pstack setup, /
              poteto-mode, and picking the skill, playbook, or
              principle for a task. Type /poteto-help with a
              question.
            disable-model-invocation: true
            """;

        FrontMatterMetadata result = FrontMatterParser.Parse(yaml);

        Assert.False(result.IsEmpty);
        Assert.True(result.HasSemanticHeader);
        Assert.Equal("poteto-help", result.Name);
        Assert.Contains("Guides users through pstack setup", result.Description);
        Assert.Contains("Type /poteto-help with a question.", result.Description);

        Assert.Single(result.Flags);
        Assert.Equal("disable-model-invocation", result.Flags[0].Key);
        Assert.True(result.Flags[0].Value);
    }

    [Fact]
    public void Parse_Lists_ExtractsInlineAndIndentedLists()
    {
        string yaml = """
            name: reviewer
            description: Reviews code
            tags: [csharp, avalonia]
            allowed-tools:
              - read_file
              - run_command
            """;

        FrontMatterMetadata result = FrontMatterParser.Parse(yaml);

        Assert.Equal(2, result.Lists.Count);

        var tags = result.Lists.First(l => l.Key == "tags");
        Assert.Equal(["csharp", "avalonia"], tags.Value);

        var tools = result.Lists.First(l => l.Key == "allowed-tools");
        Assert.Equal(["read_file", "run_command"], tools.Value);
    }

    [Fact]
    public void Parse_FallbackProperties_WhenNoSemanticKeys()
    {
        string yaml = """
            author: Valeria
            version: 2.0.0
            published: true
            """;

        FrontMatterMetadata result = FrontMatterParser.Parse(yaml);

        Assert.False(result.HasSemanticHeader);
        Assert.Null(result.Name);
        Assert.Null(result.Description);

        Assert.Single(result.Flags);
        Assert.Equal("published", result.Flags[0].Key);
        Assert.True(result.Flags[0].Value);

        Assert.Equal(2, result.CustomProperties.Count);
        Assert.Equal("Valeria", result.CustomProperties.First(p => p.Key == "author").Value);
        Assert.Equal("2.0.0", result.CustomProperties.First(p => p.Key == "version").Value);
    }

    [Fact]
    public void Parse_QuotedValues_StripsQuotes()
    {
        string yaml = """
            name: "quoted-name"
            description: 'quoted description'
            """;

        FrontMatterMetadata result = FrontMatterParser.Parse(yaml);

        Assert.Equal("quoted-name", result.Name);
        Assert.Equal("quoted description", result.Description);
    }
}
