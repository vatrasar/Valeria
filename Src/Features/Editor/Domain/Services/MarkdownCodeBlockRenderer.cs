using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

internal sealed class MarkdownCodeBlockRenderer
{
    private readonly ICodeSyntaxService _syntax;
    private readonly PreviewOptions _options;

    public MarkdownCodeBlockRenderer(ICodeSyntaxService syntax, PreviewOptions options)
    {
        _syntax = syntax;
        _options = options;
    }

    public Control BuildCodeBlock(
        CodeBlock code,
        ThemeResources theme,
        Action<CodeHighlightTarget> registerTarget)
    {
        SelectableTextBlock text = CreateCodeText(code, theme);
        if (!code.SourceSpan.IsEmpty)
            text.Tag = code.SourceSpan;
        else if (!code.LineRange.IsEmpty)
            text.Tag = code.LineRange;

        registerTarget(new CodeHighlightTarget(text, code.Language, code.Code.TrimEnd('\n')));

        Border header = CreateCodeHeader(code, theme);

        ScrollViewer scroller = new()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = _options.CodeBlockMaxHeight,
            Content = text
        };

        DockPanel layout = new() { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(scroller);

        return new Border
        {
            Background = theme.CodeBlockBackground,
            BorderBrush = theme.CodeBlockBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 8),
            ClipToBounds = true,
            Child = layout
        };
    }

    public void ApplyHighlight(CodeHighlightTarget target, IReadOnlyList<HighlightedLine> lines)
    {
        SelectableTextBlock text = target.TextBlock;
        ThemeResources theme = ThemeResources.Resolve();
        InlineCollection? inlines = text.Inlines;

        if (inlines is null)
            return;

        text.Text = string.Empty;
        inlines.Clear();

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            if (lineIndex > 0)
                inlines.Add(new LineBreak());

            FillLineInlines(lines[lineIndex], inlines, theme);
        }
    }

    private void FillLineInlines(HighlightedLine line, InlineCollection target, ThemeResources theme)
    {
        foreach (HighlightedSpan span in line.Spans)
            target.Add(CreateSpanRun(span, theme));
    }

    private static Run CreateSpanRun(HighlightedSpan span, ThemeResources theme)
    {
        return new Run(span.Text)
        {
            FontFamily = theme.MonoFont,
            FontSize = theme.CodeFontSize,
            Foreground = TryCreateBrush(span.ForegroundHex) ?? theme.Body,
            FontWeight = span.IsBold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = span.IsItalic ? FontStyle.Italic : FontStyle.Normal
        };
    }

    private static IBrush? TryCreateBrush(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return null;

        string value = hex.TrimStart('#');

        if (value.Length == 6 && uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

        if (value.Length == 8 && uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            return new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

        return null;
    }

    private static SelectableTextBlock CreateCodeText(CodeBlock code, ThemeResources theme)
    {
        return new SelectableTextBlock
        {
            Text = code.Code.TrimEnd('\n'),
            FontFamily = theme.MonoFont,
            FontSize = theme.CodeFontSize,
            Foreground = theme.Body,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(12, 8, 32, 24)
        };
    }

    private Border CreateCodeHeader(CodeBlock code, ThemeResources theme)
    {
        TextBlock languageLabel = new()
        {
            Text = _syntax.GetLanguageDisplayName(code.Language),
            FontFamily = theme.MonoFont,
            FontSize = 12,
            Foreground = theme.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };

        Button copyButton = new()
        {
            Content = new MaterialIcon { Kind = MaterialIconKind.ContentCopy, Width = 14, Height = 14 },
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };

        ToolTip.SetTip(copyButton, Resources.EditorStrings.CopyCode);
        copyButton.Click += (_, _) => CopyCodeToClipboard(copyButton, code.Code);

        DockPanel headerContent = new() { LastChildFill = true };
        DockPanel.SetDock(copyButton, Dock.Right);
        headerContent.Children.Add(copyButton);
        headerContent.Children.Add(languageLabel);

        return new Border
        {
            Background = theme.CodeBlockHeaderBackground,
            BorderBrush = theme.CodeBlockBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 4, 6, 4),
            Child = headerContent
        };
    }

    private static void CopyCodeToClipboard(Control anchor, string code)
    {
        try
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(anchor);
            _ = topLevel?.Clipboard?.SetTextAsync(code);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Copy code failed: {exception.Message}");
        }
    }
}
