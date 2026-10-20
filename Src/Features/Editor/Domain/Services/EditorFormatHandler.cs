using System;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.Domain.Services;

/// <summary>
/// Executes markdown formatting actions on text, updating editor state.
/// </summary>
public sealed class EditorFormatHandler
{
    private readonly Func<string> _getText;
    private readonly Action<string> _setText;

    public EditorFormatHandler(Func<string> getText, Action<string> setText)
    {
        _getText = getText;
        _setText = setText;
    }

    public void ToggleTask(int taskIndex, bool isChecked)
    {
        FormattingResult? result = EditorFormatting.ToggleTask(_getText(), taskIndex, isChecked);

        if (result is null)
            return;

        _setText(result.Text);
    }

    public FormattingResult ApplyInlineWrap(string marker, string placeholder, int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleWrap(_getText(), selectionStart, selectionLength, marker, placeholder);
        _setText(result.Text);

        return result;
    }

    public FormattingResult ApplyLink(int selectionStart, int selectionLength, string placeholder)
    {
        FormattingResult result = EditorFormatting.WrapLink(_getText(), selectionStart, selectionLength, placeholder);
        _setText(result.Text);

        return result;
    }

    public FormattingResult ApplyHeading(int level, int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleHeading(_getText(), selectionStart, selectionLength, level);
        _setText(result.Text);

        return result;
    }

    public FormattingResult ApplyBulletList(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleBulletList(_getText(), selectionStart, selectionLength);
        _setText(result.Text);

        return result;
    }

    public FormattingResult ApplyOrderedList(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleOrderedList(_getText(), selectionStart, selectionLength);
        _setText(result.Text);

        return result;
    }

    public FormattingResult ApplyQuote(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.ToggleQuote(_getText(), selectionStart, selectionLength);
        _setText(result.Text);

        return result;
    }

    public FormattingResult ApplyCodeFence(int selectionStart, int selectionLength)
    {
        FormattingResult result = EditorFormatting.InsertCodeFence(_getText(), selectionStart, selectionLength, string.Empty);
        _setText(result.Text);

        return result;
    }

    public FormattingResult ApplyTable(int caretOffset)
    {
        FormattingResult result = EditorFormatting.InsertTable(_getText(), caretOffset);
        _setText(result.Text);

        return result;
    }

    public FormattingResult? ContinueList(int caretOffset)
    {
        FormattingResult? result = EditorFormatting.ContinueListItem(_getText(), caretOffset);

        if (result is null)
            return null;

        _setText(result.Text);

        return result;
    }

    public FormattingResult? RenumberOrderedListAfterDeletion(int offset)
    {
        FormattingResult? result = MarkdownListRenumberer.RenumberOrderedListAfterDeletion(_getText(), offset);

        if (result is null)
            return null;

        _setText(result.Text);

        return result;
    }
}
