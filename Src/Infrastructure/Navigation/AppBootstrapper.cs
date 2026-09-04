using System;
using System.Collections.Generic;
using System.Linq;
using Splat;

namespace Valeria.Src.Infrastructure.Navigation;

/// <summary>
/// Discovers all feature modules in this assembly and lets them register routing views.
/// Invoked once during application startup.
/// </summary>
public static class AppBootstrapper
{
    public static void RegisterFeatureModules()
    {
        foreach (IFeatureModule module in DiscoverModules())
            module.Register(Locator.CurrentMutable);
    }

    private static IEnumerable<IFeatureModule> DiscoverModules()
    {
        return typeof(AppBootstrapper).Assembly
            .GetTypes()
            .Where(IsFeatureModule)
            .Select(CreateModule);
    }

    private static bool IsFeatureModule(Type type)
    {
        return typeof(IFeatureModule).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract;
    }

    private static IFeatureModule CreateModule(Type type)
    {
        return (IFeatureModule)Activator.CreateInstance(type)!;
    }
}
