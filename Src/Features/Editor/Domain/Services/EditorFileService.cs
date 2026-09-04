using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace NewMarkText.Src.Features.Editor.Domain.Services;

/// <summary>
/// UTF-8 file access for markdown documents.
/// Invoked by EditorViewModel open and save commands.
/// </summary>
public sealed class EditorFileService : IEditorFileService
{
    /// <summary>
    /// Reads the whole file as UTF-8 text.
    /// Used by EditorViewModel when opening a document.
    /// </summary>
    public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        return System.IO.File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    /// Writes the whole document as UTF-8 text.
    /// Used by EditorViewModel when saving a document.
    /// </summary>
    public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        return System.IO.File.WriteAllTextAsync(path, content ?? string.Empty, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    /// Loads the bundled welcome document shown on first launch.
    /// Used by EditorViewModel initialization.
    /// </summary>
    public async Task<string> LoadWelcomeDocumentAsync(CancellationToken cancellationToken)
    {
        Uri assetUri = new("avares://NewMarkText/Assets/Welcome.md");

        using Stream stream = AssetLoader.Open(assetUri);
        using StreamReader reader = new(stream, Encoding.UTF8);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}
