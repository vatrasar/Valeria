using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using Valeria.Src.Core.Markdown;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

/// <summary>
/// Controls text editing behaviors: list continuation, auto-closing characters,
/// code language completion, and list renumbering on item deletion.
/// </summary>
public sealed class EditorTextEditController
{
    private readonly TextEditor _sourceEditor;
    private readonly Func<EditorViewModel?> _getViewModel;
    private readonly Action<FormattingResult> _applyResult;
    private CompletionWindow? _codeLanguageCompletionWindow;
    private int _lastRemovalOffset = -1;
    private bool _hasPendingDeletion;
    private bool _isRenumberingList;

    public EditorTextEditController(
        TextEditor sourceEditor,
        Func<EditorViewModel?> getViewModel,
        Action<FormattingResult> applyResult)
    {
        _sourceEditor = sourceEditor;
        _getViewModel = getViewModel;
        _applyResult = applyResult;
    }

    public void Attach(CompositeDisposable disposables)
    {
        _sourceEditor.AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Disposable.Create(() => _sourceEditor.RemoveHandler(InputElement.KeyDownEvent, OnPreviewKeyDown)).DisposeWith(disposables);

        _sourceEditor.AddHandler(InputElement.TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);
        Disposable.Create(() => _sourceEditor.RemoveHandler(InputElement.TextInputEvent, OnPreviewTextInput)).DisposeWith(disposables);

        _sourceEditor.TextArea.TextEntering += OnTextEntering;
        Disposable.Create(() => _sourceEditor.TextArea.TextEntering -= OnTextEntering).DisposeWith(disposables);
        Disposable.Create(CloseCodeLanguageCompletion).DisposeWith(disposables);

        _sourceEditor.Document.Changing += OnDocumentChanging;
        Disposable.Create(() => _sourceEditor.Document.Changing -= OnDocumentChanging).DisposeWith(disposables);

        _sourceEditor.TextChanged += OnSourceTextChanged;
        Disposable.Create(() => _sourceEditor.TextChanged -= OnSourceTextChanged).DisposeWith(disposables);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs keyEvent)
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        bool isCtrlEnter = keyEvent.KeyModifiers.HasFlag(KeyModifiers.Control)
                           && !keyEvent.KeyModifiers.HasFlag(KeyModifiers.Shift)
                           && keyEvent.Key is Key.Return or Key.Enter;

        if (!isCtrlEnter)
            return;

        FormattingResult? result = viewModel.ContinueList(_sourceEditor.CaretOffset);

        if (result is null)
            return;

        _applyResult(result);
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

    private void OnTextEntering(object? sender, TextInputEventArgs textEvent)
    {
        if (_codeLanguageCompletionWindow is null || string.IsNullOrEmpty(textEvent.Text))
            return;

        if (!IsLanguageIdentifierCharacter(textEvent.Text[0]))
            _codeLanguageCompletionWindow.CompletionList.RequestInsertion(textEvent);
    }

    private void OnDocumentChanging(object? sender, DocumentChangeEventArgs eventArgs)
    {
        if (_isRenumberingList)
            return;

        if (eventArgs.RemovalLength <= 0)
            return;

        _lastRemovalOffset = eventArgs.Offset;
        _hasPendingDeletion = true;
    }

    private void OnSourceTextChanged(object? sender, EventArgs args)
    {
        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null)
            return;

        if (_hasPendingDeletion)
        {
            _hasPendingDeletion = false;
            TryRenumberOrderedListsAfterDeletion(_lastRemovalOffset);
        }

        viewModel.SetMarkdownText(_sourceEditor.Text ?? string.Empty);
    }

    private void TryRenumberOrderedListsAfterDeletion(int removalOffset)
    {
        if (removalOffset < 0 || _sourceEditor.Document is null)
            return;

        string currentText = _sourceEditor.Text ?? string.Empty;
        IReadOnlyList<ListNumberAdjustment> adjustments =
            MarkdownListRenumberer.GetOrderedListAdjustmentsAfterDeletion(currentText, removalOffset);

        if (adjustments.Count == 0)
            return;

        ApplyListAdjustments(adjustments);
    }

    private void ApplyListAdjustments(IReadOnlyList<ListNumberAdjustment> adjustments)
    {
        _isRenumberingList = true;

        try
        {
            for (int index = adjustments.Count - 1; index >= 0; index--)
                _sourceEditor.Document.Replace(adjustments[index].Offset, adjustments[index].Length, adjustments[index].NewNumber);
        }
        finally
        {
            _isRenumberingList = false;
        }
    }

    private bool TrySkipExistingClosingCharacter(char inputCharacter)
    {
        if (!IsClosingCharacter(inputCharacter) || _sourceEditor.SelectionLength != 0)
            return false;

        int caretOffset = _sourceEditor.CaretOffset;
        string documentText = _sourceEditor.Text ?? string.Empty;

        if (caretOffset >= documentText.Length || documentText[caretOffset] != inputCharacter)
            return false;

        _sourceEditor.CaretOffset++;
        return true;
    }

    private void ShowCodeLanguageCompletion()
    {
        CloseCodeLanguageCompletion();

        EditorViewModel? viewModel = _getViewModel();
        if (viewModel is null || viewModel.CodeLanguageSuggestions.Count == 0)
            return;

        CompletionWindow completionWindow = new(_sourceEditor.TextArea)
        {
            StartOffset = _sourceEditor.CaretOffset,
            EndOffset = _sourceEditor.CaretOffset
        };

        foreach (CodeLanguageSuggestion suggestion in viewModel.CodeLanguageSuggestions)
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
        if (_sourceEditor.SelectionLength != 0)
            return false;

        int caretOffset = _sourceEditor.CaretOffset;
        string documentText = _sourceEditor.Text ?? string.Empty;

        if (caretOffset < 2
            || documentText[caretOffset - 1] != '`'
            || documentText[caretOffset - 2] != '`'
            || caretOffset >= 3 && documentText[caretOffset - 3] == '`')
            return false;

        const string codeFence = "```";
        _sourceEditor.Document.Replace(caretOffset - 2, 2, string.Concat(codeFence, codeFence));
        _sourceEditor.CaretOffset = caretOffset + 1;
        return true;
    }

    private bool TrySkipExistingCodeFence()
    {
        if (_sourceEditor.SelectionLength != 0)
            return false;

        int caretOffset = _sourceEditor.CaretOffset;
        string documentText = _sourceEditor.Text ?? string.Empty;

        if (caretOffset + 2 >= documentText.Length
            || documentText[caretOffset] != '`'
            || documentText[caretOffset + 1] != '`'
            || documentText[caretOffset + 2] != '`')
            return false;

        _sourceEditor.CaretOffset += 3;
        return true;
    }

    private void InsertPairedCharacters(char openingCharacter, char closingCharacter)
    {
        int selectionStart = _sourceEditor.SelectionStart;
        int selectionLength = _sourceEditor.SelectionLength;
        string selectedText = selectionLength == 0 ? string.Empty : _sourceEditor.SelectedText;
        string pairedText = string.Concat(openingCharacter, selectedText, closingCharacter);

        _sourceEditor.Document.Replace(selectionStart, selectionLength, pairedText);

        if (selectionLength == 0)
        {
            _sourceEditor.CaretOffset = selectionStart + 1;
            return;
        }

        _sourceEditor.Select(selectionStart + 1, selectionLength);
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
}
