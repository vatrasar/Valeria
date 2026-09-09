using System;

namespace Valeria.Src.Core.Services;

/// <summary>
/// Application settings service contract for managing user preferences globally.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets whether automatic saving of edited files is enabled.
    /// </summary>
    bool IsAutoSaveEnabled { get; }

    /// <summary>
    /// Gets the delay in seconds before triggering an automatic save.
    /// </summary>
    int AutoSaveDelaySeconds { get; }

    /// <summary>
    /// Gets the font size of the markdown source editor.
    /// </summary>
    double EditorFontSize { get; }

    /// <summary>
    /// Gets the tab indentation width in spaces.
    /// </summary>
    int EditorTabWidth { get; }

    /// <summary>
    /// Gets an observable stream that emits when the AutoSave setting changes.
    /// </summary>
    IObservable<bool> AutoSaveEnabledObservable { get; }

    /// <summary>
    /// Gets an observable stream that emits when the AutoSave delay setting changes.
    /// </summary>
    IObservable<int> AutoSaveDelaySecondsObservable { get; }

    /// <summary>
    /// Gets an observable stream that emits when the editor font size changes.
    /// </summary>
    IObservable<double> EditorFontSizeObservable { get; }

    /// <summary>
    /// Gets an observable stream that emits when the editor tab width changes.
    /// </summary>
    IObservable<int> EditorTabWidthObservable { get; }

    /// <summary>
    /// Updates the global AutoSave setting and persists it.
    /// </summary>
    void SetAutoSaveEnabled(bool enabled);

    /// <summary>
    /// Updates the AutoSave delay in seconds and persists it.
    /// </summary>
    void SetAutoSaveDelaySeconds(int seconds);

    /// <summary>
    /// Updates the markdown editor font size and persists it.
    /// </summary>
    void SetEditorFontSize(double fontSize);

    /// <summary>
    /// Updates the markdown editor tab indentation width and persists it.
    /// </summary>
    void SetEditorTabWidth(int tabWidth);
}
