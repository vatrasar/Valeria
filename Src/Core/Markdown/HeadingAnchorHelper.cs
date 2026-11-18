using System;
using System.Collections.Generic;
using System.Text;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Provides utilities for normalizing markdown anchor targets, generating GitHub-compatible heading slugs, and matching anchors.
/// </summary>
public static class HeadingAnchorHelper
{
    /// <summary>
    /// Normalizes an anchor reference by removing leading hash markers, unescaping percent encoding, and trimming whitespace.
    /// </summary>
    public static string NormalizeAnchor(string? anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
            return string.Empty;

        string trimmed = anchor.Trim();
        if (trimmed.StartsWith('#'))
            trimmed = trimmed[1..];

        try
        {
            trimmed = Uri.UnescapeDataString(trimmed);
        }
        catch (UriFormatException)
        {
        }

        return trimmed.Trim();
    }

    /// <summary>
    /// Converts arbitrary heading text into a GitHub-compatible anchor slug.
    /// </summary>
    public static string GenerateSlug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        StringBuilder builder = new();
        bool lastWasHyphen = false;

        foreach (char character in text.Trim())
            ProcessSlugChar(character, builder, ref lastWasHyphen);

        return TrimSlugHyphens(builder.ToString());
    }

    /// <summary>
    /// Extracts plain text recursively from a collection of markdown inline elements.
    /// </summary>
    public static string ExtractPlainText(IEnumerable<MarkdownInline>? inlines)
    {
        if (inlines is null)
            return string.Empty;

        StringBuilder builder = new();
        foreach (MarkdownInline inline in inlines)
            AppendInlineText(inline, builder);

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether the given heading matches the specified normalized anchor target.
    /// </summary>
    public static bool IsMatch(HeadingBlock heading, string normalizedTarget)
    {
        if (string.IsNullOrEmpty(normalizedTarget))
            return false;

        if (!string.IsNullOrEmpty(heading.AnchorId) && string.Equals(heading.AnchorId, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            return true;

        string headingText = ExtractPlainText(heading.Inlines);
        string computedSlug = GenerateSlug(headingText);

        if (!string.IsNullOrEmpty(computedSlug) && string.Equals(computedSlug, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            return true;

        if (MatchesStrippedNumbers(heading.AnchorId, normalizedTarget))
            return true;

        if (MatchesPrefixOrContainment(heading.AnchorId, computedSlug, normalizedTarget))
            return true;

        return false;
    }

    private static void ProcessSlugChar(char character, StringBuilder builder, ref bool lastWasHyphen)
    {
        if (char.IsLetterOrDigit(character))
        {
            builder.Append(char.ToLowerInvariant(character));
            lastWasHyphen = false;
        }
        else if (char.IsWhiteSpace(character) || character == '-' || character == '_')
        {
            if (!lastWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }
    }

    private static string TrimSlugHyphens(string value)
    {
        return value.Trim('-');
    }

    private static void AppendInlineText(MarkdownInline inline, StringBuilder builder)
    {
        switch (inline)
        {
            case TextRun text:
                builder.Append(text.Text);
                break;
            case CodeSpan code:
                builder.Append(code.Code);
                break;
            case BoldSpan bold:
                AppendChildrenText(bold.Children, builder);
                break;
            case ItalicSpan italic:
                AppendChildrenText(italic.Children, builder);
                break;
            case StrikethroughSpan strike:
                AppendChildrenText(strike.Children, builder);
                break;
            case LinkSpan link:
                AppendChildrenText(link.Children, builder);
                break;
            case GroupSpan group:
                AppendChildrenText(group.Children, builder);
                break;
        }
    }

    private static void AppendChildrenText(IEnumerable<MarkdownInline> children, StringBuilder builder)
    {
        foreach (MarkdownInline child in children)
            AppendInlineText(child, builder);
    }

    private static bool MatchesStrippedNumbers(string? anchorId, string target)
    {
        if (string.IsNullOrEmpty(anchorId))
            return false;

        string strippedAnchor = StripLeadingDigitsAndDots(anchorId);
        string strippedTarget = StripLeadingDigitsAndDots(target);

        if (strippedAnchor.Length > 2 && strippedTarget.Length > 2)
            return string.Equals(strippedAnchor, strippedTarget, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static bool MatchesPrefixOrContainment(string? anchorId, string computedSlug, string target)
    {
        if (!string.IsNullOrEmpty(anchorId) && anchorId.Length >= 6 && target.Length >= 6)
        {
            if (anchorId.StartsWith(target, StringComparison.OrdinalIgnoreCase) || target.StartsWith(anchorId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!string.IsNullOrEmpty(computedSlug) && computedSlug.Length >= 6 && target.Length >= 6)
        {
            if (computedSlug.StartsWith(target, StringComparison.OrdinalIgnoreCase) || target.StartsWith(computedSlug, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string StripLeadingDigitsAndDots(string value)
    {
        int index = 0;
        while (index < value.Length && (char.IsDigit(value[index]) || value[index] == '-' || value[index] == '.'))
            index++;

        return index < value.Length ? value[index..].Trim('-') : value;
    }
}
