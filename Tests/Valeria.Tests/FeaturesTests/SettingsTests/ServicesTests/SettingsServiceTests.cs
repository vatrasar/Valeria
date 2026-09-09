using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Valeria.Src.Core.Config;
using Valeria.Src.Infrastructure.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.SettingsTests.ServicesTests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempSettingsPath;

    public SettingsServiceTests()
    {
        _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"valeria_settings_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempSettingsPath))
            File.Delete(_tempSettingsPath);
    }

    [Fact]
    public void IsAutoSaveEnabled_ByDefaultWithoutSavedFile_ReturnsTrue()
    {
        IOptions<AppConfig> config = Options.Create(new AppConfig
        {
            Editor = new EditorOptions { AutoSave = true }
        });

        using SettingsService service = new(config, _tempSettingsPath);

        Assert.True(service.IsAutoSaveEnabled);
    }

    [Fact]
    public async Task SetAutoSaveEnabled_WhenDisabled_PersistsAndEmitsFalse()
    {
        IOptions<AppConfig> config = Options.Create(new AppConfig());
        using SettingsService service = new(config, _tempSettingsPath);

        bool latestValue = true;
        using var subscription = service.AutoSaveEnabledObservable.Subscribe(value => latestValue = value);

        service.SetAutoSaveEnabled(false);

        Assert.False(service.IsAutoSaveEnabled);
        Assert.False(latestValue);

        await Task.Delay(100);

        using SettingsService reloadedService = new(config, _tempSettingsPath);
        Assert.False(reloadedService.IsAutoSaveEnabled);
    }

    [Fact]
    public async Task SetAutoSaveEnabled_WhenToggledBackToTrue_PersistsAndEmitsTrue()
    {
        IOptions<AppConfig> config = Options.Create(new AppConfig());
        using SettingsService service = new(config, _tempSettingsPath);

        service.SetAutoSaveEnabled(false);
        await Task.Delay(100);

        service.SetAutoSaveEnabled(true);
        Assert.True(service.IsAutoSaveEnabled);

        await Task.Delay(100);

        using SettingsService reloadedService = new(config, _tempSettingsPath);
        Assert.True(reloadedService.IsAutoSaveEnabled);
    }

    [Fact]
    public async Task SetAutoSaveDelaySeconds_WhenUpdated_PersistsAndEmitsNewValue()
    {
        IOptions<AppConfig> config = Options.Create(new AppConfig
        {
            Editor = new EditorOptions { AutoSaveDelayMilliseconds = 10000 }
        });
        using SettingsService service = new(config, _tempSettingsPath);

        int latestValue = 10;
        using var subscription = service.AutoSaveDelaySecondsObservable.Subscribe(value => latestValue = value);

        service.SetAutoSaveDelaySeconds(15);

        Assert.Equal(15, service.AutoSaveDelaySeconds);
        Assert.Equal(15, latestValue);

        await Task.Delay(100);

        using SettingsService reloadedService = new(config, _tempSettingsPath);
        Assert.Equal(15, reloadedService.AutoSaveDelaySeconds);
    }

    [Fact]
    public async Task SetEditorFontSize_WhenUpdated_PersistsAndEmitsNewValue()
    {
        IOptions<AppConfig> config = Options.Create(new AppConfig
        {
            Editor = new EditorOptions { FontSize = 14 }
        });
        using SettingsService service = new(config, _tempSettingsPath);

        double latestValue = 14;
        using var subscription = service.EditorFontSizeObservable.Subscribe(value => latestValue = value);

        service.SetEditorFontSize(18);

        Assert.Equal(18, service.EditorFontSize);
        Assert.Equal(18, latestValue);

        await Task.Delay(100);

        using SettingsService reloadedService = new(config, _tempSettingsPath);
        Assert.Equal(18, reloadedService.EditorFontSize);
    }

    [Fact]
    public async Task SetEditorTabWidth_WhenUpdated_PersistsAndEmitsNewValue()
    {
        IOptions<AppConfig> config = Options.Create(new AppConfig
        {
            Editor = new EditorOptions { TabWidth = 4 }
        });
        using SettingsService service = new(config, _tempSettingsPath);

        int latestValue = 4;
        using var subscription = service.EditorTabWidthObservable.Subscribe(value => latestValue = value);

        service.SetEditorTabWidth(2);

        Assert.Equal(2, service.EditorTabWidth);
        Assert.Equal(2, latestValue);

        await Task.Delay(100);

        using SettingsService reloadedService = new(config, _tempSettingsPath);
        Assert.Equal(2, reloadedService.EditorTabWidth);
    }
}
