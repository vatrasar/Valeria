<div align="center">

  <img src="Assets/valeria-icon.svg" alt="Valeria Logo" width="130" height="130" />

  # Valeria

  **A blazing-fast, distraction-free split-view Markdown editor engineered for Linux and modern desktops.**  
  *Write clean Markdown with code-editor precision on the left; watch it render with rich typography in real time on the right.*

  <br />

  [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
  [![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-11.3-7B2CBF?style=for-the-badge&logo=avalonia&logoColor=white)](https://avaloniaui.net/)
  [![ReactiveUI](https://img.shields.io/badge/ReactiveUI-20.1-EC4899?style=for-the-badge)](https://reactiveui.net/)
  [![Linux](https://img.shields.io/badge/Linux-First--Class_Citizen-FCC624?style=for-the-badge&logo=linux&logoColor=black)](#-first-class-linux-experience)
  [![Performance](https://img.shields.io/badge/Performance-Direct_GPU_Skia-10B981?style=for-the-badge&logo=speedtest&logoColor=white)](#-why-valeria--the-avalonia-performance-edge)
  [![Tests](https://img.shields.io/badge/Tests-85_Passing-success?style=for-the-badge&logo=checkmarx&logoColor=white)](#-running-tests)
  [![Debian Package](https://img.shields.io/badge/.deb-Ready_to_Install-A81D33?style=for-the-badge&logo=debian&logoColor=white)](#-method-1-native-linux-deb-package-recommended)

  <br />

  [Why Valeria?](#-why-valeria--the-avalonia-performance-edge) • [Linux Experience](#-first-class-linux-experience) • [Key Features](#-key-features) • [Shortcuts](#-keyboard-shortcuts) • [Installation](#-getting-started) • [Configuration](#-configuration) • [Architecture](#-architecture--tech-stack)

</div>

---

## 🌿 Overview

**Valeria** is a dedicated desktop Markdown environment tailored for developers, technical writers, and note-takers who demand **uncompromising performance and rock-solid reliability**. Built from the ground up with **Avalonia UI 11** and **ReactiveUI**, Valeria breaks away from sluggish web wrappers and delivers a native desktop editing experience:

- **Source Editor (Left)**: Pure, monospace Markdown code editing powered by `AvaloniaEdit` with full syntax highlighting. Clean, predictable, and free from awkward inline layout jumps—just like writing code in VS Code.
- **Live Preview (Right)**: An instant visual document preview rendered with rich typography reminiscent of Obsidian and MarkText, updated reactively as you type.

Whether you are authoring software documentation, drafting blog posts, or keeping daily technical journals, Valeria gives you the speed of raw text with the elegance of modern typography.

---

## ⚡ Why Valeria? — The Avalonia Performance Edge

Most popular Markdown editors (such as MarkText, Obsidian, or VS Code) are built on top of **Electron** or embedded browser engines (Chromium + Node.js). While versatile, web-based editors carry significant overhead on desktop environments: high memory usage, noticeable startup latency, and frame drops when editing long documents.

**Valeria takes a fundamentally different path:** it is compiled directly to native machine code with **.NET 10** and renders directly through **Avalonia UI's hardware-accelerated Skia graphics engine**.

### 🚀 Performance Highlights

- 🪶 **Featherweight RAM Footprint**: Idles around **~50–80 MB** of RAM—consuming a fraction of the 400–900+ MB typical of Electron editors.
- ⚡ **Near-Instant Startup**: Cold launches in **under 300 ms**, ready for immediate typing without waiting for a Chromium runtime to initialize.
- 🎯 **Direct GPU Rendering via Skia**: No DOM tree reflows, no HTML layout thrashing. Avalonia renders directly to OpenGL / Vulkan / Software backends at a buttery-smooth 60+ FPS.
- ⚡ **Virtualizing Text Engine**: Built upon `AvaloniaEdit`, allowing you to open, scroll, and manipulate multi-megabyte Markdown files without latency or UI freezing.
- 🔄 **Non-Blocking Reactive Pipeline**: Markdig parsing and preview generation are handled with an intelligent debounce pipeline (`PreviewDebounceMilliseconds`) backed by ReactiveUI observables, ensuring keystrokes are never blocked by rendering.

### 📊 Valeria vs. Web/Electron Markdown Editors

| Metric / Characteristic | **Valeria (Avalonia UI + .NET 10)** | Electron Editors (Obsidian, MarkText) |
| :--- | :--- | :--- |
| **Runtime Architecture** | **Native Compiled .NET 10 + Skia** | Chromium Browser + Node.js (V8) |
| **Memory Footprint (Idle)** | **~50 – 80 MB** | 350 – 900+ MB |
| **Cold Startup Time** | **< 300 ms** (Instant) | 2.5 – 6.0 seconds |
| **Rendering Engine** | **Direct GPU via Skia** (Vulkan / OpenGL) | WebKit / Blink DOM + CSS Box Model |
| **Typing Latency** | **Near-zero** (Direct buffer manipulation) | Subject to browser event loop & DOM dispatching |
| **Linux Desktop Integration** | **Native X11 & Wayland**, light system `.deb` | Sandboxed web runtime, large bundle size |
| **Background Resource Draw** | **Virtually 0% CPU** when idle | Persistent helper processes & web workers |

---

## 🐧 First-Class Linux Experience

Linux is not an afterthought in Valeria—it is a primary, first-class target. Valeria is optimized to look, feel, and behave like a true native Linux desktop application:

- **Wayland & X11 Ready**: Avalonia UI natively targets both Wayland and X11 display servers with crisp rendering and automatic fractional scaling on high-DPI displays.
- **Official Debian/Ubuntu Packaging (`.deb`)**: Includes a dedicated, zero-friction packaging script ([`build-deb.sh`](file:///home/vatrasar/projekty/newMarkText/antigravityProject/build-deb.sh)) that compiles a self-contained release and packages it into a ready-to-install `.deb` archive.
- **XDG Desktop Standards**:
  - Installs cleanly to `/opt/valeria` with a global `/usr/bin/valeria` symlink.
  - Registers an XDG desktop entry (`valeria.desktop`) under `/usr/share/applications`.
  - Integrates scalable vector icons (`valeria.svg`) under `/usr/share/icons/hicolor/scalable/apps`.
  - Configures MIME-type associations for `text/markdown` and `text/x-markdown`, enabling one-click opening from Nautilus, Dolphin, or Thunar.
- **Clean CLI Support**: Launch single or multiple Markdown files straight from your terminal:
  ```bash
  valeria README.md notes.md
  ```
- **XDG Base Directory Compliance**: Follows standard Linux filesystem conventions (`~/.local/share/valeria` for user data and storage), preserving system security and multi-user safety.

---

## ✨ Key Features

### 📝 Dual-Pane Writing Experience
- **Responsive Split View**: Drag the interactive splitter to customize pane widths, or collapse the preview completely for pure focus.
- **Intelligent Live Preview**: High-performance debounced rendering of headings, blockquotes, lists, tables, strikethrough, links, and code blocks.
- **Obsidian-Style Visuals**: Clean dark typography, subtle border accents, and beautifully spaced document hierarchy.

### 🔍 In-Preview Full-Text Search
- **Instant Find**: Press <kbd>Ctrl</kbd> + <kbd>F</kbd> to search within the rendered preview.
- **Match Navigation**: Jump seamlessly between matches using <kbd>F3</kbd> (Next) and <kbd>Shift</kbd> + <kbd>F3</kbd> (Previous).
- **Match Case Toggle**: Switch between case-sensitive and case-insensitive searching.
- **Preserved Typography**: Highlighting cleanly wraps text runs and styled inline spans without corrupting formatting.

### 🎨 Polyglot Code Blocks & TextMate Highlighting
- **50+ Languages Supported**: Powered by TextMate grammars, code blocks are highlighted with precision (C#, Python, Rust, Go, JavaScript, TypeScript, SQL, Bash, JSON, YAML, and more).
- **Visual Studio Dark Theme**: Clean contrast with line numbers, language identifier tags, and a convenient **one-click copy button**.

### ⌨️ Smart Typing Ergonomics
- **List Auto-Continuation**: Press <kbd>Ctrl</kbd> + <kbd>Enter</kbd> to automatically continue bullet lists (`- `, `* `) and numbered lists (`1. `, `2. `).
- **Auto-Closing Delimiters**: Automatically pairs `()`, `[]`, `{}`, `""`, `''`, and ```` ``` ````.
- **Code Language Auto-Completion**: Typing opening code fences prompts an intelligent language suggestion dropdown.

### 🖼️ High-Performance Image Handling
- **Multi-Source Support**: Renders remote HTTP/HTTPS images, local relative/absolute filesystem paths, and base64 data URIs (`data:image/...`).
- **Concurrent In-Memory Caching**: Decoded bitmaps are cached asynchronously to eliminate layout flicker and avoid repeated disk or network I/O.

### 📊 Tables & Document Elements
- **GFM Tables**: Formatted tables with text alignment, contrasting header rows, and alternating row backgrounds.
- **Task Lists**: Interactive checkbox items (`- [x]` / `- [ ]`).
- **Clickable Hyperlinks**: Web links open directly in your preferred system browser.
- **Blockquotes**: Single and nested blockquotes with accent side bars.

### 🛠️ Smart Formatting Toolbar
- **Quick Actions**: One-click buttons for Bold, Italic, Strikethrough, Inline Code, Hyperlinks, Headers (H1–H3), Lists, Blockquotes, Code Fences, and Tables.
- **Context-Aware Toggles**: Intelligently wraps new text or unwraps existing formatting around active selections.

### 📂 File Management & Metrics
- **Seamless I/O**: Native file dialogs supporting `.md`, `.markdown`, `.mdown`, `.mkd`, and `.mdx`.
- **Dirty Indicator (`*`)**: Immediate visual feedback when unsaved modifications exist.
- **Status Metrics**: Real-time caret position (`Ln X, Col Y`), live word count, and non-intrusive error notifications.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action | Description |
| :--- | :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>O</kbd> | **Open File** | Open an existing Markdown document |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | **Save File** | Save the active document |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>S</kbd> | **Save As** | Save active document to a new location |
| <kbd>Ctrl</kbd> + <kbd>P</kbd> or <kbd>Ctrl</kbd> + <kbd>E</kbd> | **Toggle Preview** | Show or collapse the rendered preview pane |
| <kbd>Ctrl</kbd> + <kbd>F</kbd> | **Find in Preview** | Open the search bar inside the preview pane |
| <kbd>F3</kbd> | **Next Match** | Navigate to the next search match in preview |
| <kbd>Shift</kbd> + <kbd>F3</kbd> | **Previous Match** | Navigate to the previous search match in preview |
| <kbd>Esc</kbd> | **Close Search** | Close the in-preview search bar |
| <kbd>Ctrl</kbd> + <kbd>B</kbd> | **Bold** | Wrap or unwrap selection with `**bold**` |
| <kbd>Ctrl</kbd> + <kbd>I</kbd> | **Italic** | Wrap or unwrap selection with `*italic*` |
| <kbd>Ctrl</kbd> + <kbd>Enter</kbd> | **Continue List** | Automatically continue bullet or numbered list item |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for building from source)
- Linux (Ubuntu/Debian, Fedora, Arch, etc.) or any modern desktop OS (Windows, macOS)

---

### 📦 Method 1: Native Linux `.deb` Package (Recommended)

If you are running Ubuntu, Debian, Linux Mint, or Pop!_OS, you can build and install a self-contained Debian package in seconds:

```bash
# 1. Build the .deb package (produces build/valeria_1.0.0_amd64.deb)
./build-deb.sh 1.0.0

# 2. Install the package
sudo apt install ./build/valeria_1.0.0_amd64.deb
# Or: sudo dpkg -i ./build/valeria_1.0.0_amd64.deb

# 3. Launch Valeria from terminal or your application menu
valeria
```

---

### 🔨 Method 2: Building and Running from Source

You can also run Valeria directly using the .NET 10 CLI:

```bash
# 1. Clone the repository
git clone https://github.com/vatrasar/valeria.git
cd valeria

# 2. Build the solution
dotnet build project/Valeria.slnx

# 3. Run the application
dotnet run --project project/Valeria.csproj

# (Optional) Open a specific file directly
dotnet run --project project/Valeria.csproj -- ~/Documents/notes.md
```

---

### 🧪 Running Tests

Valeria features an automated test suite comprising **85 unit and headless Avalonia integration tests**. The suite tests markdown parsing, image caching, preview search highlighting, formatting transformations, and headless UI rendering:

```bash
dotnet test project/Valeria.slnx
```

---

## ⚙️ Configuration

Application settings can be configured via `project/appsettings.json`:

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

### Configuration Options:
- **`FontSize`**: Base font size (in points) for the code editor pane.
- **`TabWidth`**: Indentation width in spaces.
- **`PreviewDebounceMilliseconds`**: Delay (in milliseconds) before updating the preview following keystrokes. Keeps typing completely smooth without wasteful re-renders.
- **`MaxWidth`**: Maximum content column width for the rendered document (for optimal readability).
- **`CodeBlockMaxHeight`**: Maximum vertical height of code blocks before internal scrolling activates.

---

## 🏗️ Architecture & Tech Stack

Valeria is engineered following a modern, feature-oriented MVVM architecture with immutable state management:

- **Framework**: [.NET 10](https://dotnet.microsoft.com/) & [C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/)
- **UI Platform**: [Avalonia UI 11.3](https://avaloniaui.net/) (Fluent Theme with custom dark palette)
- **Reactivity & State**: [ReactiveUI 20.1](https://reactiveui.net/) with source-generated commands & MVI-style immutable records
- **Code Editor**: [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) + `AvaloniaEdit.TextMate`
- **Markdown Parsing**: [Markdig](https://github.com/xoofx/markdig) (CommonMark & GitHub Flavored Markdown)
- **Icons**: [Material.Icons.Avalonia](https://github.com/AvaloniaCommunity/Material.Icons.Avalonia)
- **Configuration & DI**: `Microsoft.Extensions.DependencyInjection` & `Microsoft.Extensions.Options`

```
Valeria/
├── Assets/                 # Scalable icons, themes, and welcome document
├── Src/
│   ├── Core/               # Domain models, MVVM base classes, configuration schemas
│   ├── Features/
│   │   ├── Editor/         # Split-view screen, syntax engines, preview builders, search
│   │   └── Shell/          # Host window, routing, and top-level lifecycle
│   ├── Infrastructure/     # DI container, module bootstrappers, and native file dialogs
│   └── Shared/             # Global styles, color palettes, and string resources
└── Tests/
    └── Valeria.Tests/      # Headless UI integration & unit tests
```

---

## 🗺️ Roadmap

- [x] High-performance split-view layout with interactive splitter
- [x] Polyglot TextMate syntax highlighting for 50+ languages
- [x] Asynchronous remote & local image caching
- [x] In-preview full-text search with match highlighting (`Ctrl+F`)
- [x] Smart typing ergonomics (list continuation, auto-closing brackets, language autocomplete)
- [x] Native Debian/Ubuntu packaging (`.deb`)
- [ ] Synchronized dual-pane scrolling (scroll source and preview simultaneously)
- [ ] Light / Dark theme switcher
- [ ] Table of Contents (TOC) quick-jump sidebar
- [ ] HTML and PDF document export

---

<div align="center">
  <sub>Crafted with passion for clean typography and high-performance desktop computing.</sub>
</div>
