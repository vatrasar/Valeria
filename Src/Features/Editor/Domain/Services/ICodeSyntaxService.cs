using AvaloniaEdit;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Applies TextMate grammars to AvaloniaEdit editors so code is highlighted
/// with a dark Visual Studio like theme. Shared by every preview code block.
/// </summary>
public interface ICodeSyntaxService
{
    void ApplyGrammar(TextEditor editor, string? language);

    string GetLanguageDisplayName(string? language);
}
