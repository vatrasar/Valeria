using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Services;

namespace Valeria.Src.Infrastructure.Services;

/// <summary>
/// Manages user application preferences, persisting changes to local application storage.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private readonly string _settingsFilePath;
    private readonly BehaviorSubject<bool> _autoSaveSubject;
    private readonly object _fileLock = new();

    public bool IsAutoSaveEnabled => _autoSaveSubject.Value;

    public IObservable<bool> AutoSaveEnabledObservable => _autoSaveSubject.AsObservable();

    public SettingsService(IOptions<AppConfig> config)
        : this(config, ResolveDefaultSettingsPath())
    {
    }

    public SettingsService(IOptions<AppConfig> config, string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        bool initialAutoSave = LoadAutoSavePreference(config.Value.Editor.AutoSave);
        _autoSaveSubject = new BehaviorSubject<bool>(initialAutoSave);
    }

    /// <summary>
    /// Updates the AutoSave setting and saves it to persistent storage.
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        if (_autoSaveSubject.Value == enabled)
            return;

        _autoSaveSubject.OnNext(enabled);
        _ = SavePreferencesAsync(enabled);
    }

    public void Dispose()
    {
        _autoSaveSubject.Dispose();
    }

    private static string ResolveDefaultSettingsPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDirectory = Path.Combine(localAppData, "valeria");

        return Path.Combine(appDirectory, "settings.json");
    }

    private bool LoadAutoSavePreference(bool fallback)
    {
        if (!File.Exists(_settingsFilePath))
            return fallback;

        try
        {
            lock (_fileLock)
            {
                string json = File.ReadAllText(_settingsFilePath);
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.TryGetProperty("AutoSave", out JsonElement element))
                    return element.GetBoolean();
            }
        }
        catch (Exception)
        {
            return fallback;
        }

        return fallback;
    }

    private Task SavePreferencesAsync(bool autoSave)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_fileLock)
                {
                    string? directory = Path.GetDirectoryName(_settingsFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var payload = new { AutoSave = autoSave };
                    string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_settingsFilePath, json);
                }
            }
            catch (Exception)
            {
            }
        });
    }
}
