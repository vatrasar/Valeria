using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Headless;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class MarkdownHighlightingTests
{
    private const string HighlightingResourceName =
        "Valeria.Src.Features.Editor.UI.Screens.EditorScreen.Markdown.xshd";

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

    [Fact]
    public async Task HighlightLink_DistinguishesLinkTextAndUrlColors()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        await session.Dispatch(() =>
        {
            IHighlightingDefinition? definition = LoadHighlighting();
            Assert.NotNull(definition);

            TextDocument document = new("![Sample image](https://example.com/sample.png)");
            IHighlighter highlighter = new DocumentHighlighter(document, definition);
            HighlightedLine line = highlighter.HighlightLine(1);

            Assert.True(line.Sections.Count >= 2);
            HighlightedSection bracketSection = line.Sections[0];
            HighlightedSection urlSection = line.Sections[1];

            Assert.Equal("Link", bracketSection.Color.Name);
            Assert.Equal("Url", urlSection.Color.Name);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task HighlightCodeFence_ColorsCodeInsideDistinctFromFenceDelimiters()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        await session.Dispatch(() =>
        {
            IHighlightingDefinition? definition = LoadHighlighting();
            Assert.NotNull(definition);

            string markdown = "```csharp\nint a = 1;\nstring b = \"hello\";\n```\n# Heading Outside";
            TextDocument document = new(markdown);
            IHighlighter highlighter = new DocumentHighlighter(document, definition);

            HighlightedLine openFenceLine = highlighter.HighlightLine(1);
            HighlightedLine codeLine1 = highlighter.HighlightLine(2);
            HighlightedLine codeLine2 = highlighter.HighlightLine(3);
            HighlightedLine closeFenceLine = highlighter.HighlightLine(4);
            HighlightedLine outsideHeadingLine = highlighter.HighlightLine(5);

            Assert.Contains(openFenceLine.Sections, s => s.Color.Name == "CodeFence");
            Assert.Contains(closeFenceLine.Sections, s => s.Color.Name == "CodeFence");

            Assert.Contains(codeLine1.Sections, s => s.Color.Name == "CodeBlock");
            Assert.Contains(codeLine2.Sections, s => s.Color.Name == "CodeBlock");

            Assert.DoesNotContain(codeLine1.Sections, s => s.Color.Name == "CodeFence");
            Assert.DoesNotContain(codeLine2.Sections, s => s.Color.Name == "CodeFence");

            Assert.Contains(outsideHeadingLine.Sections, s => s.Color.Name == "Heading");
            Assert.DoesNotContain(outsideHeadingLine.Sections, s => s.Color.Name == "CodeBlock");

            HighlightingColor? fenceColor = definition.GetNamedColor("CodeFence");
            HighlightingColor? blockColor = definition.GetNamedColor("CodeBlock");

            Assert.NotNull(fenceColor);
            Assert.NotNull(blockColor);
            Assert.NotEqual(fenceColor.Foreground?.GetColor(null), blockColor.Foreground?.GetColor(null));
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
