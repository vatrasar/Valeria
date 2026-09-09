using ReactiveUI;
using Splat;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Valeria.Src.Infrastructure.Navigation;

namespace Valeria.Src.Features.Editor;

/// <summary>
/// Registers the editor feature views for ReactiveUI routing.
/// Discovered automatically by AppBootstrapper.
/// </summary>
public sealed class EditorModule : IFeatureModule
{
    public void Register(IMutableDependencyResolver services)
    {
        services.RegisterLazySingleton(() => new EditorView(), typeof(IViewFor<EditorViewModel>));
    }
}
