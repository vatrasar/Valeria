using Splat;

namespace NewMarkText.Src.Infrastructure.Navigation;

/// <summary>
/// Contract implemented by every feature module to register its views for routing.
/// Modules are discovered automatically by <see cref="AppBootstrapper"/>.
/// </summary>
public interface IFeatureModule
{
    void Register(IMutableDependencyResolver services);
}
