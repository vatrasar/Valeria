using System;
using System.Reactive;
using System.Reactive.Subjects;
using ReactiveUI;
using Valeria.Src.Core.Services;
using Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;
using Xunit;

namespace Valeria.Tests.FeaturesTests.SettingsTests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void State_WhenCreated_ReflectsSettingsServiceValue()
    {
        FakeSettingsService settingsService = new(true);
        TestScreen hostScreen = new();

        SettingsViewModel viewModel = new(hostScreen, settingsService);

        Assert.True(viewModel.State.IsAutoSaveEnabled);
        Assert.False(viewModel.State.HasUnsavedChanges);
        Assert.False(viewModel.State.IsSavedFeedbackVisible);
        Assert.Equal("settings", viewModel.UrlPathSegment);
    }

    [Fact]
    public void SetAutoSave_UpdatesDraftState_AndSaveSettingsPersistsToService()
    {
        FakeSettingsService settingsService = new(true);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.SetAutoSave(false);

        Assert.True(settingsService.IsAutoSaveEnabled);
        Assert.False(viewModel.State.IsAutoSaveEnabled);
        Assert.True(viewModel.State.HasUnsavedChanges);

        viewModel.SaveSettingsCommand.Execute().Subscribe();

        Assert.False(settingsService.IsAutoSaveEnabled);
        Assert.False(viewModel.State.HasUnsavedChanges);
        Assert.True(viewModel.State.IsSavedFeedbackVisible);
    }

    [Fact]
    public void ToggleAutoSave_WhenExecuted_InvertsDraftState()
    {
        FakeSettingsService settingsService = new(true);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.ToggleAutoSaveCommand.Execute().Subscribe();

        Assert.True(settingsService.IsAutoSaveEnabled);
        Assert.False(viewModel.State.IsAutoSaveEnabled);
        Assert.True(viewModel.State.HasUnsavedChanges);

        viewModel.ToggleAutoSaveCommand.Execute().Subscribe();

        Assert.True(settingsService.IsAutoSaveEnabled);
        Assert.True(viewModel.State.IsAutoSaveEnabled);
    }

    [Fact]
    public void NavigateBack_WhenExecuted_PopsNavigationStack()
    {
        FakeSettingsService settingsService = new(true);
        TestScreen hostScreen = new();
        DummyRoutableViewModel previousScreen = new(hostScreen);
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        hostScreen.Router.Navigate.Execute(previousScreen).Subscribe();
        hostScreen.Router.Navigate.Execute(viewModel).Subscribe();

        Assert.Equal(2, hostScreen.Router.NavigationStack.Count);
        Assert.Same(viewModel, hostScreen.Router.GetCurrentViewModel());

        viewModel.NavigateBackCommand.Execute().Subscribe();

        Assert.Single(hostScreen.Router.NavigationStack);
        Assert.Same(previousScreen, hostScreen.Router.GetCurrentViewModel());
    }

    [Fact]
    public void SetAutoSaveDelay_UpdatesDraftState_AndSaveSettingsPersistsToService()
    {
        FakeSettingsService settingsService = new(true, 10, 14, 4);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.SetAutoSaveDelay(20);

        Assert.Equal(10, settingsService.AutoSaveDelaySeconds);
        Assert.Equal(20, viewModel.State.AutoSaveDelaySeconds);
        Assert.True(viewModel.State.HasUnsavedChanges);

        viewModel.SaveSettingsCommand.Execute().Subscribe();

        Assert.Equal(20, settingsService.AutoSaveDelaySeconds);
        Assert.False(viewModel.State.HasUnsavedChanges);
        Assert.True(viewModel.State.IsSavedFeedbackVisible);
    }

    [Fact]
    public void SetFontSize_UpdatesDraftState_AndSaveSettingsPersistsToService()
    {
        FakeSettingsService settingsService = new(true, 10, 14, 4);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.SetFontSize(16);

        Assert.Equal(14, settingsService.EditorFontSize);
        Assert.Equal(16, viewModel.State.EditorFontSize);
        Assert.True(viewModel.State.HasUnsavedChanges);

        viewModel.SaveSettingsCommand.Execute().Subscribe();

        Assert.Equal(16, settingsService.EditorFontSize);
        Assert.False(viewModel.State.HasUnsavedChanges);
        Assert.True(viewModel.State.IsSavedFeedbackVisible);
    }

    [Fact]
    public void SetTabWidth_UpdatesDraftState_AndSaveSettingsPersistsToService()
    {
        FakeSettingsService settingsService = new(true, 10, 14, 4);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.SetTabWidth(8);

        Assert.Equal(4, settingsService.EditorTabWidth);
        Assert.Equal(8, viewModel.State.EditorTabWidth);
        Assert.True(viewModel.State.HasUnsavedChanges);

        viewModel.SaveSettingsCommand.Execute().Subscribe();

        Assert.Equal(8, settingsService.EditorTabWidth);
        Assert.False(viewModel.State.HasUnsavedChanges);
        Assert.True(viewModel.State.IsSavedFeedbackVisible);
    }

    [Fact]
    public void SetHeadingPalette_UpdatesDraftState_AndSaveSettingsPersistsToService()
    {
        FakeSettingsService settingsService = new(true, 10, 14, 4, Valeria.Src.Core.Domain.Enums.HeadingColorPalette.Cool);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.SetHeadingPalette(Valeria.Src.Core.Domain.Enums.HeadingColorPalette.Classic);

        Assert.Equal(Valeria.Src.Core.Domain.Enums.HeadingColorPalette.Cool, settingsService.HeadingPalette);
        Assert.Equal(Valeria.Src.Core.Domain.Enums.HeadingColorPalette.Classic, viewModel.State.HeadingPalette);
        Assert.True(viewModel.State.HasUnsavedChanges);

        viewModel.SaveSettingsCommand.Execute().Subscribe();

        Assert.Equal(Valeria.Src.Core.Domain.Enums.HeadingColorPalette.Classic, settingsService.HeadingPalette);
        Assert.False(viewModel.State.HasUnsavedChanges);
        Assert.True(viewModel.State.IsSavedFeedbackVisible);
    }

    [Fact]
    public void SetHeadingNumbering_UpdatesDraftState_AndSaveSettingsPersistsToService()
    {
        FakeSettingsService settingsService = new(true, initialHeadingNumbering: true);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.SetHeadingNumbering(false);

        Assert.True(settingsService.IsHeadingNumberingEnabled);
        Assert.False(viewModel.State.IsHeadingNumberingEnabled);
        Assert.True(viewModel.State.HasUnsavedChanges);

        viewModel.SaveSettingsCommand.Execute().Subscribe();

        Assert.False(settingsService.IsHeadingNumberingEnabled);
        Assert.False(viewModel.State.HasUnsavedChanges);
        Assert.True(viewModel.State.IsSavedFeedbackVisible);
    }

    private sealed class DummyRoutableViewModel : ReactiveObject, IRoutableViewModel
    {
        public string? UrlPathSegment => "dummy";
        public IScreen HostScreen { get; }

        public DummyRoutableViewModel(IScreen hostScreen)
        {
            HostScreen = hostScreen;
        }
    }

    private sealed class TestScreen : IScreen
    {
        public RoutingState Router { get; } = new();
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly BehaviorSubject<bool> _subject;
        private readonly BehaviorSubject<int> _delaySubject;
        private readonly BehaviorSubject<double> _fontSubject;
        private readonly BehaviorSubject<int> _tabSubject;
        private readonly BehaviorSubject<Valeria.Src.Core.Domain.Enums.HeadingColorPalette> _paletteSubject;
        private readonly BehaviorSubject<bool> _headingNumberingSubject;

        public bool IsAutoSaveEnabled => _subject.Value;
        public IObservable<bool> AutoSaveEnabledObservable => _subject;

        public int AutoSaveDelaySeconds => _delaySubject.Value;
        public IObservable<int> AutoSaveDelaySecondsObservable => _delaySubject;

        public double EditorFontSize => _fontSubject.Value;
        public IObservable<double> EditorFontSizeObservable => _fontSubject;

        public int EditorTabWidth => _tabSubject.Value;
        public IObservable<int> EditorTabWidthObservable => _tabSubject;

        public Valeria.Src.Core.Domain.Enums.HeadingColorPalette HeadingPalette => _paletteSubject.Value;
        public IObservable<Valeria.Src.Core.Domain.Enums.HeadingColorPalette> HeadingPaletteObservable => _paletteSubject;

        public bool IsHeadingNumberingEnabled => _headingNumberingSubject.Value;
        public IObservable<bool> HeadingNumberingEnabledObservable => _headingNumberingSubject;

        public FakeSettingsService(
            bool initialAutoSave,
            int initialDelay = 10,
            double initialFont = 14,
            int initialTab = 4,
            Valeria.Src.Core.Domain.Enums.HeadingColorPalette initialPalette = Valeria.Src.Core.Domain.Enums.HeadingColorPalette.Cool,
            bool initialHeadingNumbering = true)
        {
            _subject = new BehaviorSubject<bool>(initialAutoSave);
            _delaySubject = new BehaviorSubject<int>(initialDelay);
            _fontSubject = new BehaviorSubject<double>(initialFont);
            _tabSubject = new BehaviorSubject<int>(initialTab);
            _paletteSubject = new BehaviorSubject<Valeria.Src.Core.Domain.Enums.HeadingColorPalette>(initialPalette);
            _headingNumberingSubject = new BehaviorSubject<bool>(initialHeadingNumbering);
        }

        public void SetAutoSaveEnabled(bool enabled) => _subject.OnNext(enabled);

        public void SetAutoSaveDelaySeconds(int delaySeconds) => _delaySubject.OnNext(delaySeconds);

        public void SetEditorFontSize(double fontSize) => _fontSubject.OnNext(fontSize);

        public void SetEditorTabWidth(int tabWidth) => _tabSubject.OnNext(tabWidth);

        public void SetHeadingPalette(Valeria.Src.Core.Domain.Enums.HeadingColorPalette palette) => _paletteSubject.OnNext(palette);

        public void SetHeadingNumberingEnabled(bool enabled) => _headingNumberingSubject.OnNext(enabled);
    }
}
