using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class MarkdownImageLoaderTests : IDisposable
{
    private const string TinyPngBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private readonly MarkdownImageLoader _loader = new();
    private readonly string _tempDirectory;

    public MarkdownImageLoaderTests()
    {
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ValeriaImageTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        _loader.Dispose();

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }

    [Fact]
    public async Task LoadImageAsync_ValidDataUri_ReturnsDecodedBitmap()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        await session.Dispatch(async () =>
        {
            Bitmap? bitmap = await _loader.LoadImageAsync(TinyPngBase64);

            Assert.NotNull(bitmap);
            Assert.Equal(1, bitmap.PixelSize.Width);
            Assert.Equal(1, bitmap.PixelSize.Height);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task LoadImageAsync_InvalidDataUri_ReturnsNull()
    {
        Bitmap? bitmap = await _loader.LoadImageAsync("data:image/png;base64,not-valid-base64!!!");

        Assert.Null(bitmap);
    }

    [Fact]
    public async Task LoadImageAsync_NonExistentFile_ReturnsNull()
    {
        string missingPath = Path.Combine(_tempDirectory, "missing.png");

        Bitmap? bitmap = await _loader.LoadImageAsync(missingPath);

        Assert.Null(bitmap);
    }

    [Fact]
    public async Task LoadImageAsync_LocalFileAbsolutePath_ReturnsBitmap()
    {
        await EvaluateAsync(async () =>
        {
            string filePath = Path.Combine(_tempDirectory, "test.png");
            byte[] rawBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            await File.WriteAllBytesAsync(filePath, rawBytes);

            Bitmap? bitmap = await _loader.LoadImageAsync(filePath);

            Assert.NotNull(bitmap);
            Assert.Equal(1, bitmap.PixelSize.Width);
        });
    }

    [Fact]
    public async Task LoadImageAsync_LocalFileRelativePath_WithBaseDirectory_ResolvesAndReturnsBitmap()
    {
        await EvaluateAsync(async () =>
        {
            string fileName = "photo.png";
            string filePath = Path.Combine(_tempDirectory, fileName);
            byte[] rawBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            await File.WriteAllBytesAsync(filePath, rawBytes);

            Bitmap? bitmap = await _loader.LoadImageAsync(fileName, _tempDirectory);

            Assert.NotNull(bitmap);
            Assert.Equal(1, bitmap.PixelSize.Width);
        });
    }

    [Fact]
    public async Task TryGetCached_ReturnsTrue_AfterSuccessfulLoad()
    {
        await EvaluateAsync(async () =>
        {
            Bitmap? loaded = await _loader.LoadImageAsync(TinyPngBase64);
            Assert.NotNull(loaded);

            bool found = _loader.TryGetCached(TinyPngBase64, null, out Bitmap? cached);

            Assert.True(found);
            Assert.Same(loaded, cached);
        });
    }

    [Fact]
    public async Task ClearCache_ClearsStoredEntries()
    {
        await EvaluateAsync(async () =>
        {
            await _loader.LoadImageAsync(TinyPngBase64);
            _loader.ClearCache();

            bool found = _loader.TryGetCached(TinyPngBase64, null, out Bitmap? cached);

            Assert.False(found);
            Assert.Null(cached);
        });
    }

    private static Task EvaluateAsync(Func<Task> evaluate)
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        return session.Dispatch(evaluate, CancellationToken.None);
    }
}
