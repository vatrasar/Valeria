using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewMarkText.Src.Core.Config;
using NewMarkText.Src.Features.Editor.Domain.Services;
using NewMarkText.Src.Features.Shell.UI.Screens.Main;
using NewMarkText.Src.Infrastructure.Services;

namespace NewMarkText.Src.Infrastructure;

/// <summary>
/// Composition root: registers configuration, services and shell.
/// Invoked once by App during startup.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNewMarkText(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppConfig>(configuration.GetSection(AppConfig.SectionName));

        services.AddSingleton<IEditorFileService, EditorFileService>();
        services.AddSingleton<ICodeSyntaxService, CodeSyntaxService>();
        services.AddSingleton<IMarkdownPreviewBuilder, MarkdownPreviewBuilder>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
