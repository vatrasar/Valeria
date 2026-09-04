using ReactiveUI;
using Splat;
using NewMarkText.Src.Features.Shell.UI.Screens.Main;
using NewMarkText.Src.Infrastructure.Navigation;

namespace NewMarkText.Src.Features.Shell;

/// <summary>
/// Registers the shell feature views for ReactiveUI routing.
/// Discovered automatically by AppBootstrapper.
/// </summary>
public sealed class ShellModule : IFeatureModule
{
    public void Register(IMutableDependencyResolver services)
    {
        services.Register(() => new MainWindow(), typeof(IViewFor<MainWindowViewModel>));
    }
}
