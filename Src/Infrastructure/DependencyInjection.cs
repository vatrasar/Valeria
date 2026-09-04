using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Valeria.Src.Core.Config;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure.Services;

namespace Valeria.Src.Infrastructure;

/// <summary>
/// Composition root: registers configuration, services and shell.
/// Invoked once by App during startup.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddValeria(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppConfig>(configuration.GetSection(AppConfig.SectionName));

        services.AddSingleton<IEditorFileService, EditorFileService>();
        services.AddSingleton<ICodeSyntaxService, CodeSyntaxService>();
        services.AddSingleton<IMarkdownPreviewBuilder, MarkdownPreviewBuilder>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        services.AddSingleton<MainWindowViewModel>(provider =>
            ActivatorUtilities.CreateInstance<MainWindowViewModel>(provider, string.Empty));

        services.AddTransient<MainWindow>();

        return services;
    }
}
