using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Tokenizes inline code with TextMate grammars so the preview can render
/// colorized spans without allocating a text editor per block.
/// Also resolves the short display name shown in code block headers.
/// </summary>
public interface ICodeSyntaxService
{
    /// <summary>
    /// Returns the TextMate scope for the given fence language, or null when unknown.
    /// Used with the shared registry to fetch grammars.
    /// </summary>
    string? ResolveScope(string? language);

    /// <summary>
    /// Returns the short display name shown in the code block header.
    /// Invoked by the preview builder for every code block.
    /// </summary>
    string GetLanguageDisplayName(string? language);

    /// <summary>
    /// Tokenizes code into colored spans on a background thread. Uses the dark
    /// theme and per-block TextMate state. Cancellation aborts before the first
    /// tokenizing line and returns partial lines.
    /// Invoked by EditorViewModel during idle highlighting.
    /// </summary>
    Task<IReadOnlyList<HighlightedLine>> HighlightCodeAsync(string? language, string code, CancellationToken cancellationToken);

    /// <summary>
    /// Warm-up that preloads the most common grammars, so the first highlight
    /// pass does not pay one-time grammar compile cost.
    /// Invoked once by EditorViewModel at startup.
    /// </summary>
    Task PrewarmAsync(CancellationToken cancellationToken);
}