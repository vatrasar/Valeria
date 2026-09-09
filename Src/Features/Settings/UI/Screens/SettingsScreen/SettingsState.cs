namespace Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;

/// <summary>
/// Immutable state of the settings screen.
/// </summary>
public sealed record SettingsState
{
    public bool IsAutoSaveEnabled { get; init; } = true;
}
