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
}
