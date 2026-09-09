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
        Assert.Equal("settings", viewModel.UrlPathSegment);
    }

    [Fact]
    public void SetAutoSave_WhenInvoked_UpdatesSettingsServiceAndState()
    {
        FakeSettingsService settingsService = new(true);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.SetAutoSave(false);

        Assert.False(settingsService.IsAutoSaveEnabled);
        Assert.False(viewModel.State.IsAutoSaveEnabled);
    }

    [Fact]
    public void ToggleAutoSave_WhenExecuted_InvertsCurrentSetting()
    {
        FakeSettingsService settingsService = new(true);
        TestScreen hostScreen = new();
        SettingsViewModel viewModel = new(hostScreen, settingsService);

        viewModel.ToggleAutoSaveCommand.Execute().Subscribe();

        Assert.False(settingsService.IsAutoSaveEnabled);
        Assert.False(viewModel.State.IsAutoSaveEnabled);

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

        public bool IsAutoSaveEnabled => _subject.Value;

        public IObservable<bool> AutoSaveEnabledObservable => _subject;

        public FakeSettingsService(bool initialAutoSave)
        {
            _subject = new BehaviorSubject<bool>(initialAutoSave);
        }

        public void SetAutoSaveEnabled(bool enabled)
        {
            _subject.OnNext(enabled);
        }
    }
}
