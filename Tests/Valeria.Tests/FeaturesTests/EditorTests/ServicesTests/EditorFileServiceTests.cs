using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorFileServiceTests
{
    private const string HighlightingResourceName =
        "Valeria.Src.Features.Editor.UI.Screens.EditorScreen.Markdown.xshd";

    private readonly EditorFileService _files = new();

    [Fact]
    public async Task LoadWelcomeDocumentAsync_ReturnsNonEmptyMarkdown()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        string welcome = await session.Dispatch(
            () => _files.LoadWelcomeDocumentAsync(CancellationToken.None),
            CancellationToken.None);

        Assert.Contains("# ", welcome);
    }

    [Fact]
    public void EmbeddedResources_ContainMarkdownHighlightingDefinition()
    {
        string[] names = Assembly
            .GetAssembly(typeof(EditorFileService))!
            .GetManifestResourceNames();

        Assert.Contains(HighlightingResourceName, names);
    }

    [Fact]
    public async Task WriteAndReadText_RoundTripsContent()
    {
        string path = Path.GetTempFileName();

        try
        {
            await _files.WriteTextAsync(path, "# hello", CancellationToken.None);

            Assert.Equal("# hello", await _files.ReadTextAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadTextAsync_FallsBackToWindows1250_WhenBytesAreNotValidUtf8()
    {
        string path = Path.GetTempFileName();
        string expectedText = "najlepiej używać właściwie";
        Encoding windows1250 = Encoding.GetEncoding("windows-1250");
        byte[] bytes = windows1250.GetBytes(expectedText);

        try
        {
            await File.WriteAllBytesAsync(path, bytes, CancellationToken.None);

            string actual = await _files.ReadTextAsync(path, CancellationToken.None);

            Assert.Equal(expectedText, actual);
            Assert.DoesNotContain("\uFFFD", actual);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadTextAsync_StripsUtf8Bom_WhenPresent()
    {
        string path = Path.GetTempFileName();
        string expectedText = "# Nagłówek z polskimi znakami: ążśźćółęń";
        byte[] contentBytes = Encoding.UTF8.GetBytes(expectedText);
        byte[] fileBytes = [0xEF, 0xBB, 0xBF, .. contentBytes];

        try
        {
            await File.WriteAllBytesAsync(path, fileBytes, CancellationToken.None);

            string actual = await _files.ReadTextAsync(path, CancellationToken.None);

            Assert.Equal(expectedText, actual);
            Assert.False(actual.StartsWith('\uFEFF'));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
