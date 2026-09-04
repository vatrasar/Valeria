using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class CodeSyntaxServiceTests
{
    private readonly CodeSyntaxService _syntax = new();

    [Fact]
    public async Task HighlightCodeAsync_CSharpSnippet_ReturnsColoredSpans()
    {
        IReadOnlyList<HighlightedLine> lines = await _syntax.HighlightCodeAsync("csharp", "public int Add(int a, int b) { return a + b; }", CancellationToken.None);

        bool hasColoredSpan = lines.Any(line => line.Spans.Any(span => span.ForegroundHex is not null));

        Assert.True(lines.Count > 0, "Expected at least one highlighted line.");
        Assert.True(hasColoredSpan, "Expected at least one span with a resolved foreground color.");
    }

    [Fact]
    public async Task HighlightCodeAsync_UnknownLanguage_ReturnsPlainLines()
    {
        IReadOnlyList<HighlightedLine> lines = await _syntax.HighlightCodeAsync("nosuchlang", "line one\nline two", CancellationToken.None);

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.All(line.Spans, span => Assert.Null(span.ForegroundHex)));
        Assert.Equal("line one", lines[0].Spans[0].Text);
        Assert.Equal("line two", lines[1].Spans[0].Text);
    }

    [Fact]
    public async Task HighlightCodeAsync_Multiline_KeepsLineCount()
    {
        IReadOnlyList<HighlightedLine> lines = await _syntax.HighlightCodeAsync("python", "def add(a, b):\n    return a + b", CancellationToken.None);

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public async Task ResolveScope_CommonLanguages_ReturnsScope()
    {
        Assert.Equal("source.cs", _syntax.ResolveScope("csharp"));
        Assert.Equal("source.python", _syntax.ResolveScope("python"));
        Assert.Null(_syntax.ResolveScope("unknown"));
    }

    [Fact]
    public void GetLanguageSuggestions_ReturnsUniqueSupportedIdentifiers()
    {
        IReadOnlyList<CodeLanguageSuggestion> suggestions = _syntax.GetLanguageSuggestions();

        Assert.Contains(suggestions, suggestion => suggestion.Identifier == "csharp" && suggestion.DisplayName == "C#");
        Assert.Contains(suggestions, suggestion => suggestion.Identifier == "python" && suggestion.DisplayName == "Python");
        Assert.Equal(suggestions.Count, suggestions.Select(suggestion => suggestion.Identifier).Distinct().Count());
        Assert.All(suggestions, suggestion => Assert.NotNull(_syntax.ResolveScope(suggestion.Identifier)));
    }
}
