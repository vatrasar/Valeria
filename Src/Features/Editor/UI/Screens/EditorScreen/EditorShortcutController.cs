using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Valeria.Src.Features.Editor.Resources;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Handles top-level and view-level keyboard shortcuts for editor actions.
/// </summary>
public sealed class EditorShortcutController
{
    private readonly Control _view;
    private readonly Control _sourceEditor;
    private readonly Control _editorPane;
    private readonly Func<EditorViewModel?> _getViewModel;
    private readonly EditorSearchController _searchController;
    private readonly Action<string, string> _applyInlineWrap;

    public EditorShortcutController(
        Control view,
        Control sourceEditor,
        Control editorPane,
        Func<EditorViewModel?> getViewModel,
        EditorSearchController searchController,
        Action<string, string> applyInlineWrap)
    {
        _view = view;
        _sourceEditor = sourceEditor;
        _editorPane = editorPane;
        _getViewModel = getViewModel;
        _searchController = searchController;
        _applyInlineWrap = applyInlineWrap;
    }

    public void Attach(CompositeDisposable disposables)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(_view);

        if (topLevel is not null)
        {
            AttachTopLevelShortcuts(topLevel, disposables);
            return;
        }

        void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _view.AttachedToVisualTree -= OnAttached;
            TopLevel? attached = TopLevel.GetTopLevel(_view);
            if (attached is not null)
                AttachTopLevelShortcuts(attached, disposables);
        }

        _view.AttachedToVisualTree += OnAttached;
        Disposable.Create(() => _view.AttachedToVisualTree -= OnAttached).DisposeWith(disposables);
    }

    public void ProcessShortcutKeyEvent(KeyEventArgs keyEvent)
    {
        EditorViewModel? viewModel = _getViewModel();
        if (keyEvent.Handled || viewModel is null)
            return;

        bool control = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (control && shift && keyEvent.Key == Key.S)
        {
            viewModel.SaveFileAsCommand.Execute().Subscribe();
            keyEvent.Handled = true;
            return;
        }

        if (control && keyEvent.Key == Key.F)
        {
            if (CanOpenPreviewSearch())
            {
                _searchController.OpenOrFocus();
                keyEvent.Handled = true;
            }
            return;
        }

        if (keyEvent.Key == Key.Escape && viewModel.State.IsPreviewSearchOpen)
        {
            viewModel.ClosePreviewSearchCommand.Execute().Subscribe();
            keyEvent.Handled = true;
            return;
        }

        if (keyEvent.Key == Key.F3 && viewModel.State.IsPreviewSearchOpen)
        {
            if (shift)
                _searchController.ExecutePreviousSearchMatch();
            else
                _searchController.ExecuteNextSearchMatch();

            keyEvent.Handled = true;
            return;
        }

        if (control)
            HandleControlShortcut(keyEvent, viewModel);
    }

    private void AttachTopLevelShortcuts(TopLevel topLevel, CompositeDisposable disposables)
    {
        topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
        Disposable.Create(() => topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown)).DisposeWith(disposables);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs keyEvent)
    {
        ProcessShortcutKeyEvent(keyEvent);
    }

    private void HandleControlShortcut(KeyEventArgs keyEvent, EditorViewModel viewModel)
    {
        switch (keyEvent.Key)
        {
            case Key.O:
                viewModel.OpenFileCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
            case Key.S:
                viewModel.SaveFileCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
            case Key.E:
            case Key.P:
                viewModel.ToggleEditorCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
            case Key.B:
                _applyInlineWrap("**", EditorStrings.PlaceholderBoldText);
                keyEvent.Handled = true;
                break;
            case Key.I:
                _applyInlineWrap("*", EditorStrings.PlaceholderItalicText);
                keyEvent.Handled = true;
                break;
            case Key.OemComma:
                viewModel.NavigateToSettingsCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
        }
    }

    private bool CanOpenPreviewSearch()
    {
        bool isEditorVisible = _editorPane.IsVisible && _sourceEditor.IsVisible;
        bool isEditorFocused = isEditorVisible && _sourceEditor.IsFocused;

        return !isEditorFocused;
    }
}
