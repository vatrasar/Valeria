using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.Resources;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Manages the preview search bar UI, query debouncing, match navigation, and status readout.
/// </summary>
public sealed class EditorSearchController
{
    private readonly Border _searchBar;
    private readonly TextBox _searchTextBox;
    private readonly ToggleButton _matchCaseButton;
    private readonly TextBlock _matchCountLabel;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _closeButton;
    private readonly IPreviewSearchService _searchService;
    private readonly Func<EditorViewModel?> _getViewModel;

    public EditorSearchController(
        Border searchBar,
        TextBox searchTextBox,
        ToggleButton matchCaseButton,
        TextBlock matchCountLabel,
        Button previousButton,
        Button nextButton,
        Button closeButton,
        IPreviewSearchService searchService,
        Func<EditorViewModel?> getViewModel)
    {
        _searchBar = searchBar;
        _searchTextBox = searchTextBox;
        _matchCaseButton = matchCaseButton;
        _matchCountLabel = matchCountLabel;
        _previousButton = previousButton;
        _nextButton = nextButton;
        _closeButton = closeButton;
        _searchService = searchService;
        _getViewModel = getViewModel;
    }

    public void Attach(CompositeDisposable disposables)
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        TrackClick(_closeButton, (_, _) => viewModel.ClosePreviewSearchCommand.Execute().Subscribe(), disposables);
        TrackClick(_matchCaseButton, (_, _) => viewModel.TogglePreviewSearchMatchCaseCommand.Execute().Subscribe(), disposables);
        TrackClick(_nextButton, (_, _) => ExecuteNextSearchMatch(), disposables);
        TrackClick(_previousButton, (_, _) => ExecutePreviousSearchMatch(), disposables);

        _searchTextBox.KeyDown += OnSearchKeyDown;
        Disposable.Create(() => _searchTextBox.KeyDown -= OnSearchKeyDown).DisposeWith(disposables);

        _searchTextBox.GetObservable(TextBox.TextProperty)
            .Skip(1)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<string?>(OnSearchTextChanged))
            .DisposeWith(disposables);

        viewModel.WhenAnyValue(vm => vm.State.IsPreviewSearchOpen)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<bool>(OnSearchOpenChanged))
            .DisposeWith(disposables);

        viewModel.WhenAnyValue(vm => vm.State.PreviewSearchQuery)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<string>(OnSearchQueryChanged))
            .DisposeWith(disposables);

        viewModel.WhenAnyValue(vm => vm.State.PreviewSearchMatchCase)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<bool>(OnSearchMatchCaseChanged))
            .DisposeWith(disposables);

        viewModel.WhenAnyValue(vm => vm.State.PreviewBlocks)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<ImmutableList<Control>>(OnPreviewBlocksChanged))
            .DisposeWith(disposables);
    }

    public void OpenOrFocus()
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        if (!viewModel.State.IsPreviewSearchOpen)
        {
            viewModel.OpenPreviewSearchCommand.Execute().Subscribe();
            return;
        }

        _searchTextBox.Focus();
        _searchTextBox.SelectAll();
    }

    public void ExecuteNextSearchMatch()
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        PreviewSearchResult result = _searchService.NavigateNext();
        viewModel.UpdatePreviewSearchResults(result.CurrentMatchIndex, result.TotalMatches);
        UpdateMatchCountLabel(viewModel.State.PreviewSearchQuery, result.CurrentMatchIndex, result.TotalMatches);
    }

    public void ExecutePreviousSearchMatch()
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        PreviewSearchResult result = _searchService.NavigatePrevious();
        viewModel.UpdatePreviewSearchResults(result.CurrentMatchIndex, result.TotalMatches);
        UpdateMatchCountLabel(viewModel.State.PreviewSearchQuery, result.CurrentMatchIndex, result.TotalMatches);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs keyEvent)
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        if (keyEvent.Key is Key.Return or Key.Enter)
        {
            if (keyEvent.KeyModifiers.HasFlag(KeyModifiers.Shift))
                ExecutePreviousSearchMatch();
            else
                ExecuteNextSearchMatch();

            keyEvent.Handled = true;
        }
        else if (keyEvent.Key == Key.Escape)
        {
            viewModel.ClosePreviewSearchCommand.Execute().Subscribe();
            keyEvent.Handled = true;
        }
    }

    private void OnSearchTextChanged(string? text)
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        string currentText = text ?? string.Empty;

        if (viewModel.State.PreviewSearchQuery != currentText)
        {
            viewModel.SetPreviewSearchQuery(currentText);
            PerformSearch();
        }
    }

    private void OnSearchOpenChanged(bool isOpen)
    {
        _searchBar.IsVisible = isOpen;

        if (isOpen)
        {
            _searchTextBox.Focus();
            _searchTextBox.SelectAll();
            PerformSearch();
            return;
        }

        _searchService.Clear();
        UpdateMatchCountLabel(string.Empty, 0, 0);
    }

    private void OnSearchQueryChanged(string query)
    {
        if (_searchTextBox.Text != query)
            _searchTextBox.Text = query;

        PerformSearch();
    }

    private void OnSearchMatchCaseChanged(bool matchCase)
    {
        _matchCaseButton.IsChecked = matchCase;
        PerformSearch();
    }

    private void OnPreviewBlocksChanged(ImmutableList<Control> blocks)
    {
        if (_getViewModel()?.State.IsPreviewSearchOpen == true)
            PerformSearch();
    }

    private void PerformSearch()
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        if (!viewModel.State.IsPreviewSearchOpen)
        {
            _searchService.Clear();
            return;
        }

        string query = viewModel.State.PreviewSearchQuery;
        bool matchCase = viewModel.State.PreviewSearchMatchCase;
        IEnumerable<Control> blocks = viewModel.State.PreviewBlocks;

        PreviewSearchResult result = _searchService.Search(blocks, query, matchCase);
        viewModel.UpdatePreviewSearchResults(result.CurrentMatchIndex, result.TotalMatches);
        UpdateMatchCountLabel(query, result.CurrentMatchIndex, result.TotalMatches);
    }

    private void UpdateMatchCountLabel(string query, int currentIndex, int totalMatches)
    {
        if (string.IsNullOrEmpty(query))
        {
            _matchCountLabel.Text = string.Empty;
            return;
        }

        if (totalMatches == 0)
        {
            _matchCountLabel.Text = EditorStrings.NoMatches;
            return;
        }

        _matchCountLabel.Text = string.Format(EditorStrings.MatchCountFormat, currentIndex, totalMatches);
    }

    private static void TrackClick(Button button, EventHandler<RoutedEventArgs> handler, CompositeDisposable disposables)
    {
        button.Click += handler;
        Disposable.Create(() => button.Click -= handler).DisposeWith(disposables);
    }
}
