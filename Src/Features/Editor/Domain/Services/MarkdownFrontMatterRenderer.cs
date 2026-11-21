using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Resources;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Renders YAML frontmatter blocks into a semantic AI skill header card or properties inspector.
/// </summary>
internal sealed class MarkdownFrontMatterRenderer
{
    /// <summary>
    /// Builds a styled Avalonia control tree for a frontmatter block.
    /// </summary>
    public Control BuildFrontMatter(FrontMatterBlock frontMatter, ThemeResources theme)
    {
        FrontMatterMetadata metadata = frontMatter.Metadata;

        if (metadata.IsEmpty)
            return new Control();

        Border card = CreateCardContainer(theme);
        StackPanel rootPanel = new() { Spacing = 10 };

        if (metadata.HasSemanticHeader)
            PopulateSemanticCard(rootPanel, metadata, theme);
        else
            PopulateFallbackCard(rootPanel, metadata, theme);

        card.Child = rootPanel;
        return card;
    }

    private static Border CreateCardContainer(ThemeResources theme)
    {
        return new Border
        {
            Background = theme.CodeBlockBackground,
            BorderBrush = theme.CodeBlockBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 4, 0, 16),
            Padding = new Thickness(16, 12),
            ClipToBounds = true
        };
    }

    private static void PopulateSemanticCard(StackPanel rootPanel, FrontMatterMetadata metadata, ThemeResources theme)
    {
        Grid headerGrid = CreateHeaderGrid(metadata, theme);
        rootPanel.Children.Add(headerGrid);

        if (!string.IsNullOrWhiteSpace(metadata.Description))
            rootPanel.Children.Add(CreateDescriptionBlock(metadata.Description, theme));

        if (!metadata.Lists.IsEmpty)
            rootPanel.Children.Add(CreateListsPanel(metadata.Lists, theme));

        if (!metadata.CustomProperties.IsEmpty)
            rootPanel.Children.Add(CreateAdditionalPropertiesExpander(metadata.CustomProperties, theme));
    }

    private static void PopulateFallbackCard(StackPanel rootPanel, FrontMatterMetadata metadata, ThemeResources theme)
    {
        StackPanel header = CreateFallbackHeader(theme);
        rootPanel.Children.Add(header);

        List<KeyValuePair<string, string>> allProperties = CollectAllProperties(metadata);
        if (allProperties.Count > 0)
            rootPanel.Children.Add(BuildPropertiesGrid(allProperties, theme));
    }

    private static Grid CreateHeaderGrid(FrontMatterMetadata metadata, ThemeResources theme)
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        Control titleSection = CreateTitleSection(metadata.Name, theme);
        Grid.SetColumn(titleSection, 0);
        grid.Children.Add(titleSection);

        if (!metadata.Flags.IsEmpty)
        {
            Control flagsSection = CreateFlagsSection(metadata.Flags, theme);
            Grid.SetColumn(flagsSection, 1);
            grid.Children.Add(flagsSection);
        }

        return grid;
    }

    private static Control CreateTitleSection(string? name, ThemeResources theme)
    {
        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        MaterialIcon icon = new()
        {
            Kind = MaterialIconKind.RobotOutline,
            Width = 18,
            Height = 18,
            Foreground = theme.Accent,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(icon);

        string displayName = string.IsNullOrWhiteSpace(name) ? EditorStrings.SkillMetadata : name;
        PreviewTextBlock titleBlock = new()
        {
            Text = displayName,
            FontFamily = theme.MonoFont,
            FontSize = 14.5,
            FontWeight = FontWeight.Bold,
            Foreground = theme.Heading,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(titleBlock);

        if (!string.IsNullOrWhiteSpace(name))
            panel.Children.Add(CreateCopyNameButton(name, theme));

        return panel;
    }

    private static Button CreateCopyNameButton(string name, ThemeResources theme)
    {
        MaterialIcon icon = new()
        {
            Kind = MaterialIconKind.ContentCopy,
            Width = 13,
            Height = 13,
            Foreground = theme.Muted
        };

        Button button = new()
        {
            Name = "CopySkillNameButton",
            Content = icon,
            Padding = new Thickness(5, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };

        ToolTip.SetTip(button, EditorStrings.CopySkillName);
        button.Click += (_, _) => CopyNameToClipboard(button, name);

        return button;
    }

    private static void CopyNameToClipboard(Button button, string text)
    {
        try
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(button);
            _ = topLevel?.Clipboard?.SetTextAsync(text);

            button.Content = new MaterialIcon
            {
                Kind = MaterialIconKind.Check,
                Width = 13,
                Height = 13
            };
            ToolTip.SetTip(button, EditorStrings.Copied);

            DispatcherTimer.RunOnce(() =>
            {
                button.Content = new MaterialIcon
                {
                    Kind = MaterialIconKind.ContentCopy,
                    Width = 13,
                    Height = 13
                };
                ToolTip.SetTip(button, EditorStrings.CopySkillName);
            }, TimeSpan.FromSeconds(2));
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Clipboard set text failed: {exception.Message}");
        }
    }

    private static Control CreateFlagsSection(IReadOnlyList<KeyValuePair<string, bool>> flags, ThemeResources theme)
    {
        WrapPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        for (int i = 0; i < flags.Count; i++)
            panel.Children.Add(CreateFlagBadge(flags[i], theme));

        return panel;
    }

    private static Border CreateFlagBadge(KeyValuePair<string, bool> flag, ThemeResources theme)
    {
        StackPanel badgeContent = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        MaterialIcon badgeIcon = new()
        {
            Kind = flag.Value ? MaterialIconKind.AlertCircleOutline : MaterialIconKind.CloseCircleOutline,
            Width = 12,
            Height = 12,
            Foreground = flag.Value ? theme.Accent : theme.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };
        badgeContent.Children.Add(badgeIcon);

        TextBlock badgeText = new()
        {
            Text = flag.Key,
            FontFamily = theme.MonoFont,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = flag.Value ? theme.Heading : theme.Muted,
            VerticalAlignment = VerticalAlignment.Center
        };
        badgeContent.Children.Add(badgeText);

        return new Border
        {
            Background = theme.CodeBlockHeaderBackground,
            BorderBrush = theme.CodeBlockBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(4, 2, 0, 2),
            Child = badgeContent
        };
    }

    private static PreviewTextBlock CreateDescriptionBlock(string description, ThemeResources theme)
    {
        return new PreviewTextBlock
        {
            Text = description,
            FontFamily = theme.ProseFont,
            FontSize = 13.5,
            Foreground = theme.Body,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    private static WrapPanel CreateListsPanel(IReadOnlyList<KeyValuePair<string, System.Collections.Immutable.ImmutableList<string>>> lists, ThemeResources theme)
    {
        WrapPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 0)
        };

        for (int i = 0; i < lists.Count; i++)
            PopulateListPills(panel, lists[i], theme);

        return panel;
    }

    private static void PopulateListPills(
        WrapPanel panel,
        KeyValuePair<string, System.Collections.Immutable.ImmutableList<string>> list,
        ThemeResources theme)
    {
        TextBlock label = new()
        {
            Text = $"{list.Key}:",
            FontSize = 11.5,
            Foreground = theme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 4)
        };
        panel.Children.Add(label);

        for (int i = 0; i < list.Value.Count; i++)
        {
            Border pill = new()
            {
                CornerRadius = new CornerRadius(10),
                Background = theme.CodeInlineBackground,
                Padding = new Thickness(7, 2),
                Margin = new Thickness(0, 0, 6, 4),
                Child = new TextBlock
                {
                    Text = list.Value[i],
                    FontFamily = theme.MonoFont,
                    FontSize = 11,
                    Foreground = theme.CodeInlineForeground
                }
            };
            panel.Children.Add(pill);
        }
    }

    private static Expander CreateAdditionalPropertiesExpander(
        IReadOnlyList<KeyValuePair<string, string>> properties,
        ThemeResources theme)
    {
        return new Expander
        {
            Name = "AdditionalPropertiesExpander",
            Header = new TextBlock
            {
                Text = $"{EditorStrings.AdditionalProperties} ({properties.Count})",
                FontSize = 11.5,
                Foreground = theme.Muted
            },
            Margin = new Thickness(0, 2, 0, 0),
            Padding = new Thickness(4, 6),
            Content = BuildPropertiesGrid(properties, theme)
        };
    }

    private static StackPanel CreateFallbackHeader(ThemeResources theme)
    {
        StackPanel header = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        MaterialIcon icon = new()
        {
            Kind = MaterialIconKind.Tune,
            Width = 16,
            Height = 16,
            Foreground = theme.Accent,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(icon);

        TextBlock title = new()
        {
            Text = EditorStrings.Metadata,
            FontFamily = theme.MonoFont,
            FontSize = 13.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.Heading,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(title);

        return header;
    }

    private static Grid BuildPropertiesGrid(IReadOnlyList<KeyValuePair<string, string>> properties, ThemeResources theme)
    {
        Grid grid = new() { Margin = new Thickness(0, 4, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        for (int i = 0; i < properties.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddPropertyRow(grid, properties[i], i, theme);
        }

        return grid;
    }

    private static void AddPropertyRow(Grid grid, KeyValuePair<string, string> property, int rowIndex, ThemeResources theme)
    {
        TextBlock keyText = new()
        {
            Text = $"{property.Key}:",
            FontFamily = theme.MonoFont,
            FontSize = 11.5,
            Foreground = theme.Muted,
            Margin = new Thickness(0, 2, 12, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(keyText, rowIndex);
        Grid.SetColumn(keyText, 0);
        grid.Children.Add(keyText);

        PreviewTextBlock valText = new()
        {
            Text = property.Value,
            FontFamily = theme.MonoFont,
            FontSize = 11.5,
            Foreground = theme.Body,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(valText, rowIndex);
        Grid.SetColumn(valText, 1);
        grid.Children.Add(valText);
    }

    private static List<KeyValuePair<string, string>> CollectAllProperties(FrontMatterMetadata metadata)
    {
        List<KeyValuePair<string, string>> list = [.. metadata.CustomProperties];

        for (int i = 0; i < metadata.Flags.Count; i++)
            list.Add(new KeyValuePair<string, string>(metadata.Flags[i].Key, metadata.Flags[i].Value ? "true" : "false"));

        for (int i = 0; i < metadata.Lists.Count; i++)
            list.Add(new KeyValuePair<string, string>(metadata.Lists[i].Key, string.Join(", ", metadata.Lists[i].Value)));

        return list;
    }
}
