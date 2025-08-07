using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModernTextViewer.src.Models;
using ModernTextViewer.src.Services;

namespace ModernTextViewer.src.Controls
{
    public class VirtualTextBox : UserControl
    {
        private RichTextBox displayTextBox = null!;
        private VScrollBar verticalScrollBar = null!;
        private StreamingTextProcessor? streamingProcessor;
        private StreamingFileInfo? fileInfo;
        
        // Virtual display properties
        private long totalLines;
        private int visibleLines;
        private long currentTopLine;
        private readonly int linesBuffer = 50; // Extra lines to load above/below visible area
        private readonly Dictionary<long, string> lineCache = new();
        private readonly Queue<long> cacheAccessOrder = new();
        private const int MAX_CACHED_LINES = 1000;
        
        // Performance and memory management
        private CancellationTokenSource? loadCancellationTokenSource;
        private volatile bool isLoading = false;
        private readonly object cacheLock = new();
        
        // Events
        public event EventHandler<TextChangedEventArgs>? VirtualTextChanged;
        public event EventHandler<ProgressEventArgs>? LoadProgressChanged;

        public class TextChangedEventArgs : EventArgs
        {
            public string NewText { get; set; } = string.Empty;
            public int SelectionStart { get; set; }
            public int SelectionLength { get; set; }
        }

        public class ProgressEventArgs : EventArgs
        {
            public int PercentComplete { get; set; }
            public string? Status { get; set; }
        }

        public VirtualTextBox()
        {
            ErrorManager.ExecuteWithErrorHandling(() =>
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
                
                // Initialize display text box
                displayTextBox = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.None,
                    WordWrap = true,
                    ScrollBars = RichTextBoxScrollBars.None, // We handle scrolling ourselves
                    ReadOnly = false
                };
                
                // Initialize vertical scroll bar
                verticalScrollBar = new VScrollBar
                {
                    Dock = DockStyle.Right,
                    Width = SystemInformation.VerticalScrollBarWidth
                };
                
                // Wire up events with error handling
                displayTextBox.TextChanged += (s, e) => ErrorManager.ExecuteWithErrorHandling(
                    () => DisplayTextBox_TextChanged(s, e), 
                    ErrorManager.ErrorCategory.UI, "Text changed event");
                    
                displayTextBox.SelectionChanged += (s, e) => ErrorManager.ExecuteWithErrorHandling(
                    () => DisplayTextBox_SelectionChanged(s, e), 
                    ErrorManager.ErrorCategory.UI, "Selection changed event");
                    
                verticalScrollBar.Scroll += async (s, e) => 
                {
                    await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                    {
                        await VerticalScrollBar_Scroll(s, e);
                        return true;
                    }, ErrorManager.ErrorCategory.UI, "Scroll event", false);
                };
                    
                displayTextBox.MouseWheel += async (s, e) =>
                {
                    await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                    {
                        await DisplayTextBox_MouseWheel(s, e);
                        return true;
                    }, ErrorManager.ErrorCategory.UI, "Mouse wheel event", false);
                };
                
                // Add controls
                Controls.Add(displayTextBox);
                Controls.Add(verticalScrollBar);
                
                CalculateVisibleLines();
                
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Info, 
                    "VirtualTextBox initialized successfully");
            },
            ErrorManager.ErrorCategory.UI,
            "Initialize VirtualTextBox",
            fallbackAction: () => 
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Critical, 
                    "Failed to initialize VirtualTextBox - using minimal fallback");
                // Create minimal fallback controls
                try
                {
                    var fallbackTextBox = new RichTextBox { Dock = DockStyle.Fill };
                    var fallbackScrollBar = new VScrollBar { Dock = DockStyle.Right, Visible = false };
                    displayTextBox = fallbackTextBox;
                    verticalScrollBar = fallbackScrollBar;
                    Controls.Add(displayTextBox);
                    Controls.Add(verticalScrollBar);
                }
                catch (Exception ex)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Critical, 
                        "Even fallback initialization failed", ex);
                }
            });
        }

        public bool IsVirtualMode { get; private set; }

        public new string Text
        {
            get => displayTextBox.Text;
            set
            {
                if (!IsVirtualMode)
                {
                    displayTextBox.Text = value;
                }
            }
        }

        public int SelectionStart
        {
            get => displayTextBox.SelectionStart;
            set => displayTextBox.SelectionStart = value;
        }

        public int SelectionLength
        {
            get => displayTextBox.SelectionLength;
            set => displayTextBox.SelectionLength = value;
        }

        public Font TextFont
        {
            get => displayTextBox.Font;
            set
            {
                displayTextBox.Font = value;
                CalculateVisibleLines();
                if (IsVirtualMode)
                {
                    _ = RefreshDisplayAsync();
                }
            }
        }

        public new Color BackColor
        {
            get => displayTextBox.BackColor;
            set => displayTextBox.BackColor = value;
        }

        public new Color ForeColor
        {
            get => displayTextBox.ForeColor;
            set => displayTextBox.ForeColor = value;
        }

        // Additional properties for compatibility with RichTextBox usage
        public string SelectedText
        {
            get => displayTextBox.SelectedText;
            set => displayTextBox.SelectedText = value;
        }

        public int TextLength => displayTextBox.TextLength;

        public Font? SelectionFont
        {
            get => displayTextBox.SelectionFont;
            set => displayTextBox.SelectionFont = value!;
        }

        // Methods for RichTextBox compatibility
        public void Select(int start, int length)
        {
            displayTextBox.Select(start, length);
        }

        public void ScrollToCaret()
        {
            displayTextBox.ScrollToCaret();
        }

        public void Undo()
        {
            displayTextBox.Undo();
        }

        public int GetCharIndexFromPosition(Point point)
        {
            return displayTextBox.GetCharIndexFromPosition(point);
        }

        public Point GetPositionFromCharIndex(int index)
        {
            return displayTextBox.GetPositionFromCharIndex(index);
        }

        public new Graphics CreateGraphics()
        {
            return displayTextBox.CreateGraphics();
        }

        public new void Invalidate()
        {
            displayTextBox.Invalidate();
            base.Invalidate();
        }

        public void Cut()
        {
            displayTextBox.Cut();
        }

        public void Paste()
        {
            displayTextBox.Paste();
        }

        public void SelectAll()
        {
            displayTextBox.SelectAll();
        }

        // Method to get underlying RichTextBox for services that need it
        public RichTextBox GetUnderlyingRichTextBox()
        {
            return displayTextBox;
        }

        public async Task LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                // Input validation
                if (string.IsNullOrEmpty(filePath))
                {
                    var ex = new ArgumentException("File path cannot be empty");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Error, 
                        "Invalid file path for VirtualTextBox load", ex);
                    throw ex;
                }

                if (!File.Exists(filePath))
                {
                    var ex = new FileNotFoundException("File not found for VirtualTextBox load", filePath);
                    ErrorManager.LogFileError("VirtualTextBox load", filePath, ex);
                    throw ex;
                }

                try
                {
                    // Cancel any existing operations
                    loadCancellationTokenSource?.Cancel();
                    loadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    isLoading = true;

                    // Clean up previous processor
                    streamingProcessor?.Dispose();
                    streamingProcessor = new StreamingTextProcessor();
                    
                    // Set up progress reporting with error handling
                    streamingProcessor.ProgressChanged += (s, e) => ErrorManager.ExecuteWithErrorHandling(() =>
                    {
                        LoadProgressChanged?.Invoke(this, new ProgressEventArgs 
                        { 
                            PercentComplete = e.PercentComplete,
                            Status = e.CurrentOperation
                        });
                    }, ErrorManager.ErrorCategory.UI, "Progress update");

                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                        $"Starting to load file: {filePath}");

                    // Analyze file to determine if virtualization is needed
                    fileInfo = await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                    {
                        return await streamingProcessor.AnalyzeFileAsync(filePath, loadCancellationTokenSource.Token);
                    },
                    ErrorManager.ErrorCategory.FileIO,
                    "Analyze file for virtual text box",
                    new StreamingFileInfo { FilePath = filePath, RequiresStreaming = false });

                    if (fileInfo.RequiresStreaming)
                    {
                        ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                            $"Using virtual mode for large file: {fileInfo.FileSize / 1024 / 1024}MB");
                        
                        await ErrorRecovery.RecoverMemoryOperationAsync(
                            async (ct) => await EnableVirtualModeAsync(ct),
                            async (ct) => 
                            {
                                // Fallback: try to load directly with reduced memory usage
                                ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                                    "Virtual mode failed, attempting direct load as fallback");
                                await LoadFileDirectlyAsync(filePath, ct);
                            },
                            "Enable virtual mode",
                            loadCancellationTokenSource.Token);
                    }
                    else
                    {
                        ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                            $"Using direct mode for file: {fileInfo.FileSize / 1024 / 1024}MB");
                            
                        await ErrorRecovery.RecoverMemoryOperationAsync(
                            async (ct) => await LoadFileDirectlyAsync(filePath, ct),
                            null, // No fallback for direct load
                            "Load file directly",
                            loadCancellationTokenSource.Token);
                    }

                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                        $"Successfully loaded file: {filePath}");
                }
                finally
                {
                    isLoading = false;
                }

                return true;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Load file in VirtualTextBox",
            false);
        }

        private async Task EnableVirtualModeAsync(CancellationToken cancellationToken)
        {
            IsVirtualMode = true;
            totalLines = fileInfo?.EstimatedLineCount ?? 0;
            currentTopLine = 0;
            
            // Setup scroll bar
            verticalScrollBar.Visible = true;
            verticalScrollBar.Minimum = 0;
            verticalScrollBar.Maximum = (int)Math.Max(0, totalLines - visibleLines);
            verticalScrollBar.Value = 0;
            
            // Clear cache
            lock (cacheLock)
            {
                lineCache.Clear();
                cacheAccessOrder.Clear();
            }
            
            await LoadVisibleContentAsync(cancellationToken);
        }

        private async Task LoadFileDirectlyAsync(string filePath, CancellationToken cancellationToken)
        {
            IsVirtualMode = false;
            verticalScrollBar.Visible = false;
            
            // Use regular FileService for smaller files
            var (content, hyperlinks) = await FileService.LoadFileAsync(filePath, cancellationToken);
            displayTextBox.Text = content;
        }

        private async Task LoadVisibleContentAsync(CancellationToken cancellationToken)
        {
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                if (streamingProcessor == null)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Warning, 
                        "Cannot load visible content: streaming processor is null");
                    return false;
                }

                if (fileInfo == null)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Warning, 
                        "Cannot load visible content: file info is null");
                    return false;
                }

                var startLine = Math.Max(0, currentTopLine - linesBuffer);
                var endLine = Math.Min(totalLines, currentTopLine + visibleLines + linesBuffer);
                var linesToLoad = endLine - startLine;

                if (linesToLoad <= 0)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                        "No lines to load for current view");
                    return true;
                }

                // Calculate file position based on estimated line length
                var estimatedBytesPerLine = fileInfo.FileSize / Math.Max(1, totalLines);
                var startPosition = startLine * estimatedBytesPerLine;
                var lengthToLoad = linesToLoad * estimatedBytesPerLine;

                // Limit maximum load size to prevent memory issues
                const long MAX_SEGMENT_SIZE = 10 * 1024 * 1024; // 10MB
                if (lengthToLoad > MAX_SEGMENT_SIZE)
                {
                    lengthToLoad = MAX_SEGMENT_SIZE;
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                        $"Limiting segment load size to {MAX_SEGMENT_SIZE / 1024 / 1024}MB");
                }

                // Use error recovery for loading segments
                var recoveryResult = await ErrorRecovery.RecoverMemoryOperationAsync(
                    async (ct) =>
                    {
                        var segment = await streamingProcessor.LoadTextSegmentAsync(startPosition, lengthToLoad, ct);
                        return segment;
                    },
                    async (ct) =>
                    {
                        // Fallback: load smaller segment
                        var fallbackLength = Math.Min(lengthToLoad / 2, 1024 * 1024); // 1MB fallback
                        ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Info, 
                            $"Using fallback segment size: {fallbackLength / 1024}KB");
                        var segment = await streamingProcessor.LoadTextSegmentAsync(startPosition, fallbackLength, ct);
                        return segment;
                    },
                    "Load visible text segment",
                    cancellationToken);

                if (!recoveryResult.Success)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Error, 
                        $"Failed to load visible content: {recoveryResult.ErrorMessage}");

                    // Show user-friendly error dialog
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() => 
                        {
                            var result = ErrorDialogService.ShowError(
                                "Content Loading Error",
                                "Failed to load the visible portion of the file. This may be due to memory constraints or file access issues.",
                                ErrorManager.ErrorSeverity.Error,
                                new List<string> 
                                { 
                                    "Try scrolling to a different section",
                                    "Close other applications to free memory",
                                    "Restart the application if the problem persists"
                                },
                                canRetry: true,
                                canIgnore: true,
                                details: recoveryResult.ErrorMessage,
                                parent: this);

                            if (result.Choice == ErrorDialogService.UserChoice.Retry)
                            {
                                _ = Task.Run(async () => await LoadVisibleContentAsync(CancellationToken.None));
                            }
                        }));
                    }

                    return false;
                }

                var loadedSegment = recoveryResult.Result as StreamingTextProcessor.TextSegment;
                if (loadedSegment == null)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Error, 
                        "Loaded segment is null");
                    return false;
                }

                // Update display on UI thread with error handling
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() => ErrorManager.ExecuteWithErrorHandling(() =>
                        {
                            UpdateDisplayContent(loadedSegment.Content);
                        }, 
                        ErrorManager.ErrorCategory.UI, 
                        "Update display content")));
                    }
                    else
                    {
                        ErrorManager.ExecuteWithErrorHandling(() =>
                        {
                            UpdateDisplayContent(loadedSegment.Content);
                        }, 
                        ErrorManager.ErrorCategory.UI, 
                        "Update display content");
                    }
                }

                return true;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Load visible content",
            false);
        }

        private void UpdateDisplayContent(string content)
        {
            ErrorManager.ExecuteWithErrorHandling(() =>
            {
                if (isLoading) 
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Info, 
                        "Skipping display update: still loading");
                    return;
                }

                if (displayTextBox.IsDisposed)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Warning, 
                        "Cannot update display: text box is disposed");
                    return;
                }

                // Validate content
                if (content == null)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Validation, ErrorManager.ErrorSeverity.Warning, 
                        "Content is null, using empty string");
                    content = string.Empty;
                }

                // Check for excessively large content
                if (content.Length > 50 * 1024 * 1024) // 50MB text limit
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                        $"Content is very large ({content.Length / 1024 / 1024}MB), may cause UI issues");
                }

                var previousSelection = displayTextBox.SelectionStart;
                var previousLength = displayTextBox.SelectionLength;

                try
                {
                    displayTextBox.Text = content;
                }
                catch (OutOfMemoryException ex)
                {
                    ErrorManager.LogMemoryError("update display content", ex, GC.GetTotalMemory(false));
                    
                    // Fallback: try with truncated content
                    try
                    {
                        var truncatedContent = content.Length > 1024 * 1024 
                            ? content.Substring(0, 1024 * 1024) + "\n\n[Content truncated due to memory constraints]"
                            : content;
                            
                        displayTextBox.Text = truncatedContent;
                        
                        ErrorManager.LogError(ErrorManager.ErrorCategory.Memory, ErrorManager.ErrorSeverity.Warning, 
                            "Used truncated content due to memory constraints");
                    }
                    catch (Exception fallbackEx)
                    {
                        ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Error, 
                            "Even truncated content update failed", fallbackEx);
                        displayTextBox.Text = "[Error: Unable to display content due to memory constraints]";
                    }
                }
                catch (ArgumentException ex)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Error, 
                        "Invalid argument when setting text content", ex);
                    displayTextBox.Text = "[Error: Invalid content format]";
                }

                // Restore selection if possible
                try
                {
                    if (previousSelection < displayTextBox.TextLength)
                    {
                        displayTextBox.SelectionStart = Math.Min(previousSelection, displayTextBox.TextLength);
                        displayTextBox.SelectionLength = Math.Min(previousLength, displayTextBox.TextLength - displayTextBox.SelectionStart);
                    }
                }
                catch (Exception ex)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Warning, 
                        "Failed to restore text selection", ex);
                    // Don't throw - selection restoration is not critical
                }
            },
            ErrorManager.ErrorCategory.UI,
            "Update display content",
            fallbackAction: () =>
            {
                try
                {
                    displayTextBox.Text = "[Error: Failed to update display content]";
                }
                catch
                {
                    // Last resort - even this failed
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Critical, 
                        "Complete failure to update display - text box may be corrupted");
                }
            });
        }

        private async Task RefreshDisplayAsync()
        {
            if (IsVirtualMode && !isLoading)
            {
                loadCancellationTokenSource?.Cancel();
                loadCancellationTokenSource = new CancellationTokenSource();
                await LoadVisibleContentAsync(loadCancellationTokenSource.Token);
            }
        }

        private void CalculateVisibleLines()
        {
            if (displayTextBox.Font != null && displayTextBox.Height > 0)
            {
                var lineHeight = TextRenderer.MeasureText("Ay", displayTextBox.Font).Height;
                visibleLines = Math.Max(1, displayTextBox.Height / lineHeight);
            }
            else
            {
                visibleLines = 25; // Default estimate
            }
        }

        private async void VerticalScrollBar_Scroll(object? sender, ScrollEventArgs e)
        {
            if (!IsVirtualMode) return;

            currentTopLine = e.NewValue;
            await RefreshDisplayAsync();
        }

        private async void DisplayTextBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (!IsVirtualMode) return;

            var delta = e.Delta / 120; // Standard mouse wheel delta
            var newTopLine = Math.Max(0, Math.Min(totalLines - visibleLines, currentTopLine - delta * 3));
            
            if (newTopLine != currentTopLine)
            {
                currentTopLine = newTopLine;
                verticalScrollBar.Value = (int)currentTopLine;
                await RefreshDisplayAsync();
            }
        }

        private void DisplayTextBox_TextChanged(object? sender, EventArgs e)
        {
            VirtualTextChanged?.Invoke(this, new TextChangedEventArgs 
            { 
                NewText = displayTextBox.Text,
                SelectionStart = displayTextBox.SelectionStart,
                SelectionLength = displayTextBox.SelectionLength
            });
        }

        private void DisplayTextBox_SelectionChanged(object? sender, EventArgs e)
        {
            // Update scroll position if needed for virtual mode
            if (IsVirtualMode)
            {
                // Calculate which line the selection is on and ensure it's visible
                var selectionLine = GetLineFromCharIndex(displayTextBox.SelectionStart);
                EnsureLineVisible(selectionLine);
            }
        }

        private int GetLineFromCharIndex(int charIndex)
        {
            return displayTextBox.GetLineFromCharIndex(charIndex);
        }

        private void EnsureLineVisible(int line)
        {
            if (line < currentTopLine || line >= currentTopLine + visibleLines)
            {
                currentTopLine = Math.Max(0, line - visibleLines / 2);
                verticalScrollBar.Value = (int)Math.Min(verticalScrollBar.Maximum, currentTopLine);
                _ = RefreshDisplayAsync();
            }
        }

        public async Task<List<SearchResult>> SearchAsync(string searchTerm, bool caseSensitive = false, CancellationToken cancellationToken = default)
        {
            var results = new List<SearchResult>();

            if (IsVirtualMode && streamingProcessor != null)
            {
                var segments = await streamingProcessor.SearchInFileAsync(searchTerm, caseSensitive, cancellationToken);
                foreach (var segment in segments)
                {
                    var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    var index = 0;
                    while ((index = segment.Content.IndexOf(searchTerm, index, comparison)) != -1)
                    {
                        results.Add(new SearchResult 
                        { 
                            Position = segment.StartPosition + index,
                            Length = searchTerm.Length,
                            Line = (int)(segment.StartPosition / (fileInfo?.FileSize / totalLines ?? 1))
                        });
                        index += searchTerm.Length;
                    }
                }
            }
            else
            {
                // Regular search for non-virtual mode
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var text = displayTextBox.Text;
                var index = 0;
                while ((index = text.IndexOf(searchTerm, index, comparison)) != -1)
                {
                    results.Add(new SearchResult 
                    { 
                        Position = index,
                        Length = searchTerm.Length,
                        Line = GetLineFromCharIndex(index)
                    });
                    index += searchTerm.Length;
                }
            }

            return results;
        }

        public void GoToLine(long line)
        {
            if (IsVirtualMode)
            {
                currentTopLine = Math.Max(0, Math.Min(totalLines - visibleLines, line - visibleLines / 2));
                verticalScrollBar.Value = (int)currentTopLine;
                _ = RefreshDisplayAsync();
            }
            else
            {
                var charIndex = displayTextBox.GetFirstCharIndexFromLine((int)line);
                if (charIndex >= 0)
                {
                    displayTextBox.SelectionStart = charIndex;
                    displayTextBox.ScrollToCaret();
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CalculateVisibleLines();
            
            if (IsVirtualMode)
            {
                verticalScrollBar.Maximum = (int)Math.Max(0, totalLines - visibleLines);
                _ = RefreshDisplayAsync();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                loadCancellationTokenSource?.Cancel();
                loadCancellationTokenSource?.Dispose();
                streamingProcessor?.Dispose();
                
                displayTextBox?.Dispose();
                verticalScrollBar?.Dispose();
            }
            base.Dispose(disposing);
        }

        public class SearchResult
        {
            public long Position { get; set; }
            public int Length { get; set; }
            public int Line { get; set; }
        }
    }
}