using System.IO;
using System.Linq;
using System.Reflection;
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
}
