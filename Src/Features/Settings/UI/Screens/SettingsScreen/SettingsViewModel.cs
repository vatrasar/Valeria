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
        : base(new SettingsState
        {
            IsAutoSaveEnabled = settingsService.IsAutoSaveEnabled,
            AutoSaveDelaySeconds = settingsService.AutoSaveDelaySeconds,
            EditorFontSize = settingsService.EditorFontSize,
            EditorTabWidth = settingsService.EditorTabWidth,
            HasUnsavedChanges = false,
            IsSavedFeedbackVisible = false
        })
    {
        HostScreen = hostScreen;
        _settingsService = settingsService;
    }

    [ReactiveCommand]
    private void ToggleAutoSave()
    {
        SetAutoSave(!State.IsAutoSaveEnabled);
    }

    [ReactiveCommand]
    private void SaveSettings()
    {
        _settingsService.SetAutoSaveEnabled(State.IsAutoSaveEnabled);
        _settingsService.SetAutoSaveDelaySeconds(State.AutoSaveDelaySeconds);
        _settingsService.SetEditorFontSize(State.EditorFontSize);
        _settingsService.SetEditorTabWidth(State.EditorTabWidth);

        UpdateState(state => state with
        {
            HasUnsavedChanges = false,
            IsSavedFeedbackVisible = true
        });
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
    /// Updates the draft AutoSave setting in local view state.
    /// Invoked by SettingsView toggle controls.
    /// </summary>
    public void SetAutoSave(bool isEnabled)
    {
        if (State.IsAutoSaveEnabled == isEnabled)
            return;

        UpdateState(state => state with
        {
            IsAutoSaveEnabled = isEnabled,
            HasUnsavedChanges = true,
            IsSavedFeedbackVisible = false
        });
    }

    /// <summary>
    /// Updates the draft AutoSave delay in seconds in local view state.
    /// Invoked by SettingsView numeric input controls.
    /// </summary>
    public void SetAutoSaveDelay(int seconds)
    {
        if (State.AutoSaveDelaySeconds == seconds)
            return;

        UpdateState(state => state with
        {
            AutoSaveDelaySeconds = seconds,
            HasUnsavedChanges = true,
            IsSavedFeedbackVisible = false
        });
    }

    /// <summary>
    /// Updates the draft editor font size in local view state.
    /// Invoked by SettingsView numeric input controls.
    /// </summary>
    public void SetFontSize(double fontSize)
    {
        if (Math.Abs(State.EditorFontSize - fontSize) < 0.01)
            return;

        UpdateState(state => state with
        {
            EditorFontSize = fontSize,
            HasUnsavedChanges = true,
            IsSavedFeedbackVisible = false
        });
    }

    /// <summary>
    /// Updates the draft editor tab width in local view state.
    /// Invoked by SettingsView numeric input controls.
    /// </summary>
    public void SetTabWidth(int tabWidth)
    {
        if (State.EditorTabWidth == tabWidth)
            return;

        UpdateState(state => state with
        {
            EditorTabWidth = tabWidth,
            HasUnsavedChanges = true,
            IsSavedFeedbackVisible = false
        });
    }
}
