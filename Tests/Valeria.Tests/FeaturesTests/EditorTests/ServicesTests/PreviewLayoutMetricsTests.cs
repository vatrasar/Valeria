using System;
using System.Collections.Immutable;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class PreviewLayoutMetricsTests
{
    [Fact]
    public void CalculateParagraphHeight_SingleLineParagraph_ReturnsExpectedHeight()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();

        double height = metrics.CalculateParagraphHeight(1);

        double expected = metrics.BodyLineHeight + PreviewLayoutMetrics.ParagraphVerticalMargin;
        Assert.Equal(expected, height);
    }

    [Fact]
    public void CalculateParagraphHeight_MultiLineParagraph_ScalesWithLineCount()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();

        double height5 = metrics.CalculateParagraphHeight(5);
        double height1 = metrics.CalculateParagraphHeight(1);

        Assert.True(height5 > height1);
        Assert.Equal((5 * metrics.BodyLineHeight) + PreviewLayoutMetrics.ParagraphVerticalMargin, height5);
    }

    [Fact]
    public void CalculateHeadingHeight_ForLevels1Through6_CalculatesDynamicHeights()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();

        double h1 = metrics.CalculateHeadingHeight(1);
        double h2 = metrics.CalculateHeadingHeight(2);
        double h6 = metrics.CalculateHeadingHeight(6);

        Assert.True(h1 > h2);
        Assert.True(h2 > h6);
    }

    [Fact]
    public void CalculateCodeBlockHeight_SmallCodeBlock_CalculatesNaturalHeight()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();

        double height = metrics.CalculateCodeBlockHeight(3);

        double expected = (3 * metrics.CodeLineHeight) + PreviewLayoutMetrics.CodeBlockVerticalPadding;
        Assert.Equal(expected, height);
    }

    [Fact]
    public void CalculateCodeBlockHeight_LargeCodeBlock_CapsAtMaxHeight()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault(codeBlockMaxHeight: 300);

        double height = metrics.CalculateCodeBlockHeight(200);

        Assert.Equal(300, height);
    }

    [Fact]
    public void CalculateTableHeight_ForGivenRowCount_CalculatesLinearHeight()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();

        double height = metrics.CalculateTableHeight(4);

        double expected = (4 * PreviewLayoutMetrics.TableRowHeight) + PreviewLayoutMetrics.TableVerticalMargin;
        Assert.Equal(expected, height);
    }

    [Fact]
    public void CalculateListItemHeight_ForGivenLines_CalculatesItemHeight()
    {
        PreviewLayoutMetrics metrics = PreviewLayoutMetrics.CreateDefault();

        double height = metrics.CalculateListItemHeight(2);

        double expected = (2 * metrics.BodyLineHeight) + PreviewLayoutMetrics.ListItemSpacing;
        Assert.Equal(expected, height);
    }
}
