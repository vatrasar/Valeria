<div align="center">

  <img src="Assets/valeria-icon.svg" alt="Valeria Logo" width="130" height="130" />

  # Valeria

  **A fast, distraction-free split-view Markdown editor for modern desktops.**  
  *Write clean Markdown on the left — see it beautifully rendered in real time on the right.*

  <br />

  [![Platform](https://img.shields.io/badge/Platform-Cross--Platform-10B981?style=for-the-badge)](#-native-desktop-experience)
  [![Performance](https://img.shields.io/badge/Performance-Lightning_Fast_%7E60MB_RAM-blue?style=for-the-badge&logo=speedtest&logoColor=white)](#-why-choose-valeria)
  [![Packaging](https://img.shields.io/badge/Package-.deb_Ready-A81D33?style=for-the-badge&logo=debian&logoColor=white)](#-installation)
  [![Privacy](https://img.shields.io/badge/Privacy-100%25_Offline_%26_Local-purple?style=for-the-badge)](#)

  <br />

  [Why Valeria?](#-why-choose-valeria) • [Key Features](#-key-features) • [Desktop Experience](#-native-desktop-experience) • [Shortcuts](#-handy-keyboard-shortcuts) • [Installation](#-installation) • [Customization](#-customization)

</div>

---

## 🌿 What is Valeria?

**Valeria** is a lightweight, high-performance desktop Markdown editor designed for writers, researchers, programmers, and note-takers. It offers the ideal dual-pane writing environment:

- **Source Editor (Left)**: Write plain, distraction-free Markdown in a clean monospace editor with syntax coloring. No awkward layout shifts or hidden formatting markers while typing — just pure, predictable writing like in your favorite code editor.
- **Live Preview (Right)**: Watch your words transform instantly into an elegantly styled document featuring rich typography, readable tables, interactive task lists, and syntax-highlighted code blocks.

---

## ⚡ Why Choose Valeria?

Most modern desktop note apps and Markdown editors (such as Obsidian, MarkText, or Notion) are built on heavy web browser engines (Electron/Chromium). While feature-rich, they often consume massive amounts of system memory, cause battery drain, and feel sluggish on older or resource-constrained machines.

**Valeria is engineered differently.** Built natively with **Avalonia UI** and **.NET**, it communicates directly with your graphics hardware without running a hidden browser in the background.

### 🌟 Benefits You Will Notice:

- 🪶 **Whisper-Light on Memory**: Idles at around **~60 MB of RAM** (compared to 400 MB to 1 GB in typical Electron-based apps). Keep it running all day without slowing down your computer.
- ⚡ **Instant Launch**: Starts up in the blink of an eye (< 300 ms) so you can capture fleeting thoughts immediately.
- 🔋 **Battery-Friendly**: Generates minimal CPU load while idle or typing, making it a great companion for laptops on the go.
- 🎯 **Smooth, Latency-Free Typing**: Keystrokes register with zero lag. Even in documents with thousands of lines, typing stays fluid and responsive.
- 🔒 **100% Private & Offline**: No account creation, no subscriptions, and no cloud sync sending your data elsewhere. Your notes are stored as standard Markdown files directly on your computer.

### 📊 Quick Comparison

| What Matters to You | **Valeria** | Heavy Web-Based Editors (Obsidian, MarkText) |
| :--- | :--- | :--- |
| **Memory Usage** | **~50 – 80 MB** (Extremely light) | 400 – 900+ MB (Heavy) |
| **Startup Time** | **Instant** (< 0.3s) | 2 – 5 seconds |
| **Battery & CPU Impact** | **Minimal** (Direct hardware rendering) | Noticeable (Chromium background processes) |
| **Typing Responsiveness** | **Immediate**, zero frame drops | Can stutter on large documents |
| **System Integration** | **Native desktop app** (Wayland / X11 / Windows), lightweight package | Bulky web runtime wrapper |
| **Privacy & Storage** | **Local files only**, zero telemetry | Varies; often cloud-connected or telemetry-heavy |

---

## 🐧 Native Desktop Experience

Valeria is designed to feel right at home in your desktop environment:

- **First-Class Wayland & X11**: Runs smoothly across modern display servers with crisp rendering and automatic HiDPI fractional scaling.
- **Ready-to-Install Package**: Quick installation via `.deb` package for Ubuntu, Debian, Linux Mint, and Pop!_OS.
- **Launcher & File Associations**: Appears in your application menu with a crisp icon and automatically associates with `.md` files to open with a double-click.
- **Terminal Friendly**: Launch documents directly from your shell:
  ```bash
  valeria my-notes.md
  ```
- **Clean Storage**: Respects user directory standards (`~/.local/share/valeria`), keeping your home folder clean.

---

## ✨ Key Features

### 📝 Dual-Pane Writing Flow
- **Adjustable Splitter**: Drag the central divider to balance editor and preview widths, or collapse the preview completely with a single click or shortcut (<kbd>Ctrl</kbd> + <kbd>P</kbd>) for distraction-free writing.
- **Live Styled Preview**: Headings, blockquotes, lists, strikethrough, and links format automatically as you type.

### 🔍 Find in Preview
- **Instant Search**: Press <kbd>Ctrl</kbd> + <kbd>F</kbd> to search directly within your rendered document.
- **Seamless Navigation**: Cycle between matches instantly using <kbd>F3</kbd> and <kbd>Shift</kbd> + <kbd>F3</kbd>.

### 🎨 Polyglot Code Blocks
- **50+ Languages Highlighted**: Formats code snippets cleanly across Python, C#, Rust, JavaScript, Go, Bash, SQL, JSON, YAML, and dozens more.
- **1-Click Copy**: Convenient copy button on code snippets to grab code without messy manual selection.

### ⌨️ Smart Typing Ergonomics
- **Automatic Lists**: Press <kbd>Ctrl</kbd> + <kbd>Enter</kbd> to continue bullet or numbered lists effortlessly.
- **Auto-Closing Pairs**: Automatically closes brackets `()`, `[]`, `{}`, quotes `""`, `''`, and code backticks.
- **Language Picker**: Typing code fences automatically suggests matching programming languages.

### 🖼️ Seamless Media & Tables
- **Images Built-in**: Supports web images (`https://`), local images from your hard drive, and embedded data images with intelligent caching for smooth scrolling.
- **Clean Tables**: Formatted tables with aligned columns, distinct header rows, and alternating row styling.
- **Interactive Checklists**: Full support for task lists (`- [x]` and `- [ ]`).
- **Clickable Links**: Hyperlinks open directly in your favorite default web browser.

### 🛠️ One-Click Formatting Toolbar
- Quick-access toolbar buttons for Bold, Italic, Strikethrough, Headers (H1–H3), Lists, Quotes, Code Fences, Links, and Tables.
- Live status bar shows caret position (`Ln X, Col Y`), real-time word count, and an unsaved changes indicator (`*`).

---

## ⌨️ Handy Keyboard Shortcuts

| Shortcut | What It Does |
| :--- | :--- |
| <kbd>Ctrl</kbd> + <kbd>O</kbd> | **Open File** from your computer |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | **Save** current document |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>S</kbd> | **Save As** to a new file |
| <kbd>Ctrl</kbd> + <kbd>P</kbd> or <kbd>Ctrl</kbd> + <kbd>E</kbd> | **Toggle Preview** (expand or collapse preview pane) |
| <kbd>Ctrl</kbd> + <kbd>F</kbd> | **Find in Preview** (open search bar) |
| <kbd>F3</kbd> / <kbd>Shift</kbd> + <kbd>F3</kbd> | Jump to **Next / Previous Match** |
| <kbd>Esc</kbd> | Close search bar |
| <kbd>Ctrl</kbd> + <kbd>B</kbd> | Format selected text as **Bold** |
| <kbd>Ctrl</kbd> + <kbd>I</kbd> | Format selected text as *Italic* |
| <kbd>Ctrl</kbd> + <kbd>Enter</kbd> | **Continue List** on the next line |

---

## 📦 Installation

### Ubuntu, Debian, Linux Mint & Pop!_OS (`.deb`)

1. Build or grab the package:
   ```bash
   ./build-deb.sh 1.0.0
   ```
2. Install with your package manager:
   ```bash
   sudo apt install ./build/valeria_1.0.0_amd64.deb
   ```
3. Start writing! Launch **Valeria** from your application menu or type `valeria` in terminal.

---

### Running from Source (.NET 10)

If you have the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed:

```bash
# Clone the repository
git clone https://github.com/vatrasar/valeria.git
cd valeria

# Run Valeria
dotnet run --project project/Valeria.csproj
```

---

## ⚙️ Customization

You can personalize your writing environment by editing `project/appsettings.json`:

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

- **`FontSize`**: Adjust your preferred writing font size.
- **`TabWidth`**: Indentation width (in spaces).
- **`PreviewDebounceMilliseconds`**: How quickly the preview refreshes while you type (default is 350 ms for an optimal balance of smoothness and speed).
- **`MaxWidth`**: Limits the reading column width for comfortable reading on wide monitors.
- **`CodeBlockMaxHeight`**: Max height of code snippets before adding a scrollbar.

---

## 🗺️ What's Next?

- [ ] Synchronized side-by-side scrolling (scroll editor and preview together)
- [ ] Light / Dark theme selector
- [ ] Document Outline / Table of Contents sidebar
- [ ] Export to PDF and HTML

---

<div align="center">
  <sub>Fast, focused, and distraction-free Markdown writing.</sub>
</div>
