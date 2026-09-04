using System.Threading;
using System.Threading.Tasks;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Reads and writes markdown text files with cancellation support.
/// </summary>
public interface IEditorFileService
{
    Task<string> ReadTextAsync(string path, CancellationToken cancellationToken);

    Task WriteTextAsync(string path, string content, CancellationToken cancellationToken);

    Task<string> LoadWelcomeDocumentAsync(CancellationToken cancellationToken);
}
