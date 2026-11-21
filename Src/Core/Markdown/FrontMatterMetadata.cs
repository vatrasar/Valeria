using System.Collections.Generic;
using System.Collections.Immutable;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Represents parsed YAML frontmatter metadata, structured for semantic display of AI skills or generic document properties.
/// </summary>
public sealed record FrontMatterMetadata
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public ImmutableList<KeyValuePair<string, bool>> Flags { get; init; } = ImmutableList<KeyValuePair<string, bool>>.Empty;

    public ImmutableList<KeyValuePair<string, ImmutableList<string>>> Lists { get; init; } = ImmutableList<KeyValuePair<string, ImmutableList<string>>>.Empty;

    public ImmutableList<KeyValuePair<string, string>> CustomProperties { get; init; } = ImmutableList<KeyValuePair<string, string>>.Empty;

    public bool HasSemanticHeader => !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(Description);

    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) &&
                           string.IsNullOrWhiteSpace(Description) &&
                           Flags.IsEmpty &&
                           Lists.IsEmpty &&
                           CustomProperties.IsEmpty;
}
