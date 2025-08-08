# Preview/Raw Mode Toggle Feature - Implementation Plan

## 🎯 Feature Overview

Add a toggle button next to the "Open File" button that switches between:
- **Raw Mode**: Shows original text content (current behavior)
- **Preview Mode**: Shows formatted/rendered content (e.g., markdown as HTML)

## 🏗️ Architecture Analysis

### Current Structure
- **Main UI**: `src/Forms/MainForm.cs` (1,777 lines) - Primary form handling all UI interactions
- **Document State**: `src/Models/DocumentModel.cs` - Document state and dirty tracking
- **File Operations**: `src/Services/FileService.cs` - Async file loading/saving
- **Custom Controls**: `src/Controls/CustomToolbar.cs` - Custom toolbar implementations

### Integration Points
- Existing toolbar button implementation pattern
- Dark/light theme support
- Auto-save functionality
- File operation workflows
- Model-View-Service pattern

## 📋 Implementation Tasks

### Phase 1: Foundation
1. **Research Markdown Library** (web-search agent)
   - Find suitable .NET markdown to HTML library
   - Evaluate MarkDig, CommonMark.NET, or similar
   - Assess performance and feature requirements

2. **Extend DocumentModel** (code-writer agent)
   - Add `IsPreviewMode` property
   - Add `SupportsPreview` method for file type detection
   - Add preview state change notifications

### Phase 2: Core Services
3. **Create PreviewService** (code-writer agent)
   - Markdown to HTML conversion
   - Theme-aware CSS generation
   - Caching for performance optimization

4. **Add UI Controls** (code-writer agent)
   - WebBrowser control for HTML rendering
   - Toggle button with proper styling
   - Layout management for mode switching

### Phase 3: Integration
5. **Implement Toggle Logic** (code-writer agent)
   - Button click handler for mode switching
   - Content synchronization between raw and preview
   - State persistence and restoration

6. **Theme Integration** (code-writer agent)
   - Dark/light mode CSS generation
   - Button styling consistency
   - Preview content theming

### Phase 4: Operations
7. **Update File Operations** (code-writer agent)
   - Ensure save/load works in both modes
   - Auto-save compatibility
   - File type detection integration

8. **Testing & Debugging** (debugger agent)
   - UI functionality testing
   - Mode switching edge cases
   - Performance optimization

### Phase 5: Finalization
9. **Documentation Updates** (documentation-writer agent)
   - Update CLAUDE.md with new feature
   - Add XML comments to new methods
   - Update README if needed

## 🎨 Design Specifications

### Toggle Button
- **Position**: After "Open File" button in toolbar
- **Size**: 25x20px
- **Icons**: 👁️ (preview) / 📝 (raw)
- **Colors**: Blue-green theme matching application palette
- **Hover**: Consistent with existing button hover effects

### Preview Integration
- **Control**: WebBrowser for HTML rendering
- **Layout**: Replaces RichTextBox when in preview mode
- **Performance**: Lazy loading and content caching
- **Theme**: Dynamic CSS generation for dark/light modes

## 🔧 Technical Considerations

### State Management
- Preview mode state in DocumentModel
- Automatic file type detection (.md, .txt, etc.)
- Content synchronization between modes

### Performance
- HTML generation only when switching to preview mode
- Caching of converted content
- Minimal impact on existing file operations

### Extensibility
- Plugin-like architecture for future file format support
- Configurable preview renderers
- Easy addition of new supported formats

## 🧪 Testing Strategy

### Manual Testing Areas
- Mode switching functionality
- Content synchronization
- Theme compatibility (dark/light)
- File operations in both modes
- Auto-save behavior
- Keyboard shortcuts

### Edge Cases
- Large file handling
- Unsupported file formats
- Empty documents
- Malformed markdown content

## 📊 Success Criteria

1. ✅ Seamless toggle between raw and preview modes
2. ✅ Proper markdown rendering with theme support
3. ✅ No disruption to existing functionality
4. ✅ Performance maintained for large files
5. ✅ Extensible architecture for future formats
6. ✅ Consistent UI/UX with existing application

## 🚀 Next Steps

Start with **Phase 1, Task 1**: Research and select appropriate markdown library using the web-search agent.