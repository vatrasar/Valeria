using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Material.Icons;
using Material.Icons.Avalonia;
using ReactiveUI;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Enums;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen.ScreenComponents.FilesDock;

/// <summary>
/// Files dock component: collapsible side panel for favorite and recent files.
/// Purpose: display favorites sorted by last usage with custom aliases, allow adding
/// the active open file with a custom alias, display the top-pinned last-opened non-favorite
/// file, and allow switching to a 24-hour recent files list with automated cleanup.
/// Usage:
/// Inputs: DataContext assigned with FilesDockViewModel.
/// Outputs: ViewModel.FileSelected observable for file selection events.
/// Key UI elements: DockRootBorder, FavoritesModeButton, RecentModeButton,
/// AddCurrentFileButton, AddFavoritePanel, FavoriteCustomNameTextBox,
/// SummitCard, FavoritesListPanel, RecentListPanel, CollapseDockButton.
/// Used In: EditorView.
/// </summary>
public partial class FilesDockView : ReactiveUserControl<FilesDockViewModel>
{
    public FilesDockView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            BindCommands();
            BindStateObservables(disposables);
            BindKeyboardShortcuts(disposables);
        });
    }

    private void BindCommands()
    {
        this.BindCommand(ViewModel, vm => vm.ToggleDockExpansionCommand, view => view.CollapseDockButton);
        this.BindCommand(ViewModel, vm => vm.OpenAddFavoriteFormCommand, view => view.AddCurrentFileButton);
        this.BindCommand(ViewModel, vm => vm.CancelAddFavoriteFormCommand, view => view.CancelAddFavoriteButton);
        this.BindCommand(ViewModel, vm => vm.ConfirmAddFavoriteCommand, view => view.ConfirmAddFavoriteButton);
        this.BindCommand(ViewModel, vm => vm.SetFavoritesModeCommand, view => view.FavoritesModeButton);
        this.BindCommand(ViewModel, vm => vm.SetRecentModeCommand, view => view.RecentModeButton);
        this.OneWayBind(ViewModel, vm => vm.State.CanAddCurrentFile, view => view.AddCurrentFileButton.IsEnabled);
        this.OneWayBind(ViewModel, vm => vm.State.IsAddFavoriteFormOpen, view => view.AddFavoritePanel.IsVisible);
    }

    private void BindStateObservables(CompositeDisposable disposables)
    {
        this.WhenAnyValue(view => view.ViewModel!.State.IsDockExpanded)
            .Subscribe(Observer.Create<bool>(isExpanded => DockRootBorder.Classes.Set("collapsed", !isExpanded)))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.Mode)
            .Subscribe(Observer.Create<FilesDockMode>(ApplyDockMode))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.NewFavoriteCustomName)
            .Subscribe(Observer.Create<string>(text =>
            {
                if (!string.Equals(FavoriteCustomNameTextBox.Text, text, StringComparison.Ordinal))
                    FavoriteCustomNameTextBox.Text = text ?? string.Empty;
            }))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.LastOpenedNonFavorite)
            .Subscribe(Observer.Create<RecentFile?>(RenderSummitNonFavorite))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.Favorites)
            .Subscribe(Observer.Create<ImmutableList<FavoriteFile>>(RenderFavoritesList))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.RecentFiles)
            .Subscribe(Observer.Create<ImmutableList<RecentFile>>(RenderRecentFilesList))
            .DisposeWith(disposables);

        FavoriteCustomNameTextBox.TextChanged += (_, _) =>
        {
            ViewModel?.SetNewFavoriteCustomName(FavoriteCustomNameTextBox.Text ?? string.Empty);
        };
    }

    private void BindKeyboardShortcuts(CompositeDisposable disposables)
    {
        FavoriteCustomNameTextBox.KeyDown += OnCustomNameTextBoxKeyDown;
        Disposable.Create(() => FavoriteCustomNameTextBox.KeyDown -= OnCustomNameTextBoxKeyDown).DisposeWith(disposables);
    }

    private void OnCustomNameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel?.ConfirmAddFavoriteCommand.Execute().Subscribe();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel?.CancelAddFavoriteFormCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }

    private void ApplyDockMode(FilesDockMode mode)
    {
        bool isFavorites = mode == FilesDockMode.Favorites;

        FavoritesContainer.IsVisible = isFavorites;
        RecentContainer.IsVisible = !isFavorites;
        AddCurrentFileButton.IsVisible = isFavorites;

        FavoritesModeButton.Classes.Set("active", isFavorites);
        RecentModeButton.Classes.Set("active", !isFavorites);
    }

    private void RenderSummitNonFavorite(RecentFile? nonFavorite)
    {
        if (nonFavorite is null)
        {
            SummitCard.IsVisible = false;
            return;
        }

        SummitCard.IsVisible = true;
        SummitFileNameText.Text = nonFavorite.FileName;
        SummitFilePathText.Text = nonFavorite.FilePath;

        SummitOpenButton.Command = ReactiveCommand.Create(() =>
        {
            ViewModel?.OpenFileCommand.Execute(nonFavorite.FilePath).Subscribe();
        });

        SummitAddFavoriteButton.Command = ReactiveCommand.Create(() =>
        {
            _ = ViewModel?.NotifyFileOpenedAsync(nonFavorite.FilePath);
            ViewModel?.OpenAddFavoriteFormCommand.Execute().Subscribe();
        });
    }

    private void RenderFavoritesList(ImmutableList<FavoriteFile> favorites)
    {
        FavoritesListPanel.Children.Clear();
        EmptyFavoritesHint.IsVisible = favorites.IsEmpty;

        foreach (FavoriteFile file in favorites)
        {
            Control itemControl = CreateFavoriteItemCard(file);
            FavoritesListPanel.Children.Add(itemControl);
        }
    }

    private Control CreateFavoriteItemCard(FavoriteFile file)
    {
        Border card = new() { Classes = { "itemCard" } };

        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        Button openButton = new()
        {
            Classes = { "cardButton" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Command = ReactiveCommand.Create(() => ViewModel?.OpenFileCommand.Execute(file.FilePath).Subscribe())
        };

        StackPanel textPanel = new() { Spacing = 2 };
        TextBlock titleBlock = new()
        {
            Classes = { "itemTitle" },
            Text = file.CustomName
        };
        TextBlock pathBlock = new()
        {
            Classes = { "itemSubtitle" },
            Text = file.FilePath
        };

        textPanel.Children.Add(titleBlock);
        textPanel.Children.Add(pathBlock);
        openButton.Content = textPanel;

        Button removeButton = new()
        {
            Classes = { "removeCardButton" },
            VerticalAlignment = VerticalAlignment.Center,
            Command = ReactiveCommand.Create(() => ViewModel?.RemoveFavoriteCommand.Execute(file.Id).Subscribe()),
            Content = new MaterialIcon { Kind = MaterialIconKind.DeleteOutline, Width = 16, Height = 16 }
        };

        Grid.SetColumn(openButton, 0);
        Grid.SetColumn(removeButton, 1);

        grid.Children.Add(openButton);
        grid.Children.Add(removeButton);

        card.Child = grid;
        return card;
    }

    private void RenderRecentFilesList(ImmutableList<RecentFile> recentFiles)
    {
        RecentListPanel.Children.Clear();
        EmptyRecentHint.IsVisible = recentFiles.IsEmpty;

        foreach (RecentFile file in recentFiles)
        {
            Control itemControl = CreateRecentItemCard(file);
            RecentListPanel.Children.Add(itemControl);
        }
    }

    private Control CreateRecentItemCard(RecentFile file)
    {
        Border card = new() { Classes = { "itemCard" } };

        Button openButton = new()
        {
            Classes = { "cardButton" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Command = ReactiveCommand.Create(() => ViewModel?.OpenFileCommand.Execute(file.FilePath).Subscribe())
        };

        StackPanel textPanel = new() { Spacing = 2 };
        TextBlock titleBlock = new()
        {
            Classes = { "itemTitle" },
            Text = file.FileName
        };
        TextBlock pathBlock = new()
        {
            Classes = { "itemSubtitle" },
            Text = file.FilePath
        };
        TextBlock timeBlock = new()
        {
            Classes = { "itemSubtitle" },
            Text = FormatRelativeTime(file.LastOpenedAtUtc)
        };

        textPanel.Children.Add(titleBlock);
        textPanel.Children.Add(pathBlock);
        textPanel.Children.Add(timeBlock);

        openButton.Content = textPanel;
        card.Child = openButton;

        return card;
    }

    private static string FormatRelativeTime(DateTime timestampUtc)
    {
        TimeSpan elapsed = DateTime.UtcNow - timestampUtc;

        if (elapsed.TotalMinutes < 1)
            return "just now";

        if (elapsed.TotalHours < 1)
            return $"{(int)elapsed.TotalMinutes}m ago";

        return $"{(int)elapsed.TotalHours}h ago";
    }
}
