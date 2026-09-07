using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Loads and caches images for markdown preview from local files, web URLs and data URIs.
/// Invoked by MarkdownPreviewBuilder.
/// </summary>
public interface IMarkdownImageLoader
{
    /// <summary>
    /// Asynchronously loads an image from a URL, local path or data URI.
    /// Used by MarkdownPreviewBuilder during preview rendering.
    /// </summary>
    Task<Bitmap?> LoadImageAsync(string url, string? baseDirectory = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a previously cached image synchronously.
    /// Used by MarkdownPreviewBuilder to avoid layout flicker.
    /// </summary>
    bool TryGetCached(string url, string? baseDirectory, out Bitmap? bitmap);

    /// <summary>
    /// Clears the in-memory image cache.
    /// </summary>
    void ClearCache();
}
