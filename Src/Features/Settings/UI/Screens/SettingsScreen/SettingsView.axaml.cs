using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;

/// <summary>
/// Settings screen: displays application-wide configuration options.
/// Purpose: allows the user to configure global preferences such as AutoSave.
/// Available functionalities: toggle AutoSave, navigate back to editor.
/// Key UI elements: BackButton, AutoSaveToggleSwitch.
/// Navigate From: EditorScreen.
/// Navigate To: EditorScreen (back navigation).
/// </summary>
public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    private bool _isSyncingToggle;

    public SettingsView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.NavigateBackCommand, v => v.BackButton);

            this.WhenAnyValue(view => view.ViewModel!.State.IsAutoSaveEnabled)
                .Subscribe(Observer.Create<bool>(enabled =>
                {
                    _isSyncingToggle = true;
                    AutoSaveToggleSwitch.IsChecked = enabled;
                    _isSyncingToggle = false;
                }))
                .DisposeWith(disposables);

            AutoSaveToggleSwitch.GetObservable(ToggleSwitch.IsCheckedProperty)
                .Where(_ => !_isSyncingToggle && ViewModel is not null)
                .Subscribe(Observer.Create<bool?>(isChecked =>
                {
                    if (isChecked.HasValue && isChecked.Value != ViewModel!.State.IsAutoSaveEnabled)
                        ViewModel.SetAutoSave(isChecked.Value);
                }))
                .DisposeWith(disposables);
        });
    }
}
