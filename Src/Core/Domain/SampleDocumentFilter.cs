using System;
using System.IO;

namespace Valeria.Src.Core.Domain;

/// <summary>
/// Identifies test, sample, or welcome documents that must not be tracked in recent files history.
/// </summary>
public static class SampleDocumentFilter
{
    /// <summary>
    /// Checks whether the given file path or file name represents a sample, welcome, or test document.
    /// Invoked by RecentFileRepository, FilesDockService, and FilesDockViewModel.
    /// </summary>
    public static bool IsSampleOrTestDocument(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        string fileName = Path.GetFileName(path);

        return fileName.Contains("sample", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("test-", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("test_", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("-test.md", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("test.md", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Welcome.md", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/Tests/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/bin/Debug/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/bin/Release/", StringComparison.OrdinalIgnoreCase);
    }
}
