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
    /// Gets an observable stream that emits when the AutoSave setting changes.
    /// </summary>
    IObservable<bool> AutoSaveEnabledObservable { get; }

    /// <summary>
    /// Updates the global AutoSave setting and persists it.
    /// </summary>
    void SetAutoSaveEnabled(bool enabled);
}
