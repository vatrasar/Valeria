using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Splat;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.Resources;
using ReactiveUI;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Editor screen: split markdown source editor with live styled preview.
/// Purpose: edit markdown with syntax colored source on the left and rendered
/// dark preview with highlighted code blocks on the right.
/// Available functionalities: open/save/save-as files, inline formatting
/// (bold, italic, strike, code, link), block formatting (headings, lists,
/// quote, code fence, table), Ctrl+Enter list continuation, editor panel
/// toggle, automatic closing of markdown code fences, language suggestions
/// after opening a fenced code block, live word count and caret readout,
/// preview panel text search with Ctrl+F, match navigation with Enter/Shift+Enter/F3,
/// case sensitivity toggle, live match highlighting with automatic scrolling,
/// debounced background AutoSave, and navigation to application settings.
/// Key UI elements: SourceEditor (AvaloniaEdit), PreviewBlocksControl
/// (ItemsControl), PreviewSearchBar, PreviewSearchTextBox, SearchMatchCaseToggleButton,
/// SearchMatchCountLabel, SearchPreviousButton, SearchNextButton, CloseSearchButton,
/// code language completion window, EditorToggleButton, SettingsButton, formatting toolbar, status bar.
/// Navigate From: application startup, settings screen (back navigation).
/// Navigate To: settings screen.
/// </summary>
public partial class EditorView : ReactiveUserControl<EditorViewModel>
{
    private const string EmbeddedHighlightingResource =
        "Valeria.Src.Features.Editor.UI.Screens.EditorScreen.Markdown.xshd";

    private const double EditorColumnMinWidth = 250;

    private readonly IPreviewSearchService _searchService;
    private bool _syncingEditor;
    private CompletionWindow? _codeLanguageCompletionWindow;

    public EditorView()
        : this(Locator.Current.GetService<IPreviewSearchService>() ?? new PreviewSearchService())
    {
    }

    public EditorView(IPreviewSearchService searchService)
    {
        _searchService = searchService;
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            RegisterDialogAnchor();
            RegisterWindowShortcuts(disposables);
            ConfigureSourceEditor();
            InterceptListContinuation(disposables);
            InterceptAutoClosingCharacters(disposables);
            InterceptCodeLanguageCompletion(disposables);
            BindPreview(disposables);
            BindPreviewSearch(disposables);
            BindStatusBar(disposables);
            BindFileCommands(disposables);
            TrackFormattingButtons(disposables);
            TrackEditorChanges(disposables);

            _ = ViewModel?.InitializeAsync(default);
        });
    }

    private void RegisterWindowShortcuts(CompositeDisposable disposables)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);

        if (topLevel is not null)
        {
            AttachTopLevelShortcuts(topLevel, disposables);
            return;
        }

        void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            AttachedToVisualTree -= OnAttached;
            TopLevel? attached = TopLevel.GetTopLevel(this);
            if (attached is not null)
                AttachTopLevelShortcuts(attached, disposables);
        }

        AttachedToVisualTree += OnAttached;
        Disposable.Create(() => AttachedToVisualTree -= OnAttached).DisposeWith(disposables);
    }

    private void AttachTopLevelShortcuts(TopLevel topLevel, CompositeDisposable disposables)
    {
        topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
        Disposable.Create(() => topLevel.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown)).DisposeWith(disposables);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs keyEvent)
    {
        if (ViewModel is null || keyEvent.Handled)
            return;

        ProcessShortcutKeyEvent(keyEvent);
    }

    protected override void OnKeyDown(KeyEventArgs keyEvent)
    {
        if (ViewModel is null)
        {
            base.OnKeyDown(keyEvent);
            return;
        }

        ProcessShortcutKeyEvent(keyEvent);

        if (!keyEvent.Handled)
            base.OnKeyDown(keyEvent);
    }

    private void ProcessShortcutKeyEvent(KeyEventArgs keyEvent)
    {
        if (keyEvent.Handled || ViewModel is null)
            return;

        bool control = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (control && shift && keyEvent.Key == Key.S)
        {
            ExecuteSaveAs();
            keyEvent.Handled = true;
            return;
        }

        if (control && keyEvent.Key == Key.F)
        {
            if (CanOpenPreviewSearch())
            {
                OpenOrFocusPreviewSearch();
                keyEvent.Handled = true;
            }
            return;
        }

        if (keyEvent.Key == Key.Escape && ViewModel.State.IsPreviewSearchOpen)
        {
            ViewModel.ClosePreviewSearchCommand.Execute().Subscribe();
            keyEvent.Handled = true;
            return;
        }

        if (keyEvent.Key == Key.F3 && ViewModel.State.IsPreviewSearchOpen)
        {
            if (shift)
                ExecutePreviousSearchMatch();
            else
                ExecuteNextSearchMatch();

            keyEvent.Handled = true;
            return;
        }

        if (control)
            HandleControlShortcut(keyEvent);
    }

    private void HandleControlShortcut(KeyEventArgs keyEvent)
    {
        if (ViewModel is null)
            return;

        switch (keyEvent.Key)
        {
            case Key.O:
                ViewModel.OpenFileCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
            case Key.S:
                ViewModel.SaveFileCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
            case Key.E:
            case Key.P:
                ViewModel.ToggleEditorCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
            case Key.B:
                ApplyInlineWrap("**", EditorStrings.PlaceholderBoldText);
                keyEvent.Handled = true;
                break;
            case Key.I:
                ApplyInlineWrap("*", EditorStrings.PlaceholderItalicText);
                keyEvent.Handled = true;
                break;
            case Key.OemComma:
                ViewModel.NavigateToSettingsCommand.Execute().Subscribe();
                keyEvent.Handled = true;
                break;
        }
    }

    private void ExecuteSaveAs()
    {
        ViewModel?.SaveFileAsCommand.Execute().Subscribe();
    }

    private void RegisterDialogAnchor()
    {
        ViewModel?.SetTopLevelProvider(() => TopLevel.GetTopLevel(this));
    }

    private void InterceptListContinuation(CompositeDisposable disposables)
    {
        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Disposable.Create(() => RemoveHandler(InputElement.KeyDownEvent, OnPreviewKeyDown)).DisposeWith(disposables);
    }

    private void InterceptAutoClosingCharacters(CompositeDisposable disposables)
    {
        AddHandler(InputElement.TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);
        Disposable.Create(() => RemoveHandler(InputElement.TextInputEvent, OnPreviewTextInput)).DisposeWith(disposables);
    }

    private void InterceptCodeLanguageCompletion(CompositeDisposable disposables)
    {
        SourceEditor.TextArea.TextEntering += OnTextEntering;
        Disposable.Create(() => SourceEditor.TextArea.TextEntering -= OnTextEntering).DisposeWith(disposables);
        Disposable.Create(CloseCodeLanguageCompletion).DisposeWith(disposables);
    }

    private void OnTextEntering(object? sender, TextInputEventArgs textEvent)
    {
        if (_codeLanguageCompletionWindow is null || string.IsNullOrEmpty(textEvent.Text))
            return;

        if (!IsLanguageIdentifierCharacter(textEvent.Text[0]))
            _codeLanguageCompletionWindow.CompletionList.RequestInsertion(textEvent);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs keyEvent)
    {
        if (ViewModel is null)
            return;

        bool isCtrlEnter = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Control)
                           && !keyEvent.KeyModifiers.HasFlag(KeyModifiers.Shift)
                           && keyEvent.Key is Key.Return or Key.Enter;

        if (!isCtrlEnter)
            return;

        FormattingResult? result = ViewModel.ContinueList(SourceEditor.CaretOffset);

        if (result is null)
            return;

        ApplyResult(result);
        keyEvent.Handled = true;
    }

    private void OnPreviewTextInput(object? sender, TextInputEventArgs textInputEvent)
    {
        if (textInputEvent.Handled || textInputEvent.Text is not { Length: 1 } inputText)
            return;

        char inputCharacter = inputText[0];

        if (inputCharacter == '`' && TrySkipExistingCodeFence())
        {
            textInputEvent.Handled = true;
            return;
        }

        if (inputCharacter == '`' && TryCompleteCodeFence())
        {
            ShowCodeLanguageCompletion();
            textInputEvent.Handled = true;
            return;
        }

        if (TrySkipExistingClosingCharacter(inputCharacter))
        {
            textInputEvent.Handled = true;
            return;
        }

        char? closingCharacter = GetClosingCharacter(inputCharacter);

        if (closingCharacter is null)
            return;

        InsertPairedCharacters(inputCharacter, closingCharacter.Value);
        textInputEvent.Handled = true;
    }

    private bool TrySkipExistingClosingCharacter(char inputCharacter)
    {
        if (!IsClosingCharacter(inputCharacter) || SourceEditor.SelectionLength != 0)
            return false;

        int caretOffset = SourceEditor.CaretOffset;
        string documentText = SourceEditor.Text ?? string.Empty;

        if (caretOffset >= documentText.Length || documentText[caretOffset] != inputCharacter)
            return false;

        SourceEditor.CaretOffset++;
        return true;
    }

    private void ShowCodeLanguageCompletion()
    {
        CloseCodeLanguageCompletion();

        if (ViewModel is null || ViewModel.CodeLanguageSuggestions.Count == 0)
            return;

        CompletionWindow completionWindow = new(SourceEditor.TextArea)
        {
            StartOffset = SourceEditor.CaretOffset,
            EndOffset = SourceEditor.CaretOffset
        };

        foreach (CodeLanguageSuggestion suggestion in ViewModel.CodeLanguageSuggestions)
            completionWindow.CompletionList.CompletionData.Add(new CodeLanguageCompletionData(suggestion));
        completionWindow.Closed += OnCodeLanguageCompletionClosed;
        _codeLanguageCompletionWindow = completionWindow;
        completionWindow.Show();
    }

    private void OnCodeLanguageCompletionClosed(object? sender, EventArgs args)
    {
        if (sender is CompletionWindow completionWindow)
            completionWindow.Closed -= OnCodeLanguageCompletionClosed;

        _codeLanguageCompletionWindow = null;
    }

    private void CloseCodeLanguageCompletion()
    {
        _codeLanguageCompletionWindow?.Close();
        _codeLanguageCompletionWindow = null;
    }

    private static bool IsLanguageIdentifierCharacter(char character)
    {
        return char.IsLetterOrDigit(character) || character is '#' or '+' or '-';
    }

    private bool TryCompleteCodeFence()
    {
        if (SourceEditor.SelectionLength != 0)
            return false;

        int caretOffset = SourceEditor.CaretOffset;
        string documentText = SourceEditor.Text ?? string.Empty;

        if (caretOffset < 2
            || documentText[caretOffset - 1] != '`'
            || documentText[caretOffset - 2] != '`'
            || caretOffset >= 3 && documentText[caretOffset - 3] == '`')
            return false;

        const string codeFence = "```";
        SourceEditor.Document.Replace(caretOffset - 2, 2, string.Concat(codeFence, codeFence));
        SourceEditor.CaretOffset = caretOffset + 1;
        return true;
    }

    private bool TrySkipExistingCodeFence()
    {
        if (SourceEditor.SelectionLength != 0)
            return false;

        int caretOffset = SourceEditor.CaretOffset;
        string documentText = SourceEditor.Text ?? string.Empty;

        if (caretOffset + 2 >= documentText.Length
            || documentText[caretOffset] != '`'
            || documentText[caretOffset + 1] != '`'
            || documentText[caretOffset + 2] != '`')
            return false;

        SourceEditor.CaretOffset += 3;
        return true;
    }

    private void InsertPairedCharacters(char openingCharacter, char closingCharacter)
    {
        int selectionStart = SourceEditor.SelectionStart;
        int selectionLength = SourceEditor.SelectionLength;
        string selectedText = selectionLength == 0 ? string.Empty : SourceEditor.SelectedText;
        string pairedText = string.Concat(openingCharacter, selectedText, closingCharacter);

        SourceEditor.Document.Replace(selectionStart, selectionLength, pairedText);

        if (selectionLength == 0)
        {
            SourceEditor.CaretOffset = selectionStart + 1;
            return;
        }

        SourceEditor.Select(selectionStart + 1, selectionLength);
    }

    private static char? GetClosingCharacter(char openingCharacter)
    {
        return openingCharacter switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            '"' => '"',
            '\'' => '\'',
            _ => null
        };
    }

    private static bool IsClosingCharacter(char character)
    {
        return character is ')' or ']' or '}' or '"' or '\'';
    }

    private void ConfigureSourceEditor()
    {
        SourceEditor.FontFamily = FindFont("MonoFontFamily");
        SourceEditor.FontSize = ViewModel?.EditorFontSize ?? 14;
        SourceEditor.Background = FindBrush("EditorBackgroundBrush");
        SourceEditor.Foreground = FindBrush("PrimaryTextBrush");
        SourceEditor.LineNumbersForeground = FindBrush("EditorLineNumberBrush");
        SourceEditor.TextArea.SelectionBrush = FindBrush("EditorSelectionBrush");
        SourceEditor.TextArea.TextView.LinkTextForegroundBrush = FindBrush("EditorLinkBrush");
        SourceEditor.ShowLineNumbers = true;
        SourceEditor.WordWrap = true;
        SourceEditor.Options.ConvertTabsToSpaces = true;
        SourceEditor.Options.IndentationSize = ViewModel?.EditorTabWidth ?? 4;
        SourceEditor.SyntaxHighlighting = LoadMarkdownHighlighting();
        PreviewBlocksControl.MaxWidth = ViewModel?.PreviewMaxWidth ?? 860;
    }

    private static IHighlightingDefinition? LoadMarkdownHighlighting()
    {
        try
        {
            using Stream? stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream(EmbeddedHighlightingResource);

            if (stream is null)
                return null;

            using XmlReader reader = XmlReader.Create(stream);

            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception exception) when (exception is XmlException || exception is IOException)
        {
            return null;
        }
    }

    private void BindPreview(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.State.PreviewBlocks, view => view.PreviewBlocksControl.ItemsSource);
        this.OneWayBind(ViewModel, viewModel => viewModel.State.IsEditorVisible, view => view.EditorToggleButton.IsChecked);
        this.OneWayBind(ViewModel, viewModel => viewModel.State.IsPreviewIdle, view => view.PreviewUpdatingIndicator.IsVisible, isIdle => !isIdle);

        this.WhenAnyValue(view => view.ViewModel!.State.IsEditorVisible)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<bool>(SetEditorVisibility))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.MarkdownText)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<string>(SyncEditorText))
            .DisposeWith(disposables);
    }

    private void BindPreviewSearch(CompositeDisposable disposables)
    {
        this.BindCommand(ViewModel, vm => vm.ClosePreviewSearchCommand, v => v.CloseSearchButton);
        this.BindCommand(ViewModel, vm => vm.TogglePreviewSearchMatchCaseCommand, v => v.SearchMatchCaseToggleButton);

        TrackClick(SearchNextButton, (_, _) => ExecuteNextSearchMatch(), disposables);
        TrackClick(SearchPreviousButton, (_, _) => ExecutePreviousSearchMatch(), disposables);

        PreviewSearchTextBox.KeyDown += OnPreviewSearchKeyDown;
        Disposable.Create(() => PreviewSearchTextBox.KeyDown -= OnPreviewSearchKeyDown).DisposeWith(disposables);

        PreviewSearchTextBox.GetObservable(TextBox.TextProperty)
            .Skip(1)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<string?>(OnPreviewSearchTextChanged))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.IsPreviewSearchOpen)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<bool>(OnPreviewSearchOpenChanged))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.PreviewSearchQuery)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<string>(OnPreviewSearchQueryChanged))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.PreviewSearchMatchCase)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<bool>(OnPreviewSearchMatchCaseChanged))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.PreviewBlocks)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<ImmutableList<Control>>(OnPreviewBlocksChangedForSearch))
            .DisposeWith(disposables);
    }

    private void OnPreviewSearchKeyDown(object? sender, KeyEventArgs keyEvent)
    {
        if (ViewModel is null)
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
            ViewModel.ClosePreviewSearchCommand.Execute().Subscribe();
            keyEvent.Handled = true;
        }
    }

    private void OnPreviewSearchTextChanged(string? text)
    {
        if (ViewModel is null)
            return;

        string currentText = text ?? string.Empty;

        if (ViewModel.State.PreviewSearchQuery != currentText)
        {
            ViewModel.SetPreviewSearchQuery(currentText);
            PerformPreviewSearch();
        }
    }

    private void OnPreviewSearchOpenChanged(bool isOpen)
    {
        PreviewSearchBar.IsVisible = isOpen;

        if (isOpen)
        {
            PreviewSearchTextBox.Focus();
            PreviewSearchTextBox.SelectAll();
            PerformPreviewSearch();
            return;
        }

        _searchService.Clear();
        UpdateSearchMatchCountLabel(string.Empty, 0, 0);
    }

    private void OnPreviewSearchQueryChanged(string query)
    {
        if (PreviewSearchTextBox.Text != query)
            PreviewSearchTextBox.Text = query;

        PerformPreviewSearch();
    }

    private void OnPreviewSearchMatchCaseChanged(bool matchCase)
    {
        SearchMatchCaseToggleButton.IsChecked = matchCase;
        PerformPreviewSearch();
    }

    private void OnPreviewBlocksChangedForSearch(ImmutableList<Control> blocks)
    {
        if (ViewModel?.State.IsPreviewSearchOpen == true)
            PerformPreviewSearch();
    }

    private void PerformPreviewSearch()
    {
        if (ViewModel is null)
            return;

        if (!ViewModel.State.IsPreviewSearchOpen)
        {
            _searchService.Clear();
            return;
        }

        string query = ViewModel.State.PreviewSearchQuery;
        bool matchCase = ViewModel.State.PreviewSearchMatchCase;
        IEnumerable<Control> blocks = ViewModel.State.PreviewBlocks;

        PreviewSearchResult result = _searchService.Search(blocks, query, matchCase);
        ViewModel.UpdatePreviewSearchResults(result.CurrentMatchIndex, result.TotalMatches);
        UpdateSearchMatchCountLabel(query, result.CurrentMatchIndex, result.TotalMatches);
    }

    private void ExecuteNextSearchMatch()
    {
        if (ViewModel is null)
            return;

        PreviewSearchResult result = _searchService.NavigateNext();
        ViewModel.UpdatePreviewSearchResults(result.CurrentMatchIndex, result.TotalMatches);
        UpdateSearchMatchCountLabel(ViewModel.State.PreviewSearchQuery, result.CurrentMatchIndex, result.TotalMatches);
    }

    private void ExecutePreviousSearchMatch()
    {
        if (ViewModel is null)
            return;

        PreviewSearchResult result = _searchService.NavigatePrevious();
        ViewModel.UpdatePreviewSearchResults(result.CurrentMatchIndex, result.TotalMatches);
        UpdateSearchMatchCountLabel(ViewModel.State.PreviewSearchQuery, result.CurrentMatchIndex, result.TotalMatches);
    }

    private void UpdateSearchMatchCountLabel(string query, int currentIndex, int totalMatches)
    {
        if (string.IsNullOrEmpty(query))
        {
            SearchMatchCountLabel.Text = string.Empty;
            return;
        }

        if (totalMatches == 0)
        {
            SearchMatchCountLabel.Text = EditorStrings.NoMatches;
            return;
        }

        SearchMatchCountLabel.Text = string.Format(EditorStrings.MatchCountFormat, currentIndex, totalMatches);
    }

    private void OpenOrFocusPreviewSearch()
    {
        if (ViewModel is null)
            return;

        if (!ViewModel.State.IsPreviewSearchOpen)
        {
            ViewModel.OpenPreviewSearchCommand.Execute().Subscribe();
            return;
        }

        PreviewSearchTextBox.Focus();
        PreviewSearchTextBox.SelectAll();
    }

    private bool CanOpenPreviewSearch()
    {
        bool isEditorVisible = EditorPane.IsVisible && SourceEditor.IsVisible;
        bool isEditorFocused = isEditorVisible && (SourceEditor.IsFocused || SourceEditor.TextArea.IsFocused);

        return !isEditorFocused;
    }

    private bool IsEditorAreaFocused()
    {
        bool isEditorVisible = EditorPane.IsVisible && SourceEditor.IsVisible;

        return isEditorVisible && (SourceEditor.IsFocused || SourceEditor.TextArea.IsFocused);
    }

    private void SetEditorVisibility(bool visible)
    {
        EditorPane.IsVisible = visible;
        SourceEditor.IsVisible = visible;
        PaneSplitter.IsVisible = visible;
        SplitGrid.ColumnDefinitions[0].Width = visible ? GridLength.Star : new GridLength(0);
        SplitGrid.ColumnDefinitions[0].MinWidth = visible ? EditorColumnMinWidth : 0;
    }

    private void SyncEditorText(string documentText)
    {
        if (_syncingEditor)
            return;

        if (SourceEditor.Text == documentText)
            return;

        int caret = SourceEditor.CaretOffset;
        SourceEditor.Text = documentText ?? string.Empty;
        SourceEditor.CaretOffset = Math.Min(caret, SourceEditor.Text.Length);
    }

    private void BindStatusBar(CompositeDisposable disposables)
    {
        this.WhenAnyValue(view => view.ViewModel!.State)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(Observer.Create<EditorState>(RenderStatus))
            .DisposeWith(disposables);
    }

    private void RenderStatus(EditorState state)
    {
        DocumentTitleLabel.Text = state.DocumentTitle;
        ErrorLabel.Text = state.ErrorMessage ?? string.Empty;
        ErrorLabel.IsVisible = !string.IsNullOrEmpty(state.ErrorMessage);
        CaretLabel.Text = string.Format(EditorStrings.StatusCaret, state.CaretLine, state.CaretColumn);
        WordCountLabel.Text = $"{state.WordCount} {EditorStrings.Words}";
        EmptyHint.IsVisible = state.IsEmpty;
    }

    private void BindFileCommands(CompositeDisposable disposables)
    {
        this.BindCommand(ViewModel, viewModel => viewModel.OpenFileCommand, view => view.OpenFileButton);
        this.BindCommand(ViewModel, viewModel => viewModel.SaveFileCommand, view => view.SaveFileButton);
        this.BindCommand(ViewModel, viewModel => viewModel.SaveFileAsCommand, view => view.SaveFileAsButton);
        this.BindCommand(ViewModel, viewModel => viewModel.ToggleEditorCommand, view => view.EditorToggleButton);
        this.BindCommand(ViewModel, viewModel => viewModel.NavigateToSettingsCommand, view => view.SettingsButton);
    }

    private void TrackFormattingButtons(CompositeDisposable disposables)
    {
        TrackClick(BoldButton, (_, _) => ApplyInlineWrap("**", EditorStrings.PlaceholderBoldText), disposables);
        TrackClick(ItalicButton, (_, _) => ApplyInlineWrap("*", EditorStrings.PlaceholderItalicText), disposables);
        TrackClick(StrikethroughButton, (_, _) => ApplyInlineWrap("~~", EditorStrings.PlaceholderStrikeText), disposables);
        TrackClick(InlineCodeButton, (_, _) => ApplyInlineWrap("`", EditorStrings.PlaceholderCodeText), disposables);
        TrackClick(LinkButton, (_, _) => ApplyLink(), disposables);
        TrackClick(Heading1Button, (_, _) => ApplyHeading(1), disposables);
        TrackClick(Heading2Button, (_, _) => ApplyHeading(2), disposables);
        TrackClick(Heading3Button, (_, _) => ApplyHeading(3), disposables);
        TrackClick(BulletListButton, (_, _) => ApplyList(), disposables);
        TrackClick(NumberedListButton, (_, _) => ApplyOrderedList(), disposables);
        TrackClick(QuoteButton, (_, _) => ApplyQuote(), disposables);
        TrackClick(CodeBlockButton, (_, _) => ApplyCodeFence(), disposables);
        TrackClick(TableButton, (_, _) => ApplyTable(), disposables);
    }

    private static void TrackClick(Button button, EventHandler<RoutedEventArgs> handler, CompositeDisposable disposables)
    {
        button.Click += handler;
        Disposable.Create(() => button.Click -= handler).DisposeWith(disposables);
    }

    private void TrackEditorChanges(CompositeDisposable disposables)
    {
        SourceEditor.TextChanged += OnSourceTextChanged;
        Disposable.Create(() => SourceEditor.TextChanged -= OnSourceTextChanged).DisposeWith(disposables);

        SourceEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        Disposable.Create(() => SourceEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged).DisposeWith(disposables);
    }

    private void OnSourceTextChanged(object? sender, EventArgs args)
    {
        if (_syncingEditor || ViewModel is null)
            return;

        ViewModel.SetMarkdownText(SourceEditor.Text ?? string.Empty);
    }

    private void OnCaretPositionChanged(object? sender, EventArgs args)
    {
        ViewModel?.UpdateCaret(SourceEditor.TextArea.Caret.Line, SourceEditor.TextArea.Caret.Column);
    }

    private void ApplyInlineWrap(string marker, string placeholder)
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyInlineWrap(marker, placeholder, SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    private void ApplyLink()
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyLink(SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    private void ApplyHeading(int level)
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyHeading(level, SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    private void ApplyList()
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyBulletList(SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    private void ApplyOrderedList()
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyOrderedList(SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    private void ApplyQuote()
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyQuote(SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    private void ApplyCodeFence()
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyCodeFence(SourceEditor.SelectionStart, SourceEditor.SelectionLength));
    }

    private void ApplyTable()
    {
        if (ViewModel is null)
            return;

        ApplyResult(ViewModel.ApplyTable(SourceEditor.CaretOffset));
    }

    private void ApplyResult(FormattingResult result)
    {
        _syncingEditor = true;

        try
        {
            SourceEditor.Text = result.Text;
            SelectResult(result);
        }
        finally
        {
            _syncingEditor = false;
        }

        SourceEditor.Focus();
    }

    private void SelectResult(FormattingResult result)
    {
        int caret = Math.Clamp(result.CaretOffset, 0, result.Text.Length);
        int length = Math.Clamp(result.SelectionLength, 0, result.Text.Length - caret);
        SourceEditor.Select(caret, length);
    }

    private static IBrush FindBrush(string key)
    {
        if (Application.Current?.TryFindResource(key, out object? value) == true && value is IBrush brush)
            return brush;

        return Brushes.Transparent;
    }

    private static FontFamily FindFont(string key)
    {
        if (Application.Current?.TryFindResource(key, out object? value) == true && value is FontFamily family)
            return family;

        return FontFamily.Default;
    }

    private sealed class CodeLanguageCompletionData : ICompletionData
    {
        private readonly CodeLanguageSuggestion _suggestion;

        public CodeLanguageCompletionData(CodeLanguageSuggestion suggestion)
        {
            _suggestion = suggestion;
        }

        public IImage? Image => null;

        public string Text => _suggestion.Identifier;

        public object Content => _suggestion.Identifier;

        public object Description => _suggestion.DisplayName;

        public double Priority => 1;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, _suggestion.Identifier);
        }
    }
}
