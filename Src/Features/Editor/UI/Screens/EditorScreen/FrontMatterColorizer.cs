using System;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Colorizes YAML frontmatter header blocks in the source editor with specialized syntax highlighting.
/// </summary>
public sealed class FrontMatterColorizer : DocumentColorizingTransformer
{
    private static readonly IBrush DelimiterBrush = new SolidColorBrush(Color.Parse("#4FA3E0"));
    private static readonly IBrush KeyBrush = new SolidColorBrush(Color.Parse("#4EC9B0"));
    private static readonly IBrush ColonBrush = new SolidColorBrush(Color.Parse("#808080"));
    private static readonly IBrush StringBrush = new SolidColorBrush(Color.Parse("#CE9178"));
    private static readonly IBrush BoolBrush = new SolidColorBrush(Color.Parse("#569CD6"));
    private static readonly IBrush BulletBrush = new SolidColorBrush(Color.Parse("#C586C0"));
    private static readonly IBrush CommentBrush = new SolidColorBrush(Color.Parse("#6A9955"));
    private static readonly IBrush BlockBackground = new SolidColorBrush(Color.FromArgb(18, 79, 163, 224));
    private static readonly IBrush DelimiterBackground = new SolidColorBrush(Color.FromArgb(28, 79, 163, 224));

    protected override void ColorizeLine(DocumentLine line)
    {
        TextDocument document = CurrentContext.Document;
        if (document.LineCount < 2)
            return;

        int endLineNumber = FindFrontMatterEndLine(document);
        if (endLineNumber <= 1)
            return;

        if (line.LineNumber < 1 || line.LineNumber > endLineNumber)
            return;

        if (line.LineNumber == 1 || line.LineNumber == endLineNumber)
        {
            ColorizeDelimiterLine(line);
            return;
        }

        ColorizeContentLine(line, document.GetText(line));
    }

    private static int FindFrontMatterEndLine(TextDocument document)
    {
        DocumentLine line1 = document.GetLineByNumber(1);
        string line1Text = document.GetText(line1).Trim();

        if (line1Text != "---")
            return 0;

        int maxScan = Math.Min(document.LineCount, 150);
        for (int i = 2; i <= maxScan; i++)
        {
            DocumentLine currentLine = document.GetLineByNumber(i);
            if (document.GetText(currentLine).Trim() == "---")
            {
                int startOffset = line1.EndOffset;
                int endOffset = currentLine.Offset;
                string candidateYaml = document.GetText(startOffset, endOffset - startOffset);
                if (FrontMatterParser.IsValid(candidateYaml))
                    return i;

                return 0;
            }
        }

        return 0;
    }

    private void ColorizeDelimiterLine(DocumentLine line)
    {
        SetLineBackground(line, DelimiterBackground);
        StyleRange(line.Offset, line.EndOffset, DelimiterBrush, FontWeight.Bold);
    }

    private void ColorizeContentLine(DocumentLine line, string text)
    {
        SetLineBackground(line, BlockBackground);

        int commentIndex = text.IndexOf('#');
        int codeEndOffset = commentIndex >= 0 ? line.Offset + commentIndex : line.EndOffset;

        if (commentIndex >= 0)
            StyleRange(line.Offset + commentIndex, line.EndOffset, CommentBrush);

        string codePart = commentIndex >= 0 ? text.Substring(0, commentIndex) : text;
        ColorizeYamlCode(line.Offset, codePart);
    }

    private void ColorizeYamlCode(int lineStartOffset, string code)
    {
        string trimmed = code.TrimStart();
        int indent = code.Length - trimmed.Length;

        if (trimmed.StartsWith("- "))
        {
            StyleRange(lineStartOffset + indent, lineStartOffset + indent + 2, BulletBrush, FontWeight.Bold);
            ColorizeValue(lineStartOffset + indent + 2, trimmed.Substring(2));
            return;
        }

        int colonIndex = code.IndexOf(':');
        if (colonIndex >= 0)
        {
            ColorizeKeyValuePair(lineStartOffset, code, colonIndex, indent);
            return;
        }

        if (trimmed.Length > 0)
            StyleRange(lineStartOffset + indent, lineStartOffset + code.Length, StringBrush);
    }

    private void ColorizeKeyValuePair(int lineStartOffset, string code, int colonIndex, int indent)
    {
        StyleRange(lineStartOffset + indent, lineStartOffset + colonIndex, KeyBrush, FontWeight.SemiBold);
        StyleRange(lineStartOffset + colonIndex, lineStartOffset + colonIndex + 1, ColonBrush);

        string afterColon = code.Substring(colonIndex + 1);
        if (afterColon.Length > 0)
            ColorizeValue(lineStartOffset + colonIndex + 1, afterColon);
    }

    private void ColorizeValue(int valueStartOffset, string valueText)
    {
        string trimmed = valueText.TrimStart();
        int leadingSpaces = valueText.Length - trimmed.Length;
        int trimmedLength = trimmed.TrimEnd().Length;

        if (trimmedLength <= 0)
            return;

        int start = valueStartOffset + leadingSpaces;
        int end = start + trimmedLength;
        string core = trimmed.Substring(0, trimmedLength);

        if (IsBoolean(core))
        {
            StyleRange(start, end, BoolBrush, FontWeight.Bold);
            return;
        }

        StyleRange(start, end, StringBrush);
    }

    private static bool IsBoolean(string value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "no", StringComparison.OrdinalIgnoreCase);
    }

    private void SetLineBackground(DocumentLine line, IBrush background)
    {
        if (line.Length <= 0)
            return;

        ChangeLinePart(line.Offset, line.EndOffset, element =>
        {
            element.BackgroundBrush = background;
        });
    }

    private void StyleRange(int startOffset, int endOffset, IBrush brush, FontWeight? weight = null)
    {
        if (startOffset >= endOffset)
            return;

        ChangeLinePart(startOffset, endOffset, element =>
        {
            element.TextRunProperties.SetForegroundBrush(brush);
            if (weight.HasValue)
            {
                Typeface current = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(new Typeface(current.FontFamily, current.Style, weight.Value));
            }
        });
    }
}
