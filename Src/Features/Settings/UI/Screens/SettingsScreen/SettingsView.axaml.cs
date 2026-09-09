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
/// Purpose: allows the user to configure global preferences such as AutoSave, font size, and tab width, with explicit saving.
/// Available functionalities: edit AutoSave toggle, configure AutoSave delay, configure editor font size and tab indentation width, save settings, navigate back to editor.
/// Key UI elements: BackButton, SaveSettingsButton, SavedNotificationLabel, AutoSaveToggleSwitch, AutoSaveDelayNumericUpDown, FontSizeNumericUpDown, TabWidthNumericUpDown.
/// Navigate From: EditorScreen.
/// Navigate To: EditorScreen (back navigation).
/// </summary>
public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    private bool _isSyncing;

    public SettingsView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.NavigateBackCommand, v => v.BackButton);
            this.BindCommand(ViewModel, vm => vm.SaveSettingsCommand, v => v.SaveSettingsButton);

            this.WhenAnyValue(view => view.ViewModel!.State.HasUnsavedChanges)
                .Subscribe(Observer.Create<bool>(hasChanges => SaveSettingsButton.IsEnabled = hasChanges))
                .DisposeWith(disposables);

            this.WhenAnyValue(view => view.ViewModel!.State.IsSavedFeedbackVisible)
                .Subscribe(Observer.Create<bool>(isSaved => SavedNotificationLabel.IsVisible = isSaved))
                .DisposeWith(disposables);

            BindAutoSaveToggle(disposables);
            BindAutoSaveDelay(disposables);
            BindFontSize(disposables);
            BindTabWidth(disposables);
        });
    }

    private void BindAutoSaveToggle(CompositeDisposable disposables)
    {
        this.WhenAnyValue(view => view.ViewModel!.State.IsAutoSaveEnabled)
            .Subscribe(Observer.Create<bool>(enabled =>
            {
                _isSyncing = true;
                AutoSaveToggleSwitch.IsChecked = enabled;
                _isSyncing = false;
            }))
            .DisposeWith(disposables);

        AutoSaveToggleSwitch.GetObservable(ToggleSwitch.IsCheckedProperty)
            .Where(_ => !_isSyncing && ViewModel is not null)
            .Subscribe(Observer.Create<bool?>(isChecked =>
            {
                if (isChecked.HasValue && isChecked.Value != ViewModel!.State.IsAutoSaveEnabled)
                    ViewModel.SetAutoSave(isChecked.Value);
            }))
            .DisposeWith(disposables);
    }

    private void BindAutoSaveDelay(CompositeDisposable disposables)
    {
        this.WhenAnyValue(view => view.ViewModel!.State.AutoSaveDelaySeconds)
            .Subscribe(Observer.Create<int>(delay =>
            {
                _isSyncing = true;
                AutoSaveDelayNumericUpDown.Value = delay;
                _isSyncing = false;
            }))
            .DisposeWith(disposables);

        AutoSaveDelayNumericUpDown.GetObservable(NumericUpDown.ValueProperty)
            .Where(_ => !_isSyncing && ViewModel is not null)
            .Subscribe(Observer.Create<decimal?>(value =>
            {
                if (value.HasValue && (int)value.Value != ViewModel!.State.AutoSaveDelaySeconds)
                    ViewModel.SetAutoSaveDelay((int)value.Value);
            }))
            .DisposeWith(disposables);
    }

    private void BindFontSize(CompositeDisposable disposables)
    {
        this.WhenAnyValue(view => view.ViewModel!.State.EditorFontSize)
            .Subscribe(Observer.Create<double>(size =>
            {
                _isSyncing = true;
                FontSizeNumericUpDown.Value = (decimal)size;
                _isSyncing = false;
            }))
            .DisposeWith(disposables);

        FontSizeNumericUpDown.GetObservable(NumericUpDown.ValueProperty)
            .Where(_ => !_isSyncing && ViewModel is not null)
            .Subscribe(Observer.Create<decimal?>(value =>
            {
                if (value.HasValue && Math.Abs((double)value.Value - ViewModel!.State.EditorFontSize) > 0.01)
                    ViewModel.SetFontSize((double)value.Value);
            }))
            .DisposeWith(disposables);
    }

    private void BindTabWidth(CompositeDisposable disposables)
    {
        this.WhenAnyValue(view => view.ViewModel!.State.EditorTabWidth)
            .Subscribe(Observer.Create<int>(width =>
            {
                _isSyncing = true;
                TabWidthNumericUpDown.Value = width;
                _isSyncing = false;
            }))
            .DisposeWith(disposables);

        TabWidthNumericUpDown.GetObservable(NumericUpDown.ValueProperty)
            .Where(_ => !_isSyncing && ViewModel is not null)
            .Subscribe(Observer.Create<decimal?>(value =>
            {
                if (value.HasValue && (int)value.Value != ViewModel!.State.EditorTabWidth)
                    ViewModel.SetTabWidth((int)value.Value);
            }))
            .DisposeWith(disposables);
    }
}
