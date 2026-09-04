using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reflection;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Resources;
using ReactiveUI;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Editor screen: split markdown source editor with live styled preview.
/// Purpose: edit markdown with syntax colored source on the left and rendered
/// dark preview with highlighted code blocks on the right.
/// Available functionalities: open/save/save-as files, inline formatting
/// (bold, italic, strike, code, link), block formatting (headings, lists,
/// quote, code fence, table), preview toggle, live word count and caret readout.
/// Key UI elements: SourceEditor (AvaloniaEdit), PreviewBlocksControl
/// (ItemsControl), formatting toolbar, status bar.
/// Navigate From: application startup.
/// Navigate To: none, single screen application.
/// </summary>
public partial class EditorView : ReactiveUserControl<EditorViewModel>
{
    private const string EmbeddedHighlightingResource =
        "Valeria.Src.Features.Editor.UI.Screens.EditorScreen.Markdown.xshd";

    private const double PreviewColumnMinWidth = 250;

    private bool _syncingEditor;

    public EditorView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            RegisterDialogAnchor();
            ConfigureSourceEditor();
            BindPreview(disposables);
            BindStatusBar(disposables);
            BindFileCommands(disposables);
            TrackFormattingButtons(disposables);
            TrackEditorChanges(disposables);

            _ = ViewModel?.InitializeAsync(default);
        });
    }

    protected override void OnKeyDown(KeyEventArgs keyEvent)
    {
        if (ViewModel is null)
        {
            base.OnKeyDown(keyEvent);
            return;
        }

        bool control = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (control && shift && keyEvent.Key == Key.S)
            ExecuteSaveAs();
        else if (control)
            HandleControlShortcut(keyEvent);

        if (!keyEvent.Handled)
            base.OnKeyDown(keyEvent);
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
            case Key.P:
                ViewModel.TogglePreviewCommand.Execute().Subscribe();
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
        this.OneWayBind(ViewModel, viewModel => viewModel.State.IsPreviewVisible, view => view.PreviewToggleButton.IsChecked);
        this.OneWayBind(ViewModel, viewModel => viewModel.State.IsPreviewIdle, view => view.PreviewUpdatingIndicator.IsVisible, isIdle => !isIdle);

        this.WhenAnyValue(view => view.ViewModel!.State.IsPreviewVisible)
            .Subscribe(Observer.Create<bool>(SetPreviewVisibility))
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.State.MarkdownText)
            .Subscribe(Observer.Create<string>(SyncEditorText))
            .DisposeWith(disposables);
    }

    private void SetPreviewVisibility(bool visible)
    {
        PreviewPane.IsVisible = visible;
        PaneSplitter.IsVisible = visible;
        SplitGrid.ColumnDefinitions[2].Width = visible ? GridLength.Star : new GridLength(0);
        SplitGrid.ColumnDefinitions[2].MinWidth = visible ? PreviewColumnMinWidth : 0;
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
        EmptyHint.IsVisible = state.IsEmpty && state.IsPreviewVisible;
    }

    private void BindFileCommands(CompositeDisposable disposables)
    {
        this.BindCommand(ViewModel, viewModel => viewModel.OpenFileCommand, view => view.OpenFileButton);
        this.BindCommand(ViewModel, viewModel => viewModel.SaveFileCommand, view => view.SaveFileButton);
        this.BindCommand(ViewModel, viewModel => viewModel.SaveFileAsCommand, view => view.SaveFileAsButton);
        this.BindCommand(ViewModel, viewModel => viewModel.TogglePreviewCommand, view => view.PreviewToggleButton);
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
}
