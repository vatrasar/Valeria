using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(NewMarkText.Tests.TestApp))]

namespace NewMarkText.Tests;

/// <summary>
/// Minimal headless application bootstrapping the Avalonia platform for UI tests.
/// </summary>
public sealed class TestApp : Application
{
}
