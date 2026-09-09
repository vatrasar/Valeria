using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Valeria.Src.Core.Mvvm;
using Valeria.Src.Core.Services;

namespace Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;

/// <summary>
/// View model of the application settings screen.
/// Allows viewing and updating user configuration options like AutoSave.
/// </summary>
public partial class SettingsViewModel : ViewModelBase<SettingsState>, IRoutableViewModel
{
    private readonly ISettingsService _settingsService;

    public string? UrlPathSegment => "settings";

    public IScreen HostScreen { get; }

    public SettingsViewModel(IScreen hostScreen, ISettingsService settingsService)
        : base(new SettingsState { IsAutoSaveEnabled = settingsService.IsAutoSaveEnabled })
    {
        HostScreen = hostScreen;
        _settingsService = settingsService;

        _settingsService.AutoSaveEnabledObservable
            .DistinctUntilChanged()
            .Subscribe(enabled => UpdateState(state => state with { IsAutoSaveEnabled = enabled }))
            .DisposeWith(Disposables);
    }

    [ReactiveCommand]
    private void ToggleAutoSave()
    {
        _settingsService.SetAutoSaveEnabled(!State.IsAutoSaveEnabled);
    }

    [ReactiveCommand]
    private void NavigateBack()
    {
        if (HostScreen.Router.NavigationStack.Count > 1)
        {
            HostScreen.Router.NavigateBack.Execute().Subscribe();
        }
    }

    /// <summary>
    /// Updates the AutoSave setting through the underlying settings service.
    /// Invoked by SettingsView toggle controls.
    /// </summary>
    public void SetAutoSave(bool isEnabled)
    {
        _settingsService.SetAutoSaveEnabled(isEnabled);
    }
}
