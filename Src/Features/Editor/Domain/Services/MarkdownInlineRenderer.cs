using System.Collections.Immutable;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

internal sealed class MarkdownInlineRenderer
{
    private readonly MarkdownImageRenderer _imageRenderer;

    public MarkdownInlineRenderer(MarkdownImageRenderer imageRenderer)
    {
        _imageRenderer = imageRenderer;
    }

    public void AppendInlines(
        ImmutableList<MarkdownInline> inlines,
        InlineCollection? target,
        ThemeResources theme,
        BrushSet brushes,
        double fontSize,
        string? baseDirectory)
    {
        if (target is null)
            return;

        foreach (MarkdownInline inline in inlines)
            AppendInline(inline, target, theme, brushes, fontSize, baseDirectory);
    }

    public void AppendInline(
        MarkdownInline inline,
        InlineCollection target,
        ThemeResources theme,
        BrushSet brushes,
        double fontSize,
        string? baseDirectory)
    {
        switch (inline)
        {
            case TextRun text:
                target.Add(new Run(text.Text));
                break;
            case BoldSpan bold:
                AppendSpan(target, FontWeight.Bold, null, null, bold.Children, theme, brushes, fontSize, baseDirectory);
                break;
            case ItalicSpan italic:
                AppendSpan(target, null, FontStyle.Italic, null, italic.Children, theme, brushes, fontSize, baseDirectory);
                break;
            case StrikethroughSpan strike:
                AppendSpan(target, null, null, TextDecorations.Strikethrough, strike.Children, theme, brushes, fontSize, baseDirectory);
                break;
            case CodeSpan code:
                target.Add(CreateCodeRun(code.Code, theme, fontSize));
                break;
            case LinkSpan link:
                target.Add(CreateLink(link, theme, brushes, fontSize, baseDirectory));
                break;
            case ImageSpan image:
                target.Add(new InlineUIContainer(_imageRenderer.CreateImageControl(image, theme, baseDirectory)));
                break;
            case GroupSpan group:
                AppendInlines(group.Children, target, theme, brushes, fontSize, baseDirectory);
                break;
            case HardLineBreak:
                target.Add(new LineBreak());
                break;
        }
    }

    private void AppendSpan(
        InlineCollection target,
        FontWeight? weight,
        FontStyle? style,
        TextDecorationCollection? decorations,
        ImmutableList<MarkdownInline> children,
        ThemeResources theme,
        BrushSet brushes,
        double fontSize,
        string? baseDirectory)
    {
        Span span = new();

        if (weight.HasValue)
            span.FontWeight = weight.Value;

        if (style.HasValue)
            span.FontStyle = style.Value;

        if (decorations is not null)
            span.TextDecorations = decorations;

        AppendInlines(children, span.Inlines, theme, brushes, fontSize, baseDirectory);
        target.Add(span);
    }

    private static Run CreateCodeRun(string code, ThemeResources theme, double fontSize)
    {
        return new Run(code)
        {
            FontFamily = theme.MonoFont,
            FontSize = fontSize * 0.85,
            Background = theme.CodeInlineBackground,
            Foreground = theme.CodeInlineForeground
        };
    }

    private Span CreateLink(LinkSpan link, ThemeResources theme, BrushSet brushes, double fontSize, string? baseDirectory)
    {
        Span linkSpan = new()
        {
            Foreground = theme.Accent
        };

        MarkdownLink.SetUrl(linkSpan, link.Url);
        MarkdownLink.SetHoverForeground(linkSpan, theme.AccentHover);
        AppendInlines(link.Children, linkSpan.Inlines, theme, brushes, fontSize, baseDirectory);

        return linkSpan;
    }
}
