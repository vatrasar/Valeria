using System;
using System.Collections.Generic;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace NewMarkText.Src.Features.Editor.Domain.Services;

/// <summary>
/// TextMate grammar provider with a shared dark theme registry.
/// Invoked by the markdown preview builder for every code block.
/// </summary>
public sealed class CodeSyntaxService : ICodeSyntaxService
{
    private static readonly IReadOnlyDictionary<string, string> LanguageToExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["c#"] = ".cs",
            ["csharp"] = ".cs",
            ["cs"] = ".cs",
            ["dotnet"] = ".cs",
            ["python"] = ".py",
            ["py"] = ".py",
            ["javascript"] = ".js",
            ["js"] = ".js",
            ["jsx"] = ".js",
            ["typescript"] = ".ts",
            ["ts"] = ".ts",
            ["tsx"] = ".ts",
            ["html"] = ".html",
            ["xml"] = ".xml",
            ["axaml"] = ".xml",
            ["xaml"] = ".xml",
            ["json"] = ".json",
            ["css"] = ".css",
            ["scss"] = ".scss",
            ["less"] = ".less",
            ["yaml"] = ".yaml",
            ["yml"] = ".yaml",
            ["toml"] = ".toml",
            ["ini"] = ".ini",
            ["bash"] = ".sh",
            ["sh"] = ".sh",
            ["shell"] = ".sh",
            ["powershell"] = ".ps1",
            ["ps1"] = ".ps1",
            ["sql"] = ".sql",
            ["cpp"] = ".cpp",
            ["c++"] = ".cpp",
            ["c"] = ".c",
            ["h"] = ".h",
            ["java"] = ".java",
            ["kotlin"] = ".kt",
            ["rust"] = ".rs",
            ["go"] = ".go",
            ["php"] = ".php",
            ["ruby"] = ".rb",
            ["rb"] = ".rb",
            ["swift"] = ".swift",
            ["r"] = ".r",
            ["lua"] = ".lua",
            ["perl"] = ".pl",
            ["dockerfile"] = ".dockerfile",
            ["diff"] = ".diff",
            ["markdown"] = ".md",
            ["md"] = ".md",
            ["tex"] = ".tex",
            ["vb"] = ".vb",
            ["fsharp"] = ".fs",
            ["fs"] = ".fs"
        };

    private static readonly IReadOnlyDictionary<string, string> LanguageToDisplayName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["c#"] = "C#",
            ["csharp"] = "C#",
            ["cs"] = "C#",
            ["python"] = "Python",
            ["py"] = "Python",
            ["javascript"] = "JavaScript",
            ["js"] = "JavaScript",
            ["typescript"] = "TypeScript",
            ["ts"] = "TypeScript",
            ["html"] = "HTML",
            ["xml"] = "XML",
            ["axaml"] = "AXAML",
            ["xaml"] = "XAML",
            ["json"] = "JSON"
        };

    private readonly RegistryOptions _registryOptions = new(ThemeName.DarkPlus);

    /// <summary>
    /// Installs TextMate highlighting on the editor for the given fence language.
    /// Unknown languages keep the plain editor look. Used by the preview builder.
    /// </summary>
    public void ApplyGrammar(TextEditor editor, string? language)
    {
        var installation = editor.InstallTextMate(_registryOptions);

        if (!LanguageToExtension.TryGetValue(language ?? string.Empty, out string? extension))
            return;

        Language? grammarLanguage = _registryOptions.GetLanguageByExtension(extension);

        if (grammarLanguage is null)
            return;

        installation.SetGrammar(_registryOptions.GetScopeByLanguageId(grammarLanguage.Id));
    }

    /// <summary>
    /// Returns the short display name shown in the code block header.
    /// Used by the preview builder.
    /// </summary>
    public string GetLanguageDisplayName(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return Resources.EditorStrings.PlainText;

        if (LanguageToDisplayName.TryGetValue(language.Trim(), out string? displayName))
            return displayName;

        return language.Trim();
    }
}
