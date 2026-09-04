using Valeria.Src.Features.Editor.Resources;
using SharedStrings = Valeria.Src.Shared.Resources.GlobalStrings;
using Xunit;

namespace Valeria.Tests.CoreTests.MarkdownTests;

public sealed class LocalizationTests
{
    [Fact]
    public void EditorStrings_AllKeys_ReturnNonEmptyValues()
    {
        Assert.All(
            new[]
            {
                EditorStrings.OpenFile,
                EditorStrings.SaveFile,
                EditorStrings.SaveFileAs,
                EditorStrings.TogglePreview,
                EditorStrings.Bold,
                EditorStrings.Italic,
                EditorStrings.Strikethrough,
                EditorStrings.InlineCode,
                EditorStrings.InsertLink,
                EditorStrings.Heading1,
                EditorStrings.Heading2,
                EditorStrings.Heading3,
                EditorStrings.BulletList,
                EditorStrings.NumberedList,
                EditorStrings.Quote,
                EditorStrings.CodeBlock,
                EditorStrings.InsertTable,
                EditorStrings.CopyCode,
                EditorStrings.PlainText,
                EditorStrings.Words,
                EditorStrings.StartTypingHint,
                EditorStrings.ImagePlaceholder,
                EditorStrings.StatusCaret,
                EditorStrings.PlaceholderBoldText,
                EditorStrings.PlaceholderItalicText,
                EditorStrings.PlaceholderStrikeText,
                EditorStrings.PlaceholderCodeText,
                EditorStrings.PlaceholderLinkText
            },
            value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void GlobalStrings_AllKeys_ReturnNonEmptyValues()
    {
        Assert.All(
            new[]
            {
                SharedStrings.AppTitle,
                SharedStrings.UntitledDocument,
                SharedStrings.Open,
                SharedStrings.Save,
                SharedStrings.Cancel
            },
            value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }
}
