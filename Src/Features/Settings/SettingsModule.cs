using ReactiveUI;
using Splat;
using Valeria.Src.Features.Settings.UI.Screens.SettingsScreen;
using Valeria.Src.Infrastructure.Navigation;

namespace Valeria.Src.Features.Settings;

/// <summary>
/// Registers the settings feature views for ReactiveUI routing.
/// Discovered automatically by AppBootstrapper.
/// </summary>
public sealed class SettingsModule : IFeatureModule
{
    public void Register(IMutableDependencyResolver services)
    {
        services.Register(() => new SettingsView(), typeof(IViewFor<SettingsViewModel>));
    }
}
