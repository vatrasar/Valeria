using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.ReactiveUI;

[assembly: AvaloniaTestApplication(typeof(Valeria.Tests.TestApp))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerAssembly)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Valeria.Tests;

/// <summary>
/// Headless application bootstrapping the Avalonia platform for UI tests.
/// </summary>
public sealed class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .UseReactiveUI();
}
