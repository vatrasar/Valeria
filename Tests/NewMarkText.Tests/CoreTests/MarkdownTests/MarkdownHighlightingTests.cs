using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Headless;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using NewMarkText.Src.Features.Editor.Domain.Services;
using Xunit;

namespace NewMarkText.Tests.CoreTests.MarkdownTests;

public sealed class MarkdownHighlightingTests
{
    private const string HighlightingResourceName =
        "NewMarkText.Src.Features.Editor.UI.Screens.EditorScreen.Markdown.xshd";

    [Fact]
    public async Task HighlightWelcomeDocument_AllLines_HighlightsWithoutExceptions()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        string welcome = await session.Dispatch(
            () => new EditorFileService().LoadWelcomeDocumentAsync(CancellationToken.None),
            CancellationToken.None);

        await session.Dispatch(() =>
        {
            IHighlightingDefinition? definition = LoadHighlighting();

            Assert.NotNull(definition);

            TextDocument document = new(welcome);
            IHighlighter highlighter = new DocumentHighlighter(document, definition);
            System.Collections.Generic.List<string> failures = new();

            for (int line = 1; line <= document.LineCount; line++)
            {
                try
                {
                    highlighter.HighlightLine(line);
                }
                catch (System.Exception exception)
                {
                    failures.Add($"Line {line}: '{document.GetText(document.GetLineByNumber(line))}' -> {exception.Message.Split('\n')[0]}");
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }, CancellationToken.None);
    }

    private static IHighlightingDefinition? LoadHighlighting()
    {
        using Stream? stream = Assembly
            .GetAssembly(typeof(EditorFileService))!
            .GetManifestResourceStream(HighlightingResourceName);

        if (stream is null)
            return null;

        using XmlReader reader = XmlReader.Create(stream);

        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
