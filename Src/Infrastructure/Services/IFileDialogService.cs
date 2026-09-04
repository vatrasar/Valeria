using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Valeria.Src.Infrastructure.Services;

/// <summary>
/// Opens native file dialogs anchored to the current window.
/// The top level provider is set by the active view because view models cannot access visuals.
/// </summary>
public interface IFileDialogService
{
    void SetTopLevelProvider(Func<TopLevel?> provider);

    Task<string?> PickMarkdownFileToOpenAsync(CancellationToken cancellationToken);

    Task<string?> PickMarkdownFileToSaveAsync(string? suggestedFileName, CancellationToken cancellationToken);
}
