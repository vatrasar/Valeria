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
    private readonly BehaviorSubject<int> _autoSaveDelaySecondsSubject;
    private readonly BehaviorSubject<double> _editorFontSizeSubject;
    private readonly BehaviorSubject<int> _editorTabWidthSubject;
    private readonly BehaviorSubject<Valeria.Src.Core.Domain.Enums.HeadingColorPalette> _headingPaletteSubject;
    private readonly object _fileLock = new();

    public bool IsAutoSaveEnabled => _autoSaveSubject.Value;

    public int AutoSaveDelaySeconds => _autoSaveDelaySecondsSubject.Value;

    public double EditorFontSize => _editorFontSizeSubject.Value;

    public int EditorTabWidth => _editorTabWidthSubject.Value;

    public Valeria.Src.Core.Domain.Enums.HeadingColorPalette HeadingPalette => _headingPaletteSubject.Value;

    public IObservable<bool> AutoSaveEnabledObservable => _autoSaveSubject.AsObservable();

    public IObservable<int> AutoSaveDelaySecondsObservable => _autoSaveDelaySecondsSubject.AsObservable();

    public IObservable<double> EditorFontSizeObservable => _editorFontSizeSubject.AsObservable();

    public IObservable<int> EditorTabWidthObservable => _editorTabWidthSubject.AsObservable();

    public IObservable<Valeria.Src.Core.Domain.Enums.HeadingColorPalette> HeadingPaletteObservable => _headingPaletteSubject.AsObservable();

    public SettingsService(IOptions<AppConfig> config)
        : this(config, ResolveDefaultSettingsPath())
    {
    }

    public SettingsService(IOptions<AppConfig> config, string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;

        (bool autoSave, int delay, double fontSize, int tabWidth, Valeria.Src.Core.Domain.Enums.HeadingColorPalette palette) =
            LoadPreferences(config.Value.Editor, config.Value.Preview);

        _autoSaveSubject = new BehaviorSubject<bool>(autoSave);
        _autoSaveDelaySecondsSubject = new BehaviorSubject<int>(delay);
        _editorFontSizeSubject = new BehaviorSubject<double>(fontSize);
        _editorTabWidthSubject = new BehaviorSubject<int>(tabWidth);
        _headingPaletteSubject = new BehaviorSubject<Valeria.Src.Core.Domain.Enums.HeadingColorPalette>(palette);
    }

    /// <summary>
    /// Updates the AutoSave setting and saves it to persistent storage.
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        if (_autoSaveSubject.Value == enabled)
            return;

        _autoSaveSubject.OnNext(enabled);
        _ = SavePreferencesAsync();
    }

    /// <summary>
    /// Updates the AutoSave delay in seconds and saves it to persistent storage.
    /// </summary>
    public void SetAutoSaveDelaySeconds(int seconds)
    {
        int clamped = Math.Clamp(seconds, 1, 60);

        if (_autoSaveDelaySecondsSubject.Value == clamped)
            return;

        _autoSaveDelaySecondsSubject.OnNext(clamped);
        _ = SavePreferencesAsync();
    }

    /// <summary>
    /// Updates the markdown editor font size and saves it to persistent storage.
    /// </summary>
    public void SetEditorFontSize(double fontSize)
    {
        double clamped = Math.Clamp(fontSize, 10, 32);

        if (Math.Abs(_editorFontSizeSubject.Value - clamped) < 0.01)
            return;

        _editorFontSizeSubject.OnNext(clamped);
        _ = SavePreferencesAsync();
    }

    /// <summary>
    /// Updates the markdown editor tab indentation width and saves it to persistent storage.
    /// </summary>
    public void SetEditorTabWidth(int tabWidth)
    {
        int clamped = Math.Clamp(tabWidth, 2, 8);

        if (_editorTabWidthSubject.Value == clamped)
            return;

        _editorTabWidthSubject.OnNext(clamped);
        _ = SavePreferencesAsync();
    }

    /// <summary>
    /// Updates the heading color palette and saves it to persistent storage.
    /// </summary>
    public void SetHeadingPalette(Valeria.Src.Core.Domain.Enums.HeadingColorPalette palette)
    {
        if (_headingPaletteSubject.Value == palette)
            return;

        _headingPaletteSubject.OnNext(palette);
        _ = SavePreferencesAsync();
    }

    public void Dispose()
    {
        _autoSaveSubject.Dispose();
        _autoSaveDelaySecondsSubject.Dispose();
        _editorFontSizeSubject.Dispose();
        _editorTabWidthSubject.Dispose();
        _headingPaletteSubject.Dispose();
    }

    private static string ResolveDefaultSettingsPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDirectory = Path.Combine(localAppData, "valeria");

        return Path.Combine(appDirectory, "settings.json");
    }

    private (bool AutoSave, int Delay, double FontSize, int TabWidth, Valeria.Src.Core.Domain.Enums.HeadingColorPalette HeadingPalette)
        LoadPreferences(EditorOptions editorFallback, PreviewOptions previewFallback)
    {
        bool autoSave = editorFallback.AutoSave;
        int delay = Math.Max(1, editorFallback.AutoSaveDelayMilliseconds / 1000);
        double fontSize = editorFallback.FontSize;
        int tabWidth = editorFallback.TabWidth;
        Valeria.Src.Core.Domain.Enums.HeadingColorPalette palette = previewFallback.HeadingPalette;

        if (!File.Exists(_settingsFilePath))
            return (autoSave, delay, fontSize, tabWidth, palette);

        try
        {
            lock (_fileLock)
            {
                string json = File.ReadAllText(_settingsFilePath);
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (root.TryGetProperty("AutoSave", out JsonElement autoSaveElement))
                    autoSave = autoSaveElement.GetBoolean();

                if (root.TryGetProperty("AutoSaveDelaySeconds", out JsonElement delayElement))
                    delay = Math.Clamp(delayElement.GetInt32(), 1, 60);

                if (root.TryGetProperty("EditorFontSize", out JsonElement fontElement))
                    fontSize = Math.Clamp(fontElement.GetDouble(), 10, 32);

                if (root.TryGetProperty("EditorTabWidth", out JsonElement tabElement))
                    tabWidth = Math.Clamp(tabElement.GetInt32(), 2, 8);

                if (root.TryGetProperty("HeadingPalette", out JsonElement paletteElement))
                    palette = ParseHeadingPalette(paletteElement, palette);
            }
        }
        catch (Exception)
        {
            return (autoSave, delay, fontSize, tabWidth, palette);
        }

        return (autoSave, delay, fontSize, tabWidth, palette);
    }

    private static Valeria.Src.Core.Domain.Enums.HeadingColorPalette ParseHeadingPalette(
        JsonElement element,
        Valeria.Src.Core.Domain.Enums.HeadingColorPalette fallback)
    {
        if (element.ValueKind == JsonValueKind.String &&
            Enum.TryParse<Valeria.Src.Core.Domain.Enums.HeadingColorPalette>(element.GetString(), true, out var parsed))
        {
            return parsed;
        }

        if (element.ValueKind == JsonValueKind.Number &&
            Enum.IsDefined(typeof(Valeria.Src.Core.Domain.Enums.HeadingColorPalette), element.GetInt32()))
        {
            return (Valeria.Src.Core.Domain.Enums.HeadingColorPalette)element.GetInt32();
        }

        return fallback;
    }

    private Task SavePreferencesAsync()
    {
        bool autoSave = _autoSaveSubject.Value;
        int delay = _autoSaveDelaySecondsSubject.Value;
        double fontSize = _editorFontSizeSubject.Value;
        int tabWidth = _editorTabWidthSubject.Value;
        string palette = _headingPaletteSubject.Value.ToString();

        return Task.Run(() =>
        {
            try
            {
                lock (_fileLock)
                {
                    string? directory = Path.GetDirectoryName(_settingsFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var payload = new
                    {
                        AutoSave = autoSave,
                        AutoSaveDelaySeconds = delay,
                        EditorFontSize = fontSize,
                        EditorTabWidth = tabWidth,
                        HeadingPalette = palette
                    };

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
