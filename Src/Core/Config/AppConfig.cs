namespace Valeria.Src.Core.Config;

/// <summary>
/// Strongly-typed application configuration bound from appsettings.json.
/// </summary>
public sealed class AppConfig
{
    public const string SectionName = "Valeria";

    public EditorOptions Editor { get; set; } = new();

    public PreviewOptions Preview { get; set; } = new();
}

/// <summary>
/// Settings of the markdown source editor pane.
/// </summary>
public sealed class EditorOptions
{
    public double FontSize { get; set; } = 14;

    public int TabWidth { get; set; } = 4;

    public int PreviewDebounceMilliseconds { get; set; } = 350;
}

/// <summary>
/// Settings of the rendered markdown preview pane.
/// </summary>
public sealed class PreviewOptions
{
    public int MaxWidth { get; set; } = 860;

    public int CodeBlockMaxHeight { get; set; } = 420;
}
