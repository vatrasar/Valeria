using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Loads images from data URIs, remote HTTP/HTTPS endpoints and local filesystem paths.
/// Keeps an in-memory cache of decoded bitmaps to prevent repeated disk and network I/O.
/// </summary>
public sealed class MarkdownImageLoader : IMarkdownImageLoader, IDisposable
{
    private const int MaxImageBytes = 25 * 1024 * 1024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _cache = new(StringComparer.Ordinal);

    public MarkdownImageLoader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    /// <summary>
    /// Asynchronously loads an image from a URL, local path or data URI.
    /// Used by MarkdownPreviewBuilder during preview rendering.
    /// </summary>
    public Task<Bitmap?> LoadImageAsync(string url, string? baseDirectory = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.FromResult<Bitmap?>(null);

        string cacheKey = BuildCacheKey(url, baseDirectory);

        return _cache.GetOrAdd(cacheKey, _ => FetchImageAsync(url, baseDirectory));
    }

    /// <summary>
    /// Attempts to retrieve a previously cached image synchronously.
    /// Used by MarkdownPreviewBuilder to avoid layout flicker.
    /// </summary>
    public bool TryGetCached(string url, string? baseDirectory, out Bitmap? bitmap)
    {
        bitmap = null;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        string cacheKey = BuildCacheKey(url, baseDirectory);

        if (!_cache.TryGetValue(cacheKey, out Task<Bitmap?>? task) || !task.IsCompletedSuccessfully)
            return false;

        bitmap = task.Result;
        return true;
    }

    /// <summary>
    /// Clears the in-memory image cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _cache.Clear();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        HttpClient client = new() { Timeout = DefaultTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ValeriaMarkdown/1.0");

        return client;
    }

    private static string BuildCacheKey(string url, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            return url.Trim();

        return $"{baseDirectory.Trim()}::{url.Trim()}";
    }

    private async Task<Bitmap?> FetchImageAsync(string url, string? baseDirectory)
    {
        string trimmed = url.Trim();

        if (IsDataUri(trimmed))
            return LoadDataUri(trimmed);

        if (IsHttpUri(trimmed))
            return await LoadRemoteImageAsync(trimmed).ConfigureAwait(false);

        if (IsFileUri(trimmed))
            return LoadFileUri(trimmed);

        return await Task.Run(() => ResolveAndLoadLocalFile(trimmed, baseDirectory)).ConfigureAwait(false);
    }

    private static bool IsDataUri(string url)
    {
        return url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpUri(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileUri(string url)
    {
        return url.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
    }

    private static Bitmap? LoadDataUri(string url)
    {
        int commaIndex = url.IndexOf(',');
        if (commaIndex < 0)
            return null;

        string metadata = url[..commaIndex];
        string payload = url[(commaIndex + 1)..];

        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            byte[] bytes = Convert.FromBase64String(payload.Trim());
            using MemoryStream stream = new(bytes);

            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<Bitmap?> LoadRemoteImageAsync(string url)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            if (response.Content.Headers.ContentLength > MaxImageBytes)
                return null;

            byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (data.Length > MaxImageBytes)
                return null;

            using MemoryStream stream = new(data);

            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Bitmap? LoadFileUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) || !parsed.IsFile)
            return null;

        return LoadLocalFile(parsed.LocalPath);
    }

    private static Bitmap? ResolveAndLoadLocalFile(string url, string? baseDirectory)
    {
        string unescaped = Uri.UnescapeDataString(url);
        string normalized = CleanPath(unescaped);

        if (Path.IsPathRooted(normalized))
            return LoadLocalFile(normalized);

        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            string combined = Path.GetFullPath(Path.Combine(baseDirectory, normalized));
            Bitmap? fromBase = LoadLocalFile(combined);
            if (fromBase is not null)
                return fromBase;
        }

        string fallback = Path.GetFullPath(normalized);

        return LoadLocalFile(fallback);
    }

    private static string CleanPath(string path)
    {
        int queryIndex = path.IndexOf('?');
        if (queryIndex > 0)
            return path[..queryIndex];

        return path;
    }

    private static Bitmap? LoadLocalFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            using FileStream stream = File.OpenRead(filePath);

            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
