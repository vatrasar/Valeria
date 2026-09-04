using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Valeria.Src.Infrastructure.Services;

/// <summary>
/// Native file dialogs for markdown files based on Avalonia StorageProvider.
/// Invoked by EditorViewModel file commands.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType MarkdownFileType = new("Markdown")
    {
        Patterns = new[] { "*.md", "*.markdown", "*.mdown", "*.mkd", "*.mdx" },
        MimeTypes = new[] { "text/markdown" }
    };

    private Func<TopLevel?> _topLevelProvider = static () => null;

    public void SetTopLevelProvider(Func<TopLevel?> provider)
    {
        _topLevelProvider = provider;
    }

    /// <summary>
    /// Shows the native open dialog and returns the selected local path, if any.
    /// Used by EditorViewModel open command.
    /// </summary>
    public async Task<string?> PickMarkdownFileToOpenAsync(CancellationToken cancellationToken)
    {
        TopLevel? topLevel = _topLevelProvider();

        if (topLevel?.StorageProvider is not { } storage)
            return null;

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown File",
            AllowMultiple = false,
            FileTypeFilter = new[] { MarkdownFileType, FilePickerFileTypes.All }
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    /// <summary>
    /// Shows the native save dialog and returns the selected local path, if any.
    /// Used by EditorViewModel save commands.
    /// </summary>
    public async Task<string?> PickMarkdownFileToSaveAsync(string? suggestedFileName, CancellationToken cancellationToken)
    {
        TopLevel? topLevel = _topLevelProvider();

        if (topLevel?.StorageProvider is not { } storage)
            return null;

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Markdown File",
            SuggestedFileName = suggestedFileName ?? "untitled.md",
            DefaultExtension = "md",
            FileTypeChoices = new[] { MarkdownFileType, FilePickerFileTypes.All }
        });

        return file?.TryGetLocalPath();
    }
}
