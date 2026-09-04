using System;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Valeria.Src.Core.Mvvm;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

namespace Valeria.Src.Features.Shell.UI.Screens.Main;

/// <summary>
/// Application shell view model and routing owner.
/// Holds the router and opens an editor screen for the startup file,
/// or an empty editor when the startup file path is empty.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IScreen
{
    private readonly IServiceProvider _services;
    private readonly string _initialFilePath;
    private bool _navigated;

    public RoutingState Router { get; } = new();

    public MainWindowViewModel(IServiceProvider services, string initialFilePath)
    {
        _services = services;
        _initialFilePath = initialFilePath;
    }

    /// <summary>
    /// Navigates to the editor screen once, passing the startup file path.
    /// Invoked by MainWindow code-behind on activation.
    /// </summary>
    public void NavigateToEditor()
    {
        if (_navigated)
            return;

        _navigated = true;

        EditorViewModel editor = ActivatorUtilities.CreateInstance<EditorViewModel>(_services, _initialFilePath, this);
        Router.Navigate.Execute(editor).Subscribe();
    }
}
