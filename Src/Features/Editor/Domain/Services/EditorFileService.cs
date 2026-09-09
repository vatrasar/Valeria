using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// File access and encoding detection service for markdown documents.
/// Invoked by EditorViewModel open and save commands.
/// </summary>
public sealed class EditorFileService : IEditorFileService
{
    private static readonly Encoding StrictUtf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Encoding FallbackEncoding;

    static EditorFileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        FallbackEncoding = ResolveFallbackEncoding();
    }

    /// <summary>
    /// Reads text from a file with automatic BOM detection, strict UTF-8 validation, and Windows-1250 fallback.
    /// Used by EditorViewModel when opening a document.
    /// </summary>
    public async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        byte[] bytes = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        return DecodeBytes(bytes);
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
        Uri assetUri = new("avares://Valeria/Assets/Welcome.md");

        using Stream stream = AssetLoader.Open(assetUri);
        using StreamReader reader = new(stream, Encoding.UTF8);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static Encoding ResolveFallbackEncoding()
    {
        try
        {
            return Encoding.GetEncoding("windows-1250");
        }
        catch (ArgumentException)
        {
            return Encoding.Latin1;
        }
    }

    private static string DecodeBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (TryDecodeWithBom(bytes, out string? bomText))
        {
            return bomText!;
        }

        return TryDecodeStrictUtf8(bytes, out string? utf8Text)
            ? utf8Text!
            : FallbackEncoding.GetString(bytes);
    }

    private static bool TryDecodeWithBom(byte[] bytes, out string? text)
    {
        if (HasUtf8Bom(bytes))
        {
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            return true;
        }

        if (HasUtf16LeBom(bytes))
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            return true;
        }

        if (HasUtf16BeBom(bytes))
        {
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            return true;
        }

        text = null;
        return false;
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    private static bool HasUtf16LeBom(byte[] bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE;
    }

    private static bool HasUtf16BeBom(byte[] bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF;
    }

    private static bool TryDecodeStrictUtf8(byte[] bytes, out string? text)
    {
        try
        {
            text = StrictUtf8Encoding.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = null;
            return false;
        }
    }
}
