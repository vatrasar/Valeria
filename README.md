<div align="center">

  <img src="Assets/valeria-icon.svg" alt="Valeria Logo" width="130" height="130" />

  # Valeria

  **A modern, distraction-free split-view Markdown editor for desktop.**  
  *Write clean Markdown on the left, see it flourish in real time on the right.*

  <br />

  [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
  [![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.3-7B2CBF?style=for-the-badge&logo=avalonia&logoColor=white)](https://avaloniaui.net/)
  [![ReactiveUI](https://img.shields.io/badge/ReactiveUI-20.1-EC4899?style=for-the-badge)](https://reactiveui.net/)
  [![Platform](https://img.shields.io/badge/Platform-Cross--Platform-10B981?style=for-the-badge)](#)

  <br />

  [Overview](#-overview) • [Key Features](#-key-features) • [Shortcuts](#-keyboard-shortcuts) • [Getting Started](#-getting-started) • [Configuration](#-configuration) • [Roadmap](#-roadmap)

</div>

---

## 🌿 Overview

**Valeria** is a dedicated desktop Markdown environment designed for developers, writers, and technical note-takers. Built with **Avalonia UI** and **ReactiveUI**, it offers a refined split-pane workflow:

- **Source Editor (Left)**: Focused, distraction-free code editing powered by `AvaloniaEdit` with full syntax highlighting.
- **Live Preview (Right)**: An instant, elegantly rendered visual document preview updated reactively as you type.

Whether drafting documentation, crafting technical blog posts, or keeping daily dev notes, Valeria blends the speed of plain text with the beauty of rich typography.

---

## ✨ Key Features

### 📝 Dual-Pane Writing Experience
- **Responsive Split View**: Drag the interactive splitter to adjust your workspace, or collapse the preview completely for pure focus.
- **Intelligent Live Preview**: High-performance debounced rendering of headings, blockquotes, lists, strikethrough, tables, and links.
- **Rich Document Elements**: Complete support for task lists, nested blockquotes, clickable web links, and horizontal dividers.

### 🎨 Polyglot Code Blocks
- **50+ Languages Supported**: Powered by TextMate grammars, code blocks are highlighted with precision (C#, Python, Rust, Go, JavaScript, TypeScript, SQL, JSON, YAML, and more).
- **Embedded Visual Studio Dark Theme**: Clean contrast with line numbers, language labels, and a convenient **one-click copy button**.

### 🛠️ Smart Formatting Toolbar
- **Quick-Access Actions**: Dedicated toolbar controls for bold, italics, strikethrough, inline code, links, headers (H1–H3), lists, quotes, code fences, and tables.
- **Context-Aware Toggles**: Intelligently wraps new text or unwraps existing formatting around active selections.

### 📂 File Management & Metrics
- **Seamless I/O**: Native file dialogs with support for `.md`, `.markdown`, `.mdown`, `.mkd`, and `.mdx`.
- **Dirty Indicator (`*`)**: Always know when changes need saving.
- **Real-Time Status Metrics**: Live caret position tracking (`Ln X, Col Y`), word counts, and error feedback at a glance.

---

## ⌨️ Keyboard Shortcuts

Speed up your workflow with standard editor hotkeys:

| Shortcut | Action | Description |
| :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>O</kbd> | **Open File** | Open an existing Markdown document |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | **Save** | Save the active document |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>S</kbd> | **Save As** | Save document under a new path |
| <kbd>Ctrl</kbd> + <kbd>B</kbd> | **Bold** | Toggle bold formatting on selection |
| <kbd>Ctrl</kbd> + <kbd>I</kbd> | **Italic** | Toggle italic formatting on selection |
| <kbd>Ctrl</kbd> + <kbd>P</kbd> | **Toggle Preview** | Show or hide the rendered preview pane |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build and Run

1. Clone or open the repository.
2. Build and launch the application:

```bash
# Build the project
dotnet build project/Valeria.slnx

# Run the app
./project/bin/Debug/net10.0/Valeria
```

### Running Tests
Valeria includes a comprehensive unit test suite covering markdown parsing, formatting transformations, document metrics, and headless UI rendering:

```bash
dotnet test project/Valeria.slnx
```

---

## ⚙️ Configuration

Custom parameters can be tuned in `project/appsettings.json`:

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

- **`FontSize`**: Base font size inside the code editor pane.
- **`TabWidth`**: Indentation width in spaces.
- **`PreviewDebounceMilliseconds`**: Delay before updating the preview following a keystroke (optimizes typing responsiveness).
- **`MaxWidth`**: Maximum content column width for the rendered document.
- **`CodeBlockMaxHeight`**: Maximum vertical height of code blocks before scrolling.

---

## 🏗️ Architecture & Tech Stack

The project follows a decoupled, feature-driven MVVM architecture:

- **Core Framework**: [.NET 10](https://dotnet.microsoft.com/) & [C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/)
- **UI & Controls**: [Avalonia UI 11](https://avaloniaui.net/) (Fluent Theme)
- **Reactivity & Routing**: [ReactiveUI](https://reactiveui.net/) with source-generated commands & properties
- **Text Engine**: [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) + `AvaloniaEdit.TextMate`
- **Markdown Parser**: [Markdig](https://github.com/xoofx/markdig)
- **Dependency Injection & Options**: `Microsoft.Extensions.DependencyInjection` & `Microsoft.Extensions.Options`

```
Valeria/
├── Assets/           # App icons, splash assets, and bundled welcome document
├── Src/
│   ├── Core/         # Domain models, MVVM base classes, configuration schemas
│   ├── Features/
│   │   ├── Editor/   # Split-view screen, syntax engines, preview builders
│   │   └── Shell/    # Application host screen, navigation, and top-level window
│   ├── Infrastructure/ # DI registrations, bootstrapper, and native file dialogs
│   └── Shared/       # Global styles, color tokens, and resources
└── Tests/            # Unit & headless Avalonia integration tests
```

---

## 🗺️ Roadmap

- [ ] Synchronized dual-pane scrolling (scroll source and preview simultaneously)
- [ ] Asynchronous remote image downloading & caching
- [ ] Light / Dark theme toggle
- [ ] Table of Contents (TOC) quick jump sidebar
- [ ] PDF and HTML export options
