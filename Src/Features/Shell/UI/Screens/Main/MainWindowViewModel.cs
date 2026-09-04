using System;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Valeria.Src.Core.Mvvm;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

namespace Valeria.Src.Features.Shell.UI.Screens.Main;

/// <summary>
/// Application shell view model and routing owner.
/// Holds the router and opens the editor screen on startup.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IScreen
{
    private readonly IServiceProvider _services;
    private bool _navigated;

    public RoutingState Router { get; } = new();

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Navigates to the editor screen once.
    /// Invoked by MainWindow code-behind on activation.
    /// </summary>
    public void NavigateToEditor()
    {
        if (_navigated)
            return;

        _navigated = true;

        EditorViewModel editor = ActivatorUtilities.CreateInstance<EditorViewModel>(_services, this);
        Router.Navigate.Execute(editor).Subscribe();
    }
}
