using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewMarkText.Src.Features.Shell.UI.Screens.Main;
using NewMarkText.Src.Infrastructure;
using NewMarkText.Src.Infrastructure.Navigation;

namespace NewMarkText;

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
            .AddNewMarkText(configuration)
            .BuildServiceProvider();

        AppBootstrapper.RegisterFeatureModules();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow window = _services.GetRequiredService<MainWindow>();
            window.DataContext = _services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
