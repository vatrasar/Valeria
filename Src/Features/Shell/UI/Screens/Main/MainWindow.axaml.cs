using System;
using System.Reactive;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using NewMarkText.Src.Features.Editor.UI.Screens.EditorScreen;
using NewMarkText.Src.Shared.Resources;
using ReactiveUI;

namespace NewMarkText.Src.Features.Shell.UI.Screens.Main;

/// <summary>
/// Application shell window: hosts the router outlet and tracks the document title.
/// Purpose: display the active routed screen and reflect the edited document name.
/// Available functionalities: routing outlet, window title tracking.
/// Key UI elements: MainRouter (RoutedViewHost).
/// Navigate From: application startup.
/// Navigate To: editor screen.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private IDisposable? _titleSubscription;

    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, viewModel => viewModel.Router, view => view.MainRouter.Router);

            this.WhenAnyValue(view => view.ViewModel!.Router.NavigationStack.Count)
                .Subscribe(Observer.Create<int>(_ => AttachTitleTracker()))
                .DisposeWith(disposables);

            Disposable.Create(DetachTitleTracker).DisposeWith(disposables);

            ViewModel?.NavigateToEditor();
        });
    }

    private void AttachTitleTracker()
    {
        DetachTitleTracker();

        if (ViewModel?.Router.GetCurrentViewModel() is EditorViewModel editor)
        {
            _titleSubscription = editor
                .WhenAnyValue(viewModel => viewModel.State.DocumentTitle)
                .Subscribe(Observer.Create<string>(RenderTitle));
        }
        else
        {
            RenderTitle(GlobalStrings.UntitledDocument);
        }
    }

    private void RenderTitle(string documentTitle)
    {
        Title = $"{documentTitle} - {GlobalStrings.AppTitle}";
    }

    private void DetachTitleTracker()
    {
        _titleSubscription?.Dispose();
        _titleSubscription = null;
    }
}
