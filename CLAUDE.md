# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ModernTextViewer is a Windows Forms application built with .NET 8.0 that provides a modern text viewing and editing experience with features including:
- Dark/light mode support
- Auto-save functionality
- Support for multiple file formats (.txt, .srt, .md, .markdown)
- Preview/raw mode toggle for markdown files with real-time HTML rendering
- Custom window controls and styling
- Font size adjustments via keyboard shortcuts
- WebView2-powered markdown preview with theme-aware styling

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
```

## Architecture

The application follows a Model-View-Service pattern:

- **Entry Point**: `Program.cs` - Initializes Windows Forms and launches `MainForm`
- **Main UI**: `src/Forms/MainForm.cs` - The primary form handling all UI interactions, window management, user input, and WebView2 integration for markdown preview
- **Data Model**: `src/Models/DocumentModel.cs` - Manages document state, content, dirty tracking, and preview mode state
- **File Operations**: `src/Services/FileService.cs` - Handles async file loading/saving with proper encoding and line ending normalization
- **Preview Service**: `src/Services/PreviewService.cs` - Converts markdown to HTML with theme-aware styling for WebView2 rendering
- **Custom Controls**: `src/Controls/CustomToolbar.cs` - Custom toolbar implementations

## Key Implementation Details

1. **Window Management**: Uses P/Invoke for custom window handling (dragging, resizing)
2. **Auto-save**: Timer-based auto-save every 5 minutes when document is dirty
3. **File Handling**: UTF-8 encoding without BOM, normalizes line endings to system default
4. **Font Scaling**: Ctrl+Plus/Minus for zoom, range 4-96pt
5. **Dark Mode**: Default enabled, toggleable via UI button
6. **Preview Mode**: Real-time markdown-to-HTML conversion using Markdig library
7. **WebView2 Integration**: Lazy initialization for markdown preview with comprehensive error handling
8. **Theme Synchronization**: Preview mode automatically adapts to current dark/light theme

## Dependencies

The project uses the following key dependencies:
- **Markdig v0.41.3**: Markdown parsing and HTML conversion with advanced extensions support
- **Microsoft.Web.WebView2 v1.0.3351.48**: Modern web browser control for HTML rendering in preview mode

### System Requirements
- .NET 8.0 Windows Runtime
- WebView2 Runtime (automatically installed on Windows 10/11, or can be downloaded from Microsoft)
- Windows 10 version 1809 or later (for WebView2 support)

## Testing

Currently no automated tests are configured. Manual testing recommended for:
- File operations (open, save, auto-save)
- UI responsiveness and dark/light mode switching
- Keyboard shortcuts
- Window resize and drag operations
- **Preview Mode Functionality**:
  - Toggle between raw and preview modes for markdown files (.md, .markdown)
  - Content synchronization when switching between modes
  - Theme switching while in preview mode
  - WebView2 initialization and error handling
  - Markdown rendering accuracy with various content types (headers, lists, code blocks, tables, etc.)

## Troubleshooting

### WebView2 Issues

**Problem**: Preview mode fails to initialize or shows blank content
- **Solution**: Ensure WebView2 Runtime is installed. Download from Microsoft's official website if missing.
- **Diagnostic**: Check Windows Event Viewer for WebView2-related errors.

**Problem**: Preview mode times out during initialization
- **Cause**: WebView2 initialization timeout (10-second limit) or system configuration issues.
- **Solution**: Restart the application. If problem persists, reinstall WebView2 Runtime.

**Problem**: Preview not updating when switching themes
- **Cause**: WebView2 CoreWebView2 not properly initialized.
- **Solution**: Switch back to raw mode and then to preview mode again.

### File Format Support

**Markdown Preview**: Only available for files with `.md` or `.markdown` extensions.
**Fallback Behavior**: Non-markdown files automatically stay in raw text mode.

### Performance Considerations

**Large Files**: WebView2 initialization adds ~2-3 seconds for first-time preview mode activation.
**Memory Usage**: Preview mode uses additional memory for WebView2 process (~30-50MB).
**Optimization**: CSS and markdown pipeline are cached for better performance.

## Agent Usage Guidelines

This project includes a comprehensive agent system for specialized development tasks. Full documentation is available in `agents.md`.

### How to Use Agents

When you need specialized assistance, use the Task tool with the appropriate `subagent_type` parameter. For example:
- Use `architect` agent for planning new features or breaking down complex tasks
- Use `code-writer` agent for implementing features in C# and Windows Forms
- Use `debugger` agent for investigating issues in the application
- Use `test-writer` agent for creating test cases (when testing framework is established)
- Use `web-search` agent for finding online documentation, tutorials, or current best practices

### Quick Agent Reference for ModernTextViewer Development

| Agent | Primary Use Case | Example Tasks |
|-------|------------------|---------------|
| **architect** | Planning features | "Plan implementation of find/replace functionality" |
| **code-writer** | C# implementation | "Add keyboard shortcut for find dialog", "Implement text search highlighting" |
| **code-reviewer** | Quality checks | "Review the new file handling implementation" |
| **debugger** | Bug fixes | "Fix auto-save not triggering", "Investigate UI freezing issue" |
| **documentation-writer** | Documentation | "Update README with new features", "Add XML comments to public methods" |
| **git-manager** | Version control | "Create commit for find/replace feature" |
| **web-search** | Online research | "Find Windows Forms best practices", "Search for .NET 8 text editor examples" |

### Common Workflows for ModernTextViewer

1. **Adding New Features:**
   - Start with `architect` to plan the implementation
   - Use `code-writer` to implement in MainForm.cs or create new forms/services
   - Have `code-reviewer` check the implementation
   - Use `git-manager` for commits

2. **Fixing Bugs:**
   - Use `debugger` to investigate the issue
   - Use `code-writer` to implement the fix
   - Have `code-reviewer` verify the solution

3. **UI Enhancements:**
   - Use `architect` for complex UI changes
   - Use `code-writer` for Windows Forms implementation
   - Consider dark/light mode compatibility

4. **Researching Solutions:**
   - Use `web-search` to find current documentation or examples
   - Use `web-search` for Windows Forms best practices
   - Use `web-search` when encountering unfamiliar .NET APIs or error messages

### Best Practices

- Always start with `architect` for features requiring multiple file changes
- Use `code-writer` for all C# and Windows Forms implementations
- Ensure compatibility with existing features (dark mode, auto-save, keyboard shortcuts)
- Follow the established Model-View-Service pattern
- Consider P/Invoke implications for custom window handling features

For complete agent documentation and advanced usage, refer to `agents.md`.