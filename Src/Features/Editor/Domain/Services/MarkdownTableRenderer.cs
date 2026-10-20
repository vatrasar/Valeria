using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

internal sealed class MarkdownTableRenderer
{
    private readonly MarkdownInlineRenderer _inlineRenderer;

    public MarkdownTableRenderer(MarkdownInlineRenderer inlineRenderer)
    {
        _inlineRenderer = inlineRenderer;
    }

    public Control BuildTable(
        TableBlock table,
        ThemeResources theme,
        BrushSet brushes,
        string? baseDirectory)
    {
        int columnCount = CountTableColumns(table);

        if (columnCount == 0)
            return CreateEmptyCellTextBlock(theme, brushes.Body);

        List<int> columnLengths = GetColumnLengths(table, columnCount);
        int maxColumnLength = columnLengths.Count > 0 ? columnLengths.Max() : 0;

        Grid grid = new();

        for (int column = 0; column < columnCount; column++)
        {
            int length = column < columnLengths.Count ? columnLengths[column] : 0;
            grid.ColumnDefinitions.Add(CreateTableColumnDefinition(length, maxColumnLength));
        }

        int rowIndex = 0;

        if (table.Header is not null)
        {
            AddTableRow(grid, table, table.Header, theme, brushes, columnCount, rowIndex, true, baseDirectory);
            rowIndex++;
        }

        foreach (TableRow row in table.Rows)
        {
            AddTableRow(grid, table, row, theme, brushes, columnCount, rowIndex, false, baseDirectory);
            rowIndex++;
        }

        Border frame = new()
        {
            BorderBrush = theme.TableCellBorder,
            BorderThickness = new Thickness(0, 0, 1, 1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 8),
            ClipToBounds = true,
            Child = grid
        };

        return new MarkdownTableScrollViewer
        {
            ColumnCount = columnCount,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = frame
        };
    }

    private static ColumnDefinition CreateTableColumnDefinition(int columnLength, int maxColumnLength)
    {
        if (ShouldBeCompactColumn(columnLength, maxColumnLength))
            return new ColumnDefinition(GridLength.Auto);

        double weight = CalculateStarWeight(columnLength);

        return new ColumnDefinition(new GridLength(weight, GridUnitType.Star));
    }

    private static bool ShouldBeCompactColumn(int columnLength, int maxColumnLength)
    {
        if (columnLength > 16)
            return false;

        if (columnLength == maxColumnLength)
            return false;

        return columnLength <= 8 || columnLength * 2 <= maxColumnLength;
    }

    private static double CalculateStarWeight(int columnLength)
    {
        if (columnLength <= 25)
            return 1.0;

        if (columnLength <= 60)
            return 1.5;

        if (columnLength <= 120)
            return 2.0;

        return 3.0;
    }

    private static List<int> GetColumnLengths(TableBlock table, int columnCount)
    {
        List<int> lengths = new(columnCount);

        for (int column = 0; column < columnCount; column++)
            lengths.Add(GetColumnMaxTextLength(table, column));

        return lengths;
    }

    private static int GetColumnMaxTextLength(TableBlock table, int column)
    {
        int maxLength = 0;

        if (table.Header is not null && column < table.Header.Cells.Count)
            maxLength = Math.Max(maxLength, GetCellTextLength(table.Header.Cells[column]));

        foreach (TableRow row in table.Rows)
        {
            if (column < row.Cells.Count)
                maxLength = Math.Max(maxLength, GetCellTextLength(row.Cells[column]));
        }

        return maxLength;
    }

    private static int GetCellTextLength(TableCell cell)
    {
        int length = 0;

        foreach (MarkdownInline inline in cell.Inlines)
            length += GetInlineTextLength(inline);

        return length;
    }

    private static int GetInlineTextLength(MarkdownInline inline)
    {
        return inline switch
        {
            TextRun text => text.Text.Length,
            CodeSpan code => code.Code.Length,
            LinkSpan link => link.Children.Sum(GetInlineTextLength),
            BoldSpan bold => bold.Children.Sum(GetInlineTextLength),
            ItalicSpan italic => italic.Children.Sum(GetInlineTextLength),
            StrikethroughSpan strike => strike.Children.Sum(GetInlineTextLength),
            GroupSpan group => group.Children.Sum(GetInlineTextLength),
            _ => 0
        };
    }

    private static int CountTableColumns(TableBlock table)
    {
        int headerCells = table.Header?.Cells.Count ?? 0;
        int bodyCells = table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Cells.Count);

        return Math.Max(headerCells, bodyCells);
    }

    private void AddTableRow(
        Grid grid,
        TableBlock table,
        TableRow row,
        ThemeResources theme,
        BrushSet brushes,
        int columnCount,
        int rowIndex,
        bool isHeader,
        string? baseDirectory)
    {
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int column = 0; column < columnCount; column++)
        {
            TableCell cell = column < row.Cells.Count
                ? row.Cells[column]
                : new TableCell(ImmutableList<MarkdownInline>.Empty);

            Border cellControl = CreateTableCell(cell, ResolveColumnAlignment(table, column), theme, brushes, isHeader, baseDirectory);
            Grid.SetRow(cellControl, rowIndex);
            Grid.SetColumn(cellControl, column);
            grid.Children.Add(cellControl);
        }
    }

    private Border CreateTableCell(
        TableCell cell,
        TableColumnAlignment alignment,
        ThemeResources theme,
        BrushSet brushes,
        bool isHeader,
        string? baseDirectory)
    {
        SelectableTextBlock text = CreateEmptyCellTextBlock(theme, brushes.Body);
        text.TextAlignment = MapTextAlignment(alignment);
        _inlineRenderer.AppendInlines(cell.Inlines, text.Inlines, theme, brushes, theme.BodyFontSize, baseDirectory);
        MarkdownLinkHandler.Attach(text);

        if (isHeader)
            text.FontWeight = FontWeight.Bold;

        return new Border
        {
            Background = isHeader ? theme.TableHeaderBackground : Brushes.Transparent,
            BorderBrush = theme.TableCellBorder,
            BorderThickness = new Thickness(1, 1, 0, 0),
            Padding = new Thickness(13, 6),
            Child = text
        };
    }

    private static SelectableTextBlock CreateEmptyCellTextBlock(ThemeResources theme, IBrush foreground)
    {
        return new SelectableTextBlock
        {
            FontFamily = theme.ProseFont,
            FontSize = theme.BodyFontSize,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = theme.BodyFontSize * 1.5
        };
    }

    private static TableColumnAlignment ResolveColumnAlignment(TableBlock table, int column)
    {
        if (column < table.Alignments.Count)
            return table.Alignments[column];

        return TableColumnAlignment.None;
    }

    private static TextAlignment MapTextAlignment(TableColumnAlignment alignment)
    {
        return alignment switch
        {
            TableColumnAlignment.Center => TextAlignment.Center,
            TableColumnAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };
    }
}
