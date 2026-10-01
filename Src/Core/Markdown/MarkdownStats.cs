using System;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Read-only statistics computed from markdown source.
/// Used by the editor status bar.
/// </summary>
public static class MarkdownStats
{
    /// <summary>
    /// Counts whitespace-separated words. Returns zero for null or blank input.
    /// Used by EditorViewModel status bar updates.
    /// </summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        ReadOnlySpan<char> span = text.AsSpan();
        int count = 0;
        bool inWord = false;

        foreach (char character in span)
            ProcessCharacter(character, ref count, ref inWord);

        return count;
    }

    private static void ProcessCharacter(char character, ref int count, ref bool inWord)
    {
        if (char.IsWhiteSpace(character))
        {
            inWord = false;
            return;
        }

        if (!inWord)
        {
            inWord = true;
            count++;
        }
    }
}
