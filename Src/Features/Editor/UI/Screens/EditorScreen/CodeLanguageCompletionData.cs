using System;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Valeria.Src.Features.Editor.Domain.Models;

namespace Valeria.Src.Features.Editor.UI.Screens.EditorScreen;

internal sealed class CodeLanguageCompletionData : ICompletionData
{
    private readonly CodeLanguageSuggestion _suggestion;

    public CodeLanguageCompletionData(CodeLanguageSuggestion suggestion)
    {
        _suggestion = suggestion;
    }

    public IImage? Image => null;
    public string Text => _suggestion.Identifier;
    public object Content => $"{_suggestion.DisplayName} ({_suggestion.Identifier})";
    public object Description => _suggestion.DisplayName;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}
