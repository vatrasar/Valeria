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

        return text.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
