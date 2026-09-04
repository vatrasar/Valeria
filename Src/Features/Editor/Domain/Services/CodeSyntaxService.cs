using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// TextMate grammar provider that tokenizes code into colored spans.
/// A single shared registry preloads common grammars so the first highlight
/// pass does not pay one-time compile cost. Invoked by EditorViewModel for
/// every preview code block during idle highlighting.
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

    private static readonly IReadOnlyList<string> PreloadExtensions =
        [".cs", ".py", ".js", ".ts", ".json", ".css", ".html", ".xml", ".yaml", ".cpp", ".c", ".java", ".sh", ".rs", ".go", ".sql"];

    private static readonly TimeSpan PerLineTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly RegistryOptions RegistryOptions = new(ThemeName.DarkPlus);

    private static readonly Registry Registry = new(RegistryOptions);

    private static readonly Dictionary<string, IGrammar> Grammars = new(StringComparer.Ordinal);

    private static readonly object TokenizeLock = new();

    private static Theme? _resolvedTheme;

    /// <summary>
    /// Returns the TextMate scope for the given fence language, or null when unknown.
    /// Used with the shared registry to fetch grammars.
    /// </summary>
    public string? ResolveScope(string? language)
    {
        if (!LanguageToExtension.TryGetValue(language ?? string.Empty, out string? extension))
            return null;

        return RegistryOptions.GetScopeByExtension(extension);
    }

    /// <summary>
    /// Returns the short display name shown in the code block header.
    /// Invoked by the preview builder.
    /// </summary>
    public string GetLanguageDisplayName(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return Resources.EditorStrings.PlainText;

        if (LanguageToDisplayName.TryGetValue(language.Trim(), out string? displayName))
            return displayName;

        return language.Trim();
    }

    /// <summary>
    /// Tokenizes code into colored spans on a background thread. Uses the dark
    /// theme and per-block grammar state. Cancellation aborts between lines.
    /// Invoked by EditorViewModel during idle highlighting.
    /// </summary>
    public Task<IReadOnlyList<HighlightedLine>> HighlightCodeAsync(string? language, string code, CancellationToken cancellationToken)
    {
        return Task.Run(() => HighlightCode(language, code, cancellationToken), CancellationToken.None);
    }

    /// <summary>
    /// Warm-up that preloads the most common grammars, so the first highlight
    /// pass does not pay one-time grammar compile cost.
    /// Invoked once by EditorViewModel at startup.
    /// </summary>
    public Task PrewarmAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                foreach (string extension in PreloadExtensions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string scope = RegistryOptions.GetScopeByExtension(extension);

                    if (!string.IsNullOrEmpty(scope))
                        LoadGrammar(scope);
                }
            }
            catch (Exception)
            {
            }
        }, CancellationToken.None);
    }

    private IReadOnlyList<HighlightedLine> HighlightCode(string? language, string code, CancellationToken cancellationToken)
    {
        string? scope = ResolveScope(language);

        string displayCode = code.TrimEnd('\n');
        string[] lines = displayCode.Replace("\r\n", "\n").Split('\n');

        ImmutableList<HighlightedLine>.Builder builder = ImmutableList.CreateBuilder<HighlightedLine>();
        IStateStack? state = null;

        lock (TokenizeLock)
        {
            IGrammar? grammar = string.IsNullOrEmpty(scope) ? null : LoadGrammar(scope);
            Theme theme = ResolveTheme();

            foreach (string rawLine in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

                if (grammar is null)
                {
                    builder.Add(CreatePlainLine(line));
                    continue;
                }

                ITokenizeLineResult result = grammar.TokenizeLine(line, state, PerLineTimeout);
                state = result.RuleStack;

                builder.Add(new HighlightedLine(ToSpans(line, result.Tokens, theme)));
            }
        }

        return builder.ToImmutable();
    }

    private static HighlightedLine CreatePlainLine(string line)
    {
        if (line.Length == 0)
            return new HighlightedLine(ImmutableList<HighlightedSpan>.Empty);

        return new HighlightedLine(ImmutableList.Create(new HighlightedSpan(line, null, false, false)));
    }

    private static ImmutableList<HighlightedSpan> ToSpans(string line, IReadOnlyList<IToken> tokens, Theme theme)
    {
        if (line.Length == 0)
            return ImmutableList<HighlightedSpan>.Empty;

        if (tokens.Count == 0)
            return ImmutableList.Create(new HighlightedSpan(line, null, false, false));

        ImmutableList<HighlightedSpan>.Builder spans = ImmutableList.CreateBuilder<HighlightedSpan>();
        int current = 0;

        foreach (IToken token in tokens)
        {
            int start = Math.Clamp(token.StartIndex, 0, line.Length);
            int end = Math.Clamp(token.EndIndex, start, line.Length);

            if (end <= start)
                continue;

            if (start > current)
                spans.Add(new HighlightedSpan(line[current..start], null, false, false));

            string text = line[start..end];
            spans.Add(CreateSpan(text, theme, token));
            current = end;
        }

        if (current < line.Length)
            spans.Add(new HighlightedSpan(line[current..], null, false, false));

        return spans.ToImmutable();
    }

    private static HighlightedSpan CreateSpan(string text, Theme theme, IToken token)
    {
        List<ThemeTrieElementRule> rules = theme.Match(token.Scopes);

        foreach (ThemeTrieElementRule rule in rules)
        {
            bool isBold = rule.fontStyle != FontStyle.NotSet && (rule.fontStyle & FontStyle.Bold) != 0;
            bool isItalic = rule.fontStyle != FontStyle.NotSet && (rule.fontStyle & FontStyle.Italic) != 0;
            string? foreground = ResolveForeground(theme, rule);

            if (foreground is null && !isBold && !isItalic)
                continue;

            return new HighlightedSpan(text, foreground, isBold, isItalic);
        }

        return new HighlightedSpan(text, null, false, false);
    }

    private static string? ResolveForeground(Theme theme, ThemeTrieElementRule rule)
    {
        if (rule.foreground == 0)
            return null;

        string color = theme.GetColor(rule.foreground);

        if (string.IsNullOrEmpty(color) || color == "transparent" || color == ":transparent")
            return null;

        return color;
    }

    private static Theme ResolveTheme()
    {
        _resolvedTheme ??= Registry.GetTheme();

        return _resolvedTheme;
    }

    private static IGrammar LoadGrammar(string scope)
    {
        if (Grammars.TryGetValue(scope, out IGrammar? cached))
            return cached;

        IGrammar grammar = Registry.LoadGrammar(scope);
        Grammars[scope] = grammar;

        return grammar;
    }
}