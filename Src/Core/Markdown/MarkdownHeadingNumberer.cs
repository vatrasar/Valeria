using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Core.Markdown;

/// <summary>
/// Applies LaTeX-style hierarchical section numbers (1., 1.1, 1.1.1, etc.) to markdown headings for preview rendering.
/// </summary>
public static class MarkdownHeadingNumberer
{
    /// <summary>
    /// Applies LaTeX-style numbering to all heading blocks in a document.
    /// Used by MarkdownParser and MarkdownPreviewBuilder.
    /// </summary>
    public static ImmutableList<MarkdownBlock> ApplyNumbering(ImmutableList<MarkdownBlock> blocks)
    {
        HeadingNumberTracker tracker = new();
        return ApplyNumbering(blocks, tracker);
    }

    /// <summary>
    /// Applies LaTeX-style numbering to all heading blocks using an existing tracker.
    /// Used for continuous numbering across document chunks.
    /// </summary>
    public static ImmutableList<MarkdownBlock> ApplyNumbering(
        ImmutableList<MarkdownBlock> blocks,
        HeadingNumberTracker tracker)
    {
        ImmutableList<MarkdownBlock>.Builder builder = ImmutableList.CreateBuilder<MarkdownBlock>();

        for (int i = 0; i < blocks.Count; i++)
            builder.Add(NumberBlock(blocks[i], tracker));

        return builder.ToImmutable();
    }

    /// <summary>
    /// Applies continuous LaTeX-style numbering across all chunks in a virtualized document.
    /// Used by MarkdownParallelParser.
    /// </summary>
    public static ImmutableList<MarkdownChunk> ApplyNumberingToChunks(IReadOnlyList<MarkdownChunk> chunks)
    {
        HeadingNumberTracker tracker = new();
        ImmutableList<MarkdownChunk>.Builder builder = ImmutableList.CreateBuilder<MarkdownChunk>();

        for (int i = 0; i < chunks.Count; i++)
        {
            MarkdownChunk chunk = chunks[i];
            ImmutableList<MarkdownBlock> numberedBlocks = ApplyNumbering(chunk.Blocks, tracker);
            builder.Add(chunk with { Blocks = numberedBlocks });
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Checks whether any heading section numbers differ between old and new block collections.
    /// Used by EditorPreviewCoordinator to trigger re-rendering of virtualized chunks.
    /// </summary>
    public static bool HeadingNumbersChanged(
        ImmutableList<MarkdownBlock> oldBlocks,
        ImmutableList<MarkdownBlock> newBlocks)
    {
        List<string?> oldNumbers = CollectSectionNumbers(oldBlocks);
        List<string?> newNumbers = CollectSectionNumbers(newBlocks);

        if (oldNumbers.Count != newNumbers.Count)
            return true;

        for (int i = 0; i < oldNumbers.Count; i++)
        {
            if (!string.Equals(oldNumbers[i], newNumbers[i], StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static MarkdownBlock NumberBlock(MarkdownBlock block, HeadingNumberTracker tracker)
    {
        return block switch
        {
            HeadingBlock heading => heading with { SectionNumber = tracker.GetNextNumber(heading.Level) },
            QuoteBlock quote => quote with
            {
                Blocks = ApplyNumbering(quote.Blocks, tracker)
            },
            BulletListBlock bullet => bullet with
            {
                Items = bullet.Items.Select(item => NumberListItem(item, tracker)).ToImmutableList()
            },
            OrderedListBlock ordered => ordered with
            {
                Items = ordered.Items.Select(item => NumberListItem(item, tracker)).ToImmutableList()
            },
            _ => block
        };
    }

    private static ListItemBlock NumberListItem(ListItemBlock item, HeadingNumberTracker tracker)
    {
        return item with
        {
            Blocks = ApplyNumbering(item.Blocks, tracker)
        };
    }

    private static List<string?> CollectSectionNumbers(ImmutableList<MarkdownBlock> blocks)
    {
        List<string?> numbers = [];
        AppendSectionNumbers(blocks, numbers);
        return numbers;
    }

    private static void AppendSectionNumbers(ImmutableList<MarkdownBlock> blocks, List<string?> numbers)
    {
        for (int i = 0; i < blocks.Count; i++)
            ExtractSectionNumbers(blocks[i], numbers);
    }

    private static void ExtractSectionNumbers(MarkdownBlock block, List<string?> numbers)
    {
        switch (block)
        {
            case HeadingBlock heading:
                numbers.Add(heading.SectionNumber);
                break;
            case QuoteBlock quote:
                AppendSectionNumbers(quote.Blocks, numbers);
                break;
            case BulletListBlock bullet:
                AppendListSectionNumbers(bullet.Items, numbers);
                break;
            case OrderedListBlock ordered:
                AppendListSectionNumbers(ordered.Items, numbers);
                break;
        }
    }

    private static void AppendListSectionNumbers(ImmutableList<ListItemBlock> items, List<string?> numbers)
    {
        for (int i = 0; i < items.Count; i++)
            AppendSectionNumbers(items[i].Blocks, numbers);
    }
}

/// <summary>
/// Maintains counters for headings of level 2-6 according to LaTeX hierarchical sectioning rules.
/// Level 1 headings (#) represent document titles and are not numbered.
/// </summary>
public sealed class HeadingNumberTracker
{
    private readonly int[] _counters = new int[6];

    /// <summary>
    /// Computes the next formatted section number for the specified heading level.
    /// Level 1 headings return null as they are considered unnumbered titles.
    /// </summary>
    public string? GetNextNumber(int level)
    {
        if (level <= 1)
            return null;

        int clampedLevel = Math.Clamp(level, 2, 6);
        int sectionLevel = clampedLevel - 1;

        EnsureParentLevelsInitialized(sectionLevel);
        _counters[sectionLevel]++;
        ResetDeeperLevels(sectionLevel);

        return FormatNumber(sectionLevel);
    }

    private void EnsureParentLevelsInitialized(int sectionLevel)
    {
        for (int i = 1; i < sectionLevel; i++)
        {
            if (_counters[i] == 0)
                _counters[i] = 1;
        }
    }

    private void ResetDeeperLevels(int sectionLevel)
    {
        for (int i = sectionLevel + 1; i <= 5; i++)
            _counters[i] = 0;
    }

    private string FormatNumber(int sectionLevel)
    {
        if (sectionLevel == 1)
            return $"{_counters[1]}.";

        return string.Join(".", _counters.AsSpan(1, sectionLevel).ToArray());
    }
}
