using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Parses raw YAML frontmatter text into a structured <see cref="FrontMatterMetadata"/> model.
/// </summary>
public static class FrontMatterParser
{
    private static readonly HashSet<string> NameKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "title", "id", "skill"
    };

    private static readonly HashSet<string> DescriptionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "description", "summary", "desc"
    };

    /// <summary>
    /// Parses raw YAML text into structured metadata.
    /// </summary>
    public static FrontMatterMetadata Parse(string? rawYaml)
    {
        if (string.IsNullOrWhiteSpace(rawYaml))
            return new FrontMatterMetadata();

        string[] lines = rawYaml.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        List<RawEntry> entries = CollectEntries(lines);

        return BuildMetadata(entries);
    }

    /// <summary>
    /// Checks whether the raw frontmatter text represents valid YAML metadata rather than Markdown content.
    /// </summary>
    public static bool IsValid(string? rawYaml)
    {
        if (string.IsNullOrWhiteSpace(rawYaml))
            return false;

        string[] lines = rawYaml.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        bool hasAtLeastOneKey = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
                continue;

            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
                return false;

            if (trimmed.StartsWith("# ") || trimmed.StartsWith("## ") || trimmed.StartsWith("### ") ||
                trimmed.StartsWith("#### ") || trimmed.StartsWith("##### ") || trimmed.StartsWith("###### "))
                return false;

            if (IsNewKeyLine(line))
                hasAtLeastOneKey = true;
        }

        return hasAtLeastOneKey;
    }

    private static List<RawEntry> CollectEntries(string[] lines)
    {
        List<RawEntry> entries = [];
        RawEntry? currentEntry = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (IsNewKeyLine(line))
            {
                currentEntry = ProcessNewKeyLine(line, entries);
                continue;
            }

            if (currentEntry is not null)
                AppendContinuation(currentEntry, line);
        }

        return entries;
    }

    private static bool IsNewKeyLine(string line)
    {
        if (line.Length == 0 || char.IsWhiteSpace(line[0]) || !line.Contains(':'))
            return false;

        char first = line[0];
        return char.IsLetterOrDigit(first) || first == '_';
    }

    private static RawEntry ProcessNewKeyLine(string line, List<RawEntry> entries)
    {
        int colonIndex = line.IndexOf(':');
        string key = line.Substring(0, colonIndex).Trim();
        string value = line.Substring(colonIndex + 1).Trim();

        RawEntry entry = new(key);

        if (IsInlineList(value))
            PopulateInlineList(entry, value);
        else if (value.Length > 0)
            entry.Lines.Add(value);

        entries.Add(entry);
        return entry;
    }

    private static bool IsInlineList(string value)
    {
        return value.StartsWith('[') && value.EndsWith(']');
    }

    private static void PopulateInlineList(RawEntry entry, string value)
    {
        string inner = value.Substring(1, value.Length - 2);
        string[] parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = 0; i < parts.Length; i++)
            entry.ListItems.Add(CleanValue(parts[i]));
    }

    private static void AppendContinuation(RawEntry entry, string line)
    {
        string trimmed = line.TrimStart();

        if (trimmed.StartsWith("- "))
        {
            entry.ListItems.Add(CleanValue(trimmed.Substring(2).Trim()));
            return;
        }

        entry.Lines.Add(trimmed);
    }

    private static FrontMatterMetadata BuildMetadata(List<RawEntry> entries)
    {
        string? name = null;
        string? description = null;
        ImmutableList<KeyValuePair<string, bool>>.Builder flagsBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, bool>>();
        ImmutableList<KeyValuePair<string, ImmutableList<string>>>.Builder listsBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, ImmutableList<string>>>();
        ImmutableList<KeyValuePair<string, string>>.Builder customBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, string>>();

        for (int i = 0; i < entries.Count; i++)
            CategorizeEntry(entries[i], ref name, ref description, flagsBuilder, listsBuilder, customBuilder);

        return new FrontMatterMetadata
        {
            Name = name,
            Description = description,
            Flags = flagsBuilder.ToImmutable(),
            Lists = listsBuilder.ToImmutable(),
            CustomProperties = customBuilder.ToImmutable()
        };
    }

    private static void CategorizeEntry(
        RawEntry entry,
        ref string? name,
        ref string? description,
        ImmutableList<KeyValuePair<string, bool>>.Builder flags,
        ImmutableList<KeyValuePair<string, ImmutableList<string>>>.Builder lists,
        ImmutableList<KeyValuePair<string, string>>.Builder custom)
    {
        if (entry.ListItems.Count > 0)
        {
            lists.Add(new KeyValuePair<string, ImmutableList<string>>(entry.Key, entry.ListItems.ToImmutableList()));
            return;
        }

        string rawValue = CleanMultilineValue(string.Join(" ", entry.Lines).Trim());

        if (TryExtractBoolean(rawValue, out bool boolValue))
        {
            flags.Add(new KeyValuePair<string, bool>(entry.Key, boolValue));
            return;
        }

        if (name is null && NameKeys.Contains(entry.Key))
        {
            name = CleanValue(rawValue);
            return;
        }

        if (description is null && DescriptionKeys.Contains(entry.Key))
        {
            description = CleanValue(rawValue);
            return;
        }

        custom.Add(new KeyValuePair<string, string>(entry.Key, CleanValue(rawValue)));
    }

    private static string CleanMultilineValue(string value)
    {
        if (value.StartsWith('>') || value.StartsWith('|'))
            return value.Substring(1).Trim();

        return value;
    }

    private static bool TryExtractBoolean(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
            return true;

        if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static string CleanValue(string value)
    {
        string trimmed = value.Trim();

        if (trimmed.Length >= 2 && ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) || (trimmed.StartsWith('\'') && trimmed.EndsWith('\''))))
            return trimmed.Substring(1, trimmed.Length - 2).Trim();

        return trimmed;
    }

    private sealed class RawEntry
    {
        public string Key { get; }

        public List<string> Lines { get; } = [];

        public List<string> ListItems { get; } = [];

        public RawEntry(string key) => Key = key;
    }
}
