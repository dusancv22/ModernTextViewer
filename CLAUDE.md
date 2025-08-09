# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ModernTextViewer is a Windows Forms application built with .NET 8.0 that provides a modern text viewing and editing experience with features including:
- Dark/light mode support with instant theme switching (<500ms)
- Auto-save functionality (5-minute intervals)
- Support for multiple file formats (.txt, .srt, .md, .markdown)
- Preview/raw mode toggle for markdown files with WebView2-powered HTML rendering
- Custom borderless window with P/Invoke-based dragging and resizing
- Font customization and zoom controls (Ctrl+Plus/Minus, Ctrl+Scroll)
- Find/Replace dialog with regex support
- Hyperlink management with Ctrl+K shortcut

## Build and Development Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# Build in Release mode
dotnet build -c Release

# Clean build artifacts
dotnet clean

# Publish for Windows (single file)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Architecture

The application follows a Model-View-Service pattern with these key components:

### Core Structure
- **Entry Point**: `Program.cs` - Initializes Windows Forms and launches MainForm
- **Main UI**: `src/Forms/MainForm.cs` (~2000 lines) - Central form handling all UI interactions, window management, keyboard shortcuts, and WebView2 integration
- **Data Model**: `src/Models/DocumentModel.cs` - Document state management including content, dirty tracking, preview mode state, and file metadata
- **Services**:
  - `FileService.cs` - Async file I/O with UTF-8 encoding (no BOM) and line ending normalization
  - `PreviewService.cs` - Markdown-to-HTML conversion with theme-aware CSS generation
  - `HyperlinkService.cs` - Hyperlink detection and management for rich text

### Window Management Architecture
- Custom borderless window using P/Invoke for `SendMessage` API
- Manual implementation of window dragging (title bar) and resizing (border detection)
- Resize border detection with 8-pixel zones on all edges
- Custom minimize/maximize/close buttons with hover effects

### WebView2 Integration
- Lazy initialization on first markdown preview request
- 10-second initialization timeout with error handling
- JavaScript injection for instant theme switching without page reloads
- Cached CSS generation for performance optimization
- Fallback to full HTML regeneration if JavaScript injection fails

## Key Implementation Details

### Critical Code Patterns

1. **Null Reference Warning**: Line 1350 in MainForm.cs has a CS8602 warning - needs null check
2. **Assembly Version Conflict**: WebView2 causes WindowsBase version conflicts (4.0.0.0 vs 5.0.0.0) - currently resolved by preferring 4.0.0.0
3. **Undo/Redo System**: Custom stack-based implementation tracking text, hyperlinks, and selection state
4. **Auto-save Timer**: 5-minute interval, only triggers when document.IsDirty is true
5. **Font Size Constraints**: 4pt minimum, 96pt maximum, enforced in zoom operations

### Event Handling Flow
1. Text changes trigger `TextBox_TextChanged` → sets document dirty → updates status bar
2. Keyboard shortcuts handled in `MainForm_KeyDown` with modifier key checks
3. WebView2 navigation events managed asynchronously with proper error boundaries
4. Timer-based debouncing for hyperlink updates (250ms delay)

### Theme System
- Dark mode colors: Background #1E1E1E, Foreground #DCDCDC, Toolbar #2D2D2D
- Light mode colors: System defaults (Control, ControlText)
- Preview mode uses CSS custom properties for instant switching
- JavaScript-based theme updates via `ExecuteScriptAsync()`

## Dependencies and Requirements

### NuGet Packages
- **Markdig v0.41.3**: Full-featured markdown parser with GitHub-flavored markdown support
- **Microsoft.Web.WebView2 v1.0.3351.48**: Chromium-based web view for HTML rendering

### System Requirements
- .NET 8.0 Windows Runtime
- Windows 10 version 1809+ (for WebView2)
- WebView2 Runtime (auto-installed on Windows 10/11)

## Common Development Tasks

### Adding New Keyboard Shortcuts
1. Add case in `MainForm_KeyDown` method
2. Check for modifier keys (e.Control, e.Shift, e.Alt)
3. Set `e.Handled = true` to prevent bubbling
4. Update README.md keyboard shortcuts table

### Modifying Theme Colors
1. Update color definitions in MainForm constructor
2. Modify `ApplyTheme()` method for UI elements
3. Update `PreviewService.GenerateCss()` for preview mode
4. Test theme switching in both raw and preview modes

### Working with WebView2
1. Always check `isWebViewInitialized` before WebView2 operations
2. Use try-catch around all WebView2 async operations
3. Implement timeouts for initialization (see `InitializeWebView()`)
4. Test with WebView2 Runtime uninstalled to verify error handling

### File Format Support
1. Check extension in `IsMarkdownFile()` method
2. Preview mode only activates for .md/.markdown files
3. All text operations work on any text-based file
4. Binary file detection not implemented - will corrupt binary files

## Known Issues and Workarounds

### Build Warnings
- **CS8602 at line 1350**: Possible null reference - add null check to `webView.CoreWebView2`
- **MSB3277**: Assembly version conflicts - safe to ignore, resolved at runtime

### WebView2 Initialization
- First-time initialization takes 2-3 seconds
- Timeout after 10 seconds triggers fallback to raw mode
- Memory usage increases by ~30-50MB when preview active

### Performance Considerations
- Large files (8000+ words) previously caused freezes - fixed in recent commits
- Preview mode CSS cached after first generation
- Theme switching optimized to <500ms using JavaScript injection

## Testing Checklist

When making changes, verify:
1. Dark/light mode switching works in both raw and preview modes
2. Keyboard shortcuts function correctly with proper modifier keys
3. Auto-save triggers after 5 minutes when document is dirty
4. Find/Replace works with case sensitivity and whole word options
5. Preview mode renders markdown correctly with tables, code blocks, lists
6. Window can be dragged by title bar and resized from all edges
7. Font dialog changes apply to text and persist across sessions
8. Hyperlinks open in default browser when clicked