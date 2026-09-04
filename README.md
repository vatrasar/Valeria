# Valeria

Valeria is a dark split-view markdown editor for the desktop, built with Avalonia UI and ReactiveUI.
Write markdown with syntax coloring on the left, read the live styled preview on the right.

## Features

### Editor (split markdown editing)

- **Split view**: AvaloniaEdit source pane with a custom `Markdown.xshd` highlighting definition
  (headings, bold, italic, strikethrough, inline code, code fences, links, lists, quotes, rules),
  and a live preview pane rendered from the parsed document.
- **Live preview**: headings (H1–H6), emphasis, inline code, fenced code blocks, blockquotes
  (including nested), bullet/numbered/task lists, pipe tables with column alignment,
  horizontal rules, links (click to open in browser) and image placeholders.
- **Code blocks**: read-only embedded editors with TextMate grammars (50+ language aliases
  including C#, Python, JavaScript/TypeScript, AXAML/XAML, JSON, SQL, Rust, Go and more),
  dark Visual Studio like theme, line numbers, language label and copy-to-clipboard button.
- **File operations**: open, save and save-as for `.md`/`.markdown`/`.mdown`/`.mkd`/`.mdx`
  files through native dialogs, dirty (`*`) indicator, bundled welcome document on first launch.
- **Formatting toolbar**: bold, italic, strikethrough, inline code, link, headings H1–H3,
  bulleted and numbered lists, quote, code fence and starter table insertion.
  Toggles unwrap when the selection is already formatted.
- **Preview toggle and splitter**: hide the preview pane, drag the divider between panes
  (both panes keep a minimum width).
- **Status bar**: document title, error messages, caret position (`Ln X, Col Y`) and word count.
- **Keyboard shortcuts**: `Ctrl+O` open, `Ctrl+S` save, `Ctrl+Shift+S` save as,
  `Ctrl+B` bold, `Ctrl+I` italic, `Ctrl+P` toggle preview.
- **Rendering**: debounced preview refresh (configurable), empty-document hint overlay.

### Shell (application shell)

- Main window owning the ReactiveUI router (`IScreen`), routed view outlet and automatic
  window title tracking (`document - Valeria`).
- Navigates to the editor screen on startup. Dark theme only.

## Tech stack

- .NET 10, C# (nullable enabled)
- Avalonia UI 11 (Fluent theme, dark variant), ReactiveUI with source generators
- AvaloniaEdit + TextMate grammars for code highlighting, Markdig for markdown parsing
- Microsoft.Extensions.DependencyInjection / Configuration / Options
- xUnit test suite (parser, formatting, statistics, preview builder on headless Avalonia)

## How to run

Requirements: .NET 10 SDK.

```bash
dotnet build project/Valeria.slnx
./project/bin/Debug/net10.0/Valeria
```

Run tests:

```bash
dotnet test project/Valeria.slnx
```

## Configuration

`project/appsettings.json` (optional, defaults are built in):

```json
{
  "Valeria": {
    "Editor": {
      "FontSize": 14,
      "TabWidth": 4,
      "PreviewDebounceMilliseconds": 350
    },
    "Preview": {
      "MaxWidth": 860,
      "CodeBlockMaxHeight": 420
    }
  }
}
```

## Project layout

- `project/Src/Features/Editor` — editor screen, preview builder, file and syntax services
- `project/Src/Features/Shell` — main window and routing
- `project/Src/Core` — MVVM base classes, configuration model, markdown parser IR
- `project/Src/Infrastructure` — dependency injection, navigation bootstrapper, file dialogs
- `project/Src/Shared` — dark color palette, fonts, shared strings
- `project/Assets/Welcome.md` — sample document shown on first launch
- `project/Tests/Valeria.Tests` — xUnit test suite

## Current limitations

- Dark theme only, single document at a time.
- Remote images render as placeholders (no downloading yet).
- No scroll synchronization between source and preview panes.
