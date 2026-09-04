using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Valeria.Tests.TestApp))]

namespace Valeria.Tests;

/// <summary>
/// Minimal headless application bootstrapping the Avalonia platform for UI tests.
/// </summary>
public sealed class TestApp : Application
{
}
