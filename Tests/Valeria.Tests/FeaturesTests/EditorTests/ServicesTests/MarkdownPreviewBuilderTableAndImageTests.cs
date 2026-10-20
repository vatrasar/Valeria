using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class MarkdownPreviewBuilderTableAndImageTests
{
    private readonly MarkdownImageLoader _imageLoader = new();
    private readonly MarkdownPreviewBuilder _builder;

    public MarkdownPreviewBuilderTableAndImageTests()
    {
        _builder = new MarkdownPreviewBuilder(
            new CodeSyntaxService(),
            Options.Create(new AppConfig()),
            _imageLoader);
    }

    [Fact]
    public async Task BuildBlocks_Table_ReturnsGridWithHeaderAndBodyCells()
    {
        (int cellCount, bool hasBoldHeader) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            List<SelectableTextBlock> cells = Build("| A | B |\n| --- | --- |\n| 1 | 2 |")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<Grid>)
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .ToList();

            return (cells.Count, cells.Any(cell => cell.FontWeight == FontWeight.Bold));
        });

        Assert.True(cellCount >= 4);
        Assert.True(hasBoldHeader);
    }

    [Fact]
    public async Task BuildBlocks_TableWithLongText_WrapsCellContent()
    {
        (int cellCount, int lineCount, TextWrapping wrapping, TextAlignment alignment) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            string markdown = "| Short | Long Content Header |\n| :--- | :---: |\n| 1 | This is a very long text inside the table cell that should definitely wrap across multiple lines rather than remaining on a single line. |";
            IReadOnlyList<Control> blocks = Build(markdown);
            Control tableControl = blocks.First();

            Window window = new() { Width = 400, Height = 600, Content = tableControl };
            try
            {
                window.Show();
                tableControl.Measure(new Size(400, 600));
                tableControl.Arrange(new Rect(0, 0, 400, 600));
                Dispatcher.UIThread.RunJobs();

                List<SelectableTextBlock> cells = MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>(tableControl).ToList();
                if (cells.Count == 0 && tableControl is ContentControl contentControl && contentControl.Content is Control contentChild)
                    cells = MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>(contentChild).ToList();

                SelectableTextBlock? longCell = cells.LastOrDefault();

                return (
                    cells.Count,
                    longCell?.TextLayout?.TextLines?.Count ?? 0,
                    longCell?.TextWrapping ?? TextWrapping.NoWrap,
                    longCell?.TextAlignment ?? TextAlignment.Left);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });

        Assert.True(cellCount >= 4);
        Assert.True(lineCount > 1);
        Assert.Equal(TextWrapping.Wrap, wrapping);
        Assert.Equal(TextAlignment.Center, alignment);
    }

    [Fact]
    public async Task BuildBlocks_Table_IndexColumnIsCompactAndTableFillsFullWidth()
    {
        (double tableWidth, double col0Width, double col1Width, bool col0IsAuto, bool col1IsStar) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            string markdown = "| # | Description |\n| - | --- |\n| 1 | This is a detailed description that takes more space than a single digit index column. |";
            IReadOnlyList<Control> blocks = Build(markdown);
            Control tableControl = blocks.First();

            Window window = new() { Width = 500, Height = 600, Content = tableControl };
            try
            {
                window.Show();
                tableControl.Measure(new Size(500, 600));
                tableControl.Arrange(new Rect(0, 0, 500, 600));
                Dispatcher.UIThread.RunJobs();

                Grid grid = MarkdownPreviewTestHelpers.Descendants<Grid>(tableControl).First();
                Border cell0 = MarkdownPreviewTestHelpers.Descendants<Border>(grid).First();
                Border cell1 = MarkdownPreviewTestHelpers.Descendants<Border>(grid).Skip(1).First();

                bool isAuto = grid.ColumnDefinitions[0].Width.IsAuto;
                bool isStar = grid.ColumnDefinitions[1].Width.IsStar;

                return (
                    grid.Bounds.Width,
                    cell0.Bounds.Width,
                    cell1.Bounds.Width,
                    isAuto,
                    isStar);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });

        Assert.True(tableWidth >= 499 && tableWidth <= 500);
        Assert.True(col0IsAuto);
        Assert.True(col1IsStar);
        Assert.True(col0Width < 80);
        Assert.True(col1Width > 400);
    }

    [Fact]
    public async Task BuildBlocks_ImageCached_RendersImageControlWithBitmap()
    {
        const string tinyPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        await _imageLoader.LoadImageAsync(tinyPng);

        (bool hasImage, bool hasSource, object? tip) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build($"![Sample Image]({tinyPng})")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .First();

            InlineUIContainer container = textBlock.Inlines!.OfType<InlineUIContainer>().First();
            Border border = Assert.IsType<Border>(container.Child);
            Image? img = border.Child as Image;
            object? tooltip = ToolTip.GetTip(border);

            return (img is not null, img?.Source is not null, tooltip);
        });

        Assert.True(hasImage);
        Assert.True(hasSource);
        Assert.Equal($"Sample Image ({tinyPng})", tip);
    }

    [Fact]
    public async Task BuildBlocks_ImageUncached_RendersContainerWithToolTip()
    {
        const string url = "https://example.com/photo.png";

        (Border border, object? tip) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
        {
            SelectableTextBlock textBlock = Build($"![Sample Image]({url})")
                .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                .First();

            InlineUIContainer container = textBlock.Inlines!.OfType<InlineUIContainer>().First();
            Border b = Assert.IsType<Border>(container.Child);
            object? tooltip = ToolTip.GetTip(b);

            return (b, tooltip);
        });

        Assert.NotNull(border);
        Assert.Equal($"Sample Image ({url})", tip);
    }

    [Fact]
    public async Task BuildBlocks_ImageRelativePath_WithBaseDirectory_RendersImageControlWhenCached()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ValeriaPreviewImageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string fileName = "preview-pic.png";
            string filePath = Path.Combine(tempDir, fileName);
            byte[] rawBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            await File.WriteAllBytesAsync(filePath, rawBytes);

            await _imageLoader.LoadImageAsync(fileName, tempDir);

            (bool hasImage, bool hasSource) = await MarkdownPreviewTestHelpers.EvaluateAsync(() =>
            {
                SelectableTextBlock textBlock = Build($"![Photo]({fileName})", tempDir)
                    .SelectMany(MarkdownPreviewTestHelpers.Descendants<SelectableTextBlock>)
                    .First();

                InlineUIContainer container = textBlock.Inlines!.OfType<InlineUIContainer>().First();
                Border border = Assert.IsType<Border>(container.Child);
                Image? img = border.Child as Image;

                return (img is not null, img?.Source is not null);
            });

            Assert.True(hasImage);
            Assert.True(hasSource);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private IReadOnlyList<Control> Build(string markdown, string? baseDirectory = null)
    {
        return _builder.BuildBlocks(MarkdownParser.Parse(markdown), null, baseDirectory).Blocks;
    }
}
