using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Media;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class PreviewSearchServiceTests
{
    private readonly PreviewSearchService _service = new();

    [Fact]
    public async Task Search_WhenQueryIsEmpty_ReturnsEmptyResult()
    {
        await EvaluateAsync(() =>
        {
            SelectableTextBlock block = new() { Text = "Sample markdown text" };

            PreviewSearchResult result = _service.Search([block], string.Empty, false);

            Assert.Equal(0, result.TotalMatches);
            Assert.Equal(0, result.CurrentMatchIndex);
            Assert.Null(result.ActiveTextBlock);
        });
    }

    [Fact]
    public async Task Search_WhenQueryMatchesInlines_HighlightsRunsAndReturnsTotalCount()
    {
        await EvaluateAsync(() =>
        {
            SelectableTextBlock block = new();
            block.Inlines!.Add(new Run("Hello markdown world, welcome to markdown preview."));

            PreviewSearchResult result = _service.Search([block], "markdown", false);

            Assert.Equal(2, result.TotalMatches);
            Assert.Equal(1, result.CurrentMatchIndex);
            Assert.Same(block, result.ActiveTextBlock);

            List<Run> highlightedRuns = block.Inlines.OfType<Run>().Where(run => run.Background is not null).ToList();
            Assert.Equal(2, highlightedRuns.Count);
        });
    }

    [Fact]
    public async Task Search_WhenQueryMatchesCaseInsensitive_FindsMatchesRegardlessOfCasing()
    {
        await EvaluateAsync(() =>
        {
            SelectableTextBlock block = new();
            block.Inlines!.Add(new Run("Markdown and MARKDOWN and markdown."));

            PreviewSearchResult result = _service.Search([block], "markdown", false);

            Assert.Equal(3, result.TotalMatches);
        });
    }

    [Fact]
    public async Task Search_WhenMatchCaseIsTrue_FindsOnlyExactCaseMatches()
    {
        await EvaluateAsync(() =>
        {
            SelectableTextBlock block = new();
            block.Inlines!.Add(new Run("Markdown and MARKDOWN and markdown."));

            PreviewSearchResult result = _service.Search([block], "Markdown", true);

            Assert.Equal(1, result.TotalMatches);
        });
    }

    [Fact]
    public async Task NavigateNext_WhenCalled_CyclesThroughMatchesAndWrapsAround()
    {
        await EvaluateAsync(() =>
        {
            SelectableTextBlock block = new();
            block.Inlines!.Add(new Run("item one, item two, item three"));

            PreviewSearchResult initial = _service.Search([block], "item", false);
            Assert.Equal(3, initial.TotalMatches);
            Assert.Equal(1, initial.CurrentMatchIndex);

            PreviewSearchResult second = _service.NavigateNext();
            Assert.Equal(2, second.CurrentMatchIndex);

            PreviewSearchResult third = _service.NavigateNext();
            Assert.Equal(3, third.CurrentMatchIndex);

            PreviewSearchResult wrapped = _service.NavigateNext();
            Assert.Equal(1, wrapped.CurrentMatchIndex);
        });
    }

    [Fact]
    public async Task NavigatePrevious_WhenCalled_CyclesBackwardsAndWrapsAround()
    {
        await EvaluateAsync(() =>
        {
            SelectableTextBlock block = new();
            block.Inlines!.Add(new Run("item one, item two, item three"));

            _service.Search([block], "item", false);

            PreviewSearchResult wrapped = _service.NavigatePrevious();
            Assert.Equal(3, wrapped.CurrentMatchIndex);

            PreviewSearchResult second = _service.NavigatePrevious();
            Assert.Equal(2, second.CurrentMatchIndex);

            PreviewSearchResult first = _service.NavigatePrevious();
            Assert.Equal(1, first.CurrentMatchIndex);
        });
    }

    [Fact]
    public async Task Clear_WhenHighlightsApplied_RestoresOriginalInlinesAndText()
    {
        await EvaluateAsync(() =>
        {
            SelectableTextBlock blockWithInlines = new();
            Span boldSpan = new() { FontWeight = FontWeight.Bold };
            boldSpan.Inlines.Add(new Run("bold text"));
            blockWithInlines.Inlines!.Add(new Run("prefix "));
            blockWithInlines.Inlines.Add(boldSpan);

            SelectableTextBlock blockWithText = new() { Text = "plain code snippet" };

            _service.Search([blockWithInlines, blockWithText], "text", false);

            _service.Clear();

            string inlinesCombined = string.Concat(blockWithInlines.Inlines.Select(GetInlineText));
            Assert.Equal("prefix bold text", inlinesCombined);
            Assert.Equal("plain code snippet", blockWithText.Text);
        });
    }

    private static string GetInlineText(Inline inline)
    {
        return inline switch
        {
            Run run => run.Text ?? string.Empty,
            Span span => string.Concat(span.Inlines.Select(GetInlineText)),
            _ => string.Empty
        };
    }

    private static Task EvaluateAsync(Action evaluate)
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        return session.Dispatch(evaluate, CancellationToken.None);
    }
}
