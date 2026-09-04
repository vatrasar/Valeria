using ReactiveUI;
using Splat;
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure.Navigation;

namespace Valeria.Src.Features.Shell;

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
