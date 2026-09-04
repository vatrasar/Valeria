using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure;
using Valeria.Src.Infrastructure.Navigation;

namespace Valeria;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        _services = new ServiceCollection()
            .AddValeria(configuration)
            .BuildServiceProvider();

        AppBootstrapper.RegisterFeatureModules();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            OpenStartupWindows(desktop, _services!);

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Opens one editor window per startup file, or a single empty window
    /// when no files were passed on the command line.
    /// Invoked once during startup.
    /// </summary>
    private void OpenStartupWindows(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services)
    {
        string[] files = desktop.Args!.Where(IsExistingFile).ToArray();

        if (files.Length == 0)
        {
            desktop.MainWindow = CreateEditorWindow(services, string.Empty);
            return;
        }

        List<Window> windows = files.Select(path => CreateEditorWindow(services, path)).ToList();
        desktop.MainWindow = windows[0];

        foreach (Window window in windows.Skip(1))
            window.Show();
    }

    private Window CreateEditorWindow(IServiceProvider services, string filePath)
    {
        MainWindowViewModel viewModel = ActivatorUtilities.CreateInstance<MainWindowViewModel>(services, filePath);
        MainWindow window = services.GetRequiredService<MainWindow>();
        window.DataContext = viewModel;

        return window;
    }

    private static bool IsExistingFile(string argument)
    {
        return !argument.StartsWith("-") && File.Exists(argument);
    }
}
