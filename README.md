# 📝 ModernTextViewer

A modern, feature-rich text editor for Windows with a clean interface, dark mode support, and powerful text manipulation capabilities.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

## ✨ Features

### 🎨 Modern Interface
- **Dark/Light Mode**: Toggle between dark and light themes for comfortable viewing
- **Custom Window Controls**: Sleek, borderless window design with custom minimize, maximize, and close buttons
- **Responsive Design**: Smooth resizing and window management

### 📝 Text Editing
- **Rich Text Formatting**: Apply bold (Ctrl+B), italic (Ctrl+I), and underline (Ctrl+U) formatting
- **Font Customization**: Full font dialog with family, size, style, and color options
- **Zoom Control**: Quickly adjust text size with Ctrl+Plus/Minus or mouse wheel (Ctrl+Scroll)
- **Find & Replace**: Powerful search and replace functionality (Ctrl+F) with options for:
  - Case-sensitive search
  - Whole word matching
  - Replace all occurrences

### 🔗 Advanced Features
- **Hyperlink Support**: Add, edit, and navigate hyperlinks (Ctrl+K)
- **Auto-Save**: Automatic saving every 5 minutes with visual status indicator
- **Multi-Format Support**: Open and save .txt, .srt (subtitle), and .md (markdown) files
- **Drag & Drop**: Simply drag files into the window to open them
- **Undo/Redo**: Standard undo functionality (Ctrl+Z)

### ⌨️ Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| Save As | Ctrl+S (when no file is open) |
| Quick Save | Ctrl+S (when file is open) |
| Find/Replace | Ctrl+F |
| Add/Edit Hyperlink | Ctrl+K |
| Bold | Ctrl+B |
| Italic | Ctrl+I |
| Underline | Ctrl+U |
| Undo | Ctrl+Z |
| Zoom In | Ctrl+Plus or Ctrl+Mouse Wheel Up |
| Zoom Out | Ctrl+Minus or Ctrl+Mouse Wheel Down |
| Select All | Ctrl+A |
| Cut | Ctrl+X |
| Copy | Ctrl+C |
| Paste | Ctrl+V |

## 🚀 Getting Started

### System Requirements
- Windows 10 or later
- .NET 8.0 Runtime (will be prompted to install if not present)

### Installation

#### Option 1: Download Release (Recommended)
1. Go to the [Releases](https://github.com/yourusername/ModernTextViewer/releases) page
2. Download the latest `ModernTextViewer.exe`
3. Run the application - no installation required!

#### Option 2: Build from Source
Prerequisites:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 or later
- Visual Studio 2022 (optional, but recommended)

```bash
# Clone the repository
git clone https://github.com/yourusername/ModernTextViewer.git
cd ModernTextViewer

# Build the project
dotnet build -c Release

# Run the application
dotnet run
```

The compiled executable will be in `bin\Release\net8.0-windows\`

## 📖 Usage Guide

### Opening Files
1. **Drag and Drop**: Simply drag any supported file into the window
2. **Using Save Dialog**: Click the 💾+ button to open a file through the save dialog

### Text Formatting
1. Select the text you want to format
2. Use keyboard shortcuts (Ctrl+B/I/U) or the font button (A) in the toolbar
3. For advanced formatting, click the font button to open the font dialog

### Working with Hyperlinks
1. Select text you want to turn into a hyperlink
2. Press Ctrl+K or click the 🔗 button
3. Enter the URL in the dialog
4. Click on hyperlinks to open them in your default browser

### Find and Replace
1. Press Ctrl+F to open the Find/Replace dialog
2. Enter your search term
3. Use "Find Next" to navigate through matches
4. Use "Replace" or "Replace All" for text substitution

## 🛠️ Technical Details

### Architecture
ModernTextViewer follows a Model-View-Service architecture pattern:

```
ModernTextViewer/
├── Program.cs              # Application entry point
├── src/
│   ├── Forms/             # UI Forms (MainForm, Dialogs)
│   ├── Models/            # Data models (Document, Hyperlink)
│   ├── Services/          # Business logic (File operations, Hyperlink handling)
│   └── Controls/          # Custom UI controls
```

### Technology Stack
- **Framework**: .NET 8.0
- **UI Framework**: Windows Forms
- **Language**: C# 12.0
- **IDE**: Visual Studio 2022 / VS Code

### Key Components
- `MainForm.cs`: Main application window and UI logic
- `DocumentModel.cs`: Document state management
- `FileService.cs`: Async file I/O operations
- `FindReplaceDialog.cs`: Search and replace functionality
- `HyperlinkDialog.cs`: Hyperlink management

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Development Guidelines
- Follow C# coding conventions
- Maintain the existing architecture pattern
- Add XML documentation for public methods
- Test on Windows 10 and Windows 11
- Ensure dark mode compatibility for new features

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/ModernTextViewer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/ModernTextViewer/discussions)

## 🙏 Acknowledgments

- Built with .NET and Windows Forms
- Icon designed for modern Windows applications
- Inspired by the need for a lightweight, modern text editor

---

Made with ❤️ for Windows users who appreciate clean, functional design.