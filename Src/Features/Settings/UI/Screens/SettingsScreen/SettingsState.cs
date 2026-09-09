namespace Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;

/// <summary>
/// Immutable state of the settings screen.
/// </summary>
public sealed record SettingsState
{
    public bool IsAutoSaveEnabled { get; init; } = true;

    public int AutoSaveDelaySeconds { get; init; } = 10;

    public double EditorFontSize { get; init; } = 14;

    public int EditorTabWidth { get; init; } = 4;

    public bool HasUnsavedChanges { get; init; }

    public bool IsSavedFeedbackVisible { get; init; }
}
