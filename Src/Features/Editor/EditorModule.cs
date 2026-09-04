using ReactiveUI;
using Splat;
using NewMarkText.Src.Features.Editor.UI.Screens.EditorScreen;
using NewMarkText.Src.Infrastructure.Navigation;

namespace NewMarkText.Src.Features.Editor;

/// <summary>
/// Registers the editor feature views for ReactiveUI routing.
/// Discovered automatically by AppBootstrapper.
/// </summary>
public sealed class EditorModule : IFeatureModule
{
    public void Register(IMutableDependencyResolver services)
    {
        services.Register(() => new EditorView(), typeof(IViewFor<EditorViewModel>));
    }
}
