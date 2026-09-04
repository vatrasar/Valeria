using System.Globalization;
using System.Resources;

#nullable enable

namespace NewMarkText.Src.Features.Editor.Resources;

/// <summary>
/// Strongly-typed access to Editor/Resources/EditorStrings.resx.
/// Mirrors the output of PublicResXFileCodeGenerator.
/// </summary>
public static class EditorStrings
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    public static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "NewMarkText.Src.Features.Editor.Resources.EditorStrings",
                typeof(EditorStrings).Assembly);

            return _resourceManager;
        }
    }

    public static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    public static string OpenFile => ResourceManager.GetString("OpenFile", _resourceCulture)!;

    public static string SaveFile => ResourceManager.GetString("SaveFile", _resourceCulture)!;

    public static string SaveFileAs => ResourceManager.GetString("SaveFileAs", _resourceCulture)!;

    public static string TogglePreview => ResourceManager.GetString("TogglePreview", _resourceCulture)!;

    public static string Bold => ResourceManager.GetString("Bold", _resourceCulture)!;

    public static string Italic => ResourceManager.GetString("Italic", _resourceCulture)!;

    public static string Strikethrough => ResourceManager.GetString("Strikethrough", _resourceCulture)!;

    public static string InlineCode => ResourceManager.GetString("InlineCode", _resourceCulture)!;

    public static string InsertLink => ResourceManager.GetString("InsertLink", _resourceCulture)!;

    public static string Heading1 => ResourceManager.GetString("Heading1", _resourceCulture)!;

    public static string Heading2 => ResourceManager.GetString("Heading2", _resourceCulture)!;

    public static string Heading3 => ResourceManager.GetString("Heading3", _resourceCulture)!;

    public static string BulletList => ResourceManager.GetString("BulletList", _resourceCulture)!;

    public static string NumberedList => ResourceManager.GetString("NumberedList", _resourceCulture)!;

    public static string Quote => ResourceManager.GetString("Quote", _resourceCulture)!;

    public static string CodeBlock => ResourceManager.GetString("CodeBlock", _resourceCulture)!;

    public static string InsertTable => ResourceManager.GetString("InsertTable", _resourceCulture)!;

    public static string CopyCode => ResourceManager.GetString("CopyCode", _resourceCulture)!;

    public static string PlainText => ResourceManager.GetString("PlainText", _resourceCulture)!;

    public static string Words => ResourceManager.GetString("Words", _resourceCulture)!;

    public static string StartTypingHint => ResourceManager.GetString("StartTypingHint", _resourceCulture)!;

    public static string ImagePlaceholder => ResourceManager.GetString("ImagePlaceholder", _resourceCulture)!;

    public static string StatusCaret => ResourceManager.GetString("StatusCaret", _resourceCulture)!;

    public static string PlaceholderBoldText => ResourceManager.GetString("PlaceholderBoldText", _resourceCulture)!;

    public static string PlaceholderItalicText => ResourceManager.GetString("PlaceholderItalicText", _resourceCulture)!;

    public static string PlaceholderStrikeText => ResourceManager.GetString("PlaceholderStrikeText", _resourceCulture)!;

    public static string PlaceholderCodeText => ResourceManager.GetString("PlaceholderCodeText", _resourceCulture)!;

    public static string PlaceholderLinkText => ResourceManager.GetString("PlaceholderLinkText", _resourceCulture)!;
}
