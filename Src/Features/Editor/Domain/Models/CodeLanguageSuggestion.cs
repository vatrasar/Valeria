namespace Valeria.Src.Features.Editor.Domain.Models;

/// <summary>
/// A supported Markdown code-fence language identifier and its human-readable name.
/// </summary>
public sealed record CodeLanguageSuggestion(string Identifier, string DisplayName);
