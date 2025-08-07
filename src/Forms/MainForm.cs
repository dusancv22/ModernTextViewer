using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ModernTextViewer.src.Services;
using ModernTextViewer.src.Models;
using ModernTextViewer.src.Controls;
using System.Runtime.InteropServices.Marshalling;
using System.Diagnostics;
using System.IO;
using System.Drawing.Text;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ModernTextViewer.src.Forms
{
    public partial class MainForm : Form
    {
        private VirtualTextBox textBox = null!;
        private Panel titleBar = null!;
        private Button closeButton = null!;
        private Button maximizeButton = null!;
        private Button minimizeButton = null!;
        private Panel bottomToolbar = null!;
        private Button saveButton = null!;
        private Button quickSaveButton = null!;
        private Button fontButton = null!;
        private Button hyperlinkButton = null!;
        private Button themeToggleButton = null!;
        private ContextMenuStrip textBoxContextMenu = null!;
        private string lastTextContent = string.Empty;
        private int lastSelectionStart = 0;
        private const int RESIZE_BORDER = 8;
        private const int TITLE_BAR_WIDTH = 32;
        private const float MIN_FONT_SIZE = 4f;
        private const float MAX_FONT_SIZE = 96f;
        private float currentFontSize = 10f;
        private readonly DocumentModel document = new DocumentModel();
        private bool isDarkMode = true;  // Default to dark mode
        private System.Windows.Forms.Timer autoSaveTimer = null!;
        private readonly Color darkBackColor = Color.FromArgb(30, 30, 30);
        private readonly Color darkForeColor = Color.FromArgb(220, 220, 220);
        private readonly Color darkToolbarColor = Color.FromArgb(45, 45, 45);
        private Label autoSaveLabel = null!;
        private FindReplaceDialog? findReplaceDialog;
        
        // Memory-efficient undo/redo system
        private CircularUndoBuffer undoBuffer = null!;
        private bool isUndoRedoOperation = false;
        
        // Font caching for memory efficiency (lazy-loaded)
        private FontCache? fontCache;
        
        // Memory monitoring (lazy-loaded)
        private MemoryPressureMonitor? memoryMonitor;
        
        // Lazy loading flags and performance tracking
        private volatile bool isStartupComplete = false;
        private volatile bool isFullyInitialized = false;
        private readonly Stopwatch overallStartupTimer = Stopwatch.StartNew();
        private System.Windows.Forms.Timer hyperlinkUpdateTimer = null!;
        private CancellationTokenSource? hyperlinkCancellationTokenSource;
        private readonly SemaphoreSlim hyperlinkUpdateSemaphore = new SemaphoreSlim(1, 1);
        private volatile bool isHyperlinkUpdateInProgress = false;
        private int lastTimerIntervalTextLength = -1; // Track text length for timer interval optimization
        private CancellationTokenSource? autoSaveCancellationTokenSource;
        private CancellationTokenSource? themeRefreshCancellationTokenSource;
        private volatile bool isThemeRefreshInProgress = false;
        private readonly SemaphoreSlim themeRefreshSemaphore = new SemaphoreSlim(1, 1);

        // Performance monitoring system
        private PerformanceMonitor? performanceMonitor;
        private PerformanceStatusBar? performanceStatusBar;
        private PerformanceMetricsDialog? performanceDialog;
        private Stopwatch frameTimer = new Stopwatch();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        public MainForm()
        {
            ErrorManager.ExecuteWithErrorHandling(() =>
            {
                // Measure startup performance
                var startupTimer = Stopwatch.StartNew();
                
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Info, 
                    "Starting MainForm initialization");

                // Set up critical error handling
                ErrorManager.CriticalErrorOccurred += OnCriticalErrorOccurred;
                ErrorManager.ErrorOccurred += OnErrorOccurred;
                
                // Only do minimal initialization in constructor
                InitializeComponent();
                
                // Defer expensive initialization to Load event with error handling
                this.Load += async (s, e) => await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                {
                    await MainForm_LoadAsync(s, e);
                    return true;
                }, ErrorManager.ErrorCategory.UI, "MainForm Load event", false);
                
                this.Shown += async (s, e) => await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                {
                    await MainForm_ShownAsync(s, e);
                    return true;
                }, ErrorManager.ErrorCategory.UI, "MainForm Shown event", false);
                
                // Initialize only critical UI elements synchronously
                InitializeCriticalUI();
                
                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                    $"Constructor completed in {startupTimer.ElapsedMilliseconds}ms");
            },
            ErrorManager.ErrorCategory.UI,
            "MainForm constructor",
            fallbackAction: () =>
            {
                try
                {
                    // Minimal fallback initialization
                    InitializeComponent();
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Critical, 
                        "Using minimal fallback initialization");
                }
                catch (Exception criticalEx)
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Critical, 
                        "Complete initialization failure", criticalEx);
                    
                    ErrorDialogService.ShowCriticalError(
                        "Application Initialization Failed",
                        "The application could not initialize properly. This may be due to system compatibility issues or corrupted installation.",
                        "Please restart the application or reinstall if the problem persists.",
                        requiresRestart: true);
                    
                    throw criticalEx;
                }
            });
        }

        private async Task MainForm_LoadAsync(object? sender, EventArgs e)
        {
            var loadTimer = Stopwatch.StartNew();
            
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Info, 
                    "Starting MainForm load process");

                // Initialize essential components first with error recovery
                var essentialResult = await ErrorRecovery.RecoverUIOperation(
                    async () => await InitializeEssentialComponentsAsync(),
                    async () => 
                    {
                        // Fallback: minimal essential initialization
                        ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Warning, 
                            "Using fallback essential initialization");
                        await InitializeMinimalEssentialComponentsAsync();
                    },
                    "Initialize essential components");

                if (!essentialResult.Success)
                {
                    var ex = new InvalidOperationException($"Failed to initialize essential components: {essentialResult.ErrorMessage}");
                    ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Critical, 
                        "Critical failure in essential initialization", ex);
                    throw ex;
                }
                
                // Show window quickly
                ErrorManager.ExecuteWithErrorHandling(() =>
                {
                    this.Show();
                    this.Refresh();
                }, ErrorManager.ErrorCategory.UI, "Show main window");
                
                // Continue initialization in background with comprehensive error handling
                _ = Task.Run(async () =>
                {
                    await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
                    {
                        await CompleteInitializationAsync();
                        
                        BeginInvoke(new Action(() => ErrorManager.ExecuteWithErrorHandling(() =>
                        {
                            isFullyInitialized = true;
                            overallStartupTimer.Stop();
                            
                            ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                                $"=== STARTUP PERFORMANCE REPORT ===\n" +
                                $"Load event: {loadTimer.ElapsedMilliseconds}ms\n" +
                                $"Total startup time: {overallStartupTimer.ElapsedMilliseconds}ms\n" +
                                $"==================================");
                            
                            // Update status label safely
                            if (autoSaveLabel != null && !autoSaveLabel.IsDisposed)
                            {
                                autoSaveLabel.Text = $"Ready ({overallStartupTimer.ElapsedMilliseconds}ms startup)";
                            }
                        }, ErrorManager.ErrorCategory.UI, "Finalize startup")));
                        
                        return true;
                    },
                    ErrorManager.ErrorCategory.UI,
                    "Complete background initialization",
                    false);
                });
                
                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, ErrorManager.ErrorSeverity.Info, 
                    $"Load event completed in {loadTimer.ElapsedMilliseconds}ms");
                
                return true;
            },
            ErrorManager.ErrorCategory.UI,
            "MainForm load process",
            false);
        }

        private async Task MainForm_ShownAsync(object? sender, EventArgs e)
        {
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Info, 
                    "MainForm shown event triggered");
                
                isStartupComplete = true;
                
                // Perform any post-show initialization
                await Task.Delay(100); // Small delay to ensure UI is fully rendered
                
                return true;
            },
            ErrorManager.ErrorCategory.UI,
            "MainForm shown event",
            false);
        }

        private void OnErrorOccurred(object? sender, ErrorManager.ErrorEventArgs e)
        {
            ErrorManager.ExecuteWithErrorHandling(() =>
            {
                // Only show dialogs for errors and higher severity
                if (e.Error.Severity >= ErrorManager.ErrorSeverity.Error && e.CanRecover)
                {
                    // Show error dialog on UI thread
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() => ShowErrorDialog(e)));
                    }
                    else
                    {
                        ShowErrorDialog(e);
                    }
                }
            }, ErrorManager.ErrorCategory.UI, "Handle error event");
        }

        private void OnCriticalErrorOccurred(object? sender, ErrorManager.CriticalErrorEventArgs e)
        {
            ErrorManager.ExecuteWithErrorHandling(() =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => ShowCriticalErrorDialog(e)));
                }
                else
                {
                    ShowCriticalErrorDialog(e);
                }
            }, ErrorManager.ErrorCategory.UI, "Handle critical error event");
        }

        private void ShowErrorDialog(ErrorManager.ErrorEventArgs e)
        {
            var result = ErrorDialogService.ShowError(
                "Application Error",
                e.Error.Message,
                e.Error.Severity,
                e.SuggestedActions,
                canRetry: e.CanRecover && e.Error.Category == ErrorManager.ErrorCategory.FileIO,
                canIgnore: e.Error.Severity < ErrorManager.ErrorSeverity.Critical,
                details: $"Category: {e.Error.Category}\nTime: {e.Error.Timestamp:HH:mm:ss}\n\n{e.Error.Details}\n\n{e.Error.StackTrace}",
                parent: this);

            if (result.Choice == ErrorDialogService.UserChoice.Retry && e.Error.Category == ErrorManager.ErrorCategory.FileIO)
            {
                // Implement retry logic based on the error context
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Info, 
                    "User requested retry for error");
            }
        }

        private void ShowCriticalErrorDialog(ErrorManager.CriticalErrorEventArgs e)
        {
            var result = ErrorDialogService.ShowCriticalError(
                "Critical Application Error",
                e.Error.Message,
                e.RecoveryInstructions,
                e.RequiresRestart,
                $"Category: {e.Error.Category}\nTime: {e.Error.Timestamp:HH:mm:ss}\n\n{e.Error.Details}\n\n{e.Error.StackTrace}",
                parent: this);

            if (result.Choice == ErrorDialogService.UserChoice.SaveAndExit || e.RequiresRestart)
            {
                // Attempt emergency save and exit
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PerformEmergencySaveAsync();
                        await ErrorRecovery.TryRecoverFromCriticalErrorAsync(e.Error.Exception ?? new Exception(e.Error.Message), 
                            e.Error.Context ?? "Unknown");
                    }
                    finally
                    {
                        BeginInvoke(new Action(() => Application.Exit()));
                    }
                });
            }
        }

        private async Task PerformEmergencySaveAsync()
        {
            await ErrorManager.ExecuteWithErrorHandlingAsync(async () =>
            {
                if (document.IsDirty && !string.IsNullOrEmpty(document.CurrentFilePath))
                {
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                        "Attempting emergency save");
                    
                    var content = string.Empty;
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => content = textBox.Text));
                    }
                    else
                    {
                        content = textBox.Text;
                    }

                    var emergencyPath = document.CurrentFilePath + ".emergency";
                    await FileService.SaveFileAsync(emergencyPath, content, document.Hyperlinks);
                    
                    ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, ErrorManager.ErrorSeverity.Info, 
                        $"Emergency save completed to: {emergencyPath}");
                }
                
                return true;
            },
            ErrorManager.ErrorCategory.FileIO,
            "Emergency save",
            false);
        }

        private async Task InitializeMinimalEssentialComponentsAsync()
        {
            await Task.Run(() =>
            {
                // Minimal initialization that must succeed
                if (autoSaveLabel == null)
                {
                    autoSaveLabel = new Label { Text = "Safe Mode", Dock = DockStyle.Right };
                }
                
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, ErrorManager.ErrorSeverity.Warning, 
                    "Initialized minimal essential components - application in safe mode");
            });
        }
        
        private void MainForm_Shown(object? sender, EventArgs e)
        {
            var shownTimer = Stopwatch.StartNew();
            
            // Focus the text box once window is visible
            textBox?.Focus();
            textBox?.Refresh();
            
            isStartupComplete = true;
            Debug.WriteLine($"Window shown in {shownTimer.ElapsedMilliseconds}ms");
        }
        
        private void InitializeCriticalUI()
        {
            // Only set essential properties for quick window display
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = isDarkMode ? darkBackColor : Color.White;
            this.Padding = new Padding(3);
            this.DoubleBuffered = true;
            this.MinimumSize = new Size(200, 100);
            
            // Create minimal text box for immediate use
            CreateBasicTextBox();
        }
        
        private Task InitializeEssentialComponentsAsync()
        {
            // Initialize components needed for basic functionality
            autoSaveLabel.Text = "📝 Loading interface...";
            
            InitializeTextBox();
            InitializeTitleBar();
            InitializeBasicToolbar();
            
            this.Controls.Add(titleBar);
            this.LocationChanged += Form1_LocationChanged;
            
            autoSaveLabel.Text = "🔤 Loading fonts...";
            
            // Initialize font cache asynchronously
            _ = Task.Run(() => GetFontCache());
            
            return Task.CompletedTask;
        }
        
        private async Task CompleteInitializationAsync()
        {
            // Update status
            BeginInvoke(() => autoSaveLabel.Text = "🧠 Loading memory systems...");
            
            // Initialize expensive components in background
            await Task.Run(() =>
            {
                // Initialize performance monitoring system
                performanceMonitor = new PerformanceMonitor();
                performanceMonitor.PerformanceAlert += OnPerformanceAlert;
                
                // Set performance monitor for FileService
                FileService.SetPerformanceMonitor(performanceMonitor);
                
                // Initialize memory-efficient undo buffer (50MB default, configurable)
                undoBuffer = new CircularUndoBuffer(maxMemoryMB: 50);
                
                // Initialize memory monitoring
                GetMemoryMonitor().Start();
            });
            
            // Initialize timers on UI thread
            BeginInvoke(new Action(() =>
            {
                autoSaveLabel.Text = "🔧 Finalizing setup...";
                
                InitializeTimers();
                CompleteToolbarInitialization();
                InitializeButtons();
                
                // Initialize performance status bar
                if (performanceMonitor != null)
                {
                    InitializePerformanceStatusBar();
                }
                
                bottomToolbar?.BringToFront();
                titleBar?.BringToFront();
                
                // Start timers after full initialization
                autoSaveTimer?.Start();
            }));
        }
        
        private void CreateBasicTextBox()
        {
            // Create minimal text box for immediate display
            var textBoxContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                BackColor = isDarkMode ? darkBackColor : Color.White
            };

            var paddingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(33, 5, 5, 5),
                BackColor = isDarkMode ? darkBackColor : Color.White
            };

            textBox = new VirtualTextBox
            {
                Dock = DockStyle.Fill,
                TextFont = new Font("Consolas", currentFontSize), // Use system font initially
                BackColor = isDarkMode ? darkBackColor : Color.White,
                ForeColor = isDarkMode ? darkForeColor : Color.Black,
                TabStop = true,
                Enabled = true
            };
            
            paddingPanel.Controls.Add(textBox);
            textBoxContainer.Controls.Add(paddingPanel);
            this.Controls.Add(textBoxContainer);
            textBoxContainer.SendToBack();
            
            textBox.Select();
            lastTextContent = textBox.Text;
        }
        
        private void InitializeTimers()
        {
            autoSaveTimer = new System.Windows.Forms.Timer
            {
                Interval = 5 * 60 * 1000
            };
            autoSaveTimer.Tick += AutoSaveTimer_Tick;
            
            hyperlinkUpdateTimer = new System.Windows.Forms.Timer
            {
                Interval = 250 // 250ms delay to debounce rapid typing
            };
            hyperlinkUpdateTimer.Tick += HyperlinkUpdateTimer_Tick;
        }

        private void InitializePerformanceStatusBar()
        {
            try
            {
                performanceStatusBar = new PerformanceStatusBar();
                performanceStatusBar.ApplyTheme(isDarkMode);
                performanceStatusBar.SetPerformanceMonitor(performanceMonitor);
                performanceStatusBar.ShowDetailedMetrics += OnShowPerformanceDetails;
                
                // Add to form controls
                this.Controls.Add(performanceStatusBar);
                performanceStatusBar.BringToFront();
                
                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, 
                    ErrorManager.ErrorSeverity.Info,
                    "Performance status bar initialized successfully");
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, 
                    ErrorManager.ErrorSeverity.Warning,
                    "Failed to initialize performance status bar", ex);
            }
        }

        private void OnPerformanceAlert(object? sender, PerformanceAlertEventArgs e)
        {
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => OnPerformanceAlert(sender, e));
                    return;
                }

                // Update status bar with alert
                performanceStatusBar?.SetStatus($"⚠️ {e.AlertType}: {e.Message}", true);

                // Log the alert
                var severity = e.Severity switch
                {
                    AlertSeverity.Critical => ErrorManager.ErrorSeverity.Error,
                    AlertSeverity.Warning => ErrorManager.ErrorSeverity.Warning,
                    _ => ErrorManager.ErrorSeverity.Info
                };

                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, severity,
                    $"Performance Alert: {e.AlertType} - {e.Message}");

                // For critical alerts, consider showing a dialog
                if (e.Severity == AlertSeverity.Critical)
                {
                    _ = Task.Run(() =>
                    {
                        BeginInvoke(() =>
                        {
                            var result = MessageBox.Show(
                                $"Critical Performance Issue Detected:\n\n{e.Message}\n\nWould you like to view detailed performance metrics?",
                                "Performance Alert",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);

                            if (result == DialogResult.Yes)
                            {
                                ShowPerformanceMetrics();
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, 
                    ErrorManager.ErrorSeverity.Warning,
                    "Error handling performance alert", ex);
            }
        }

        private void OnShowPerformanceDetails(object? sender, EventArgs e)
        {
            ShowPerformanceMetrics();
        }

        private void ShowPerformanceMetrics()
        {
            try
            {
                if (performanceMonitor == null)
                {
                    MessageBox.Show("Performance monitoring is not available.", 
                        "Performance Metrics", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Close existing dialog if open
                performanceDialog?.Close();
                performanceDialog?.Dispose();

                // Create and show new dialog
                performanceDialog = new PerformanceMetricsDialog(performanceMonitor, isDarkMode);
                performanceDialog.Show(this);
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, 
                    ErrorManager.ErrorSeverity.Warning,
                    "Error showing performance metrics dialog", ex);
                MessageBox.Show("Failed to open performance metrics. Check the error log for details.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeUI()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = isDarkMode ? darkBackColor : Color.White;
            this.Padding = new Padding(3);
            this.DoubleBuffered = true;
            this.MinimumSize = new Size(200, 100);

            InitializeTextBox();
            InitializeBottomToolbar();
            InitializeTitleBar();
            InitializeButtons();

            this.Controls.Add(titleBar);

            bottomToolbar?.BringToFront();
            titleBar?.BringToFront();
            
            this.LocationChanged += Form1_LocationChanged;

            this.Shown += (s, e) => 
            {
                textBox?.Focus();
                textBox?.Refresh();  // Force refresh after showing
            };
        }

        private void InitializeTitleBar()
        {
            titleBar = new Panel
            {
                Width = TITLE_BAR_WIDTH,
                Dock = DockStyle.Left,
                BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke
            };

            InitializeWindowButtons();
            
            titleBar.MouseDown += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (e.Clicks == 2)
                    {
                        maximizeButton.PerformClick();
                    }
                    else
                    {
                        ReleaseCapture();
                        SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                    }
                }
            };
        }

        private void InitializeWindowButtons()
        {
            closeButton = CreateWindowButton("×", 12);
            maximizeButton = CreateWindowButton("□", 10);
            minimizeButton = CreateWindowButton("−", 10);

            closeButton.Click += (s, e) => this.Close();
            maximizeButton.Click += (s, e) => {
                this.WindowState = this.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
            minimizeButton.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            titleBar.Controls.Add(closeButton);
            titleBar.Controls.Add(maximizeButton);
            titleBar.Controls.Add(minimizeButton);
        }

        private Button CreateWindowButton(string text, int fontSize)
        {
            return new Button
            {
                Text = text,
                Size = new Size(TITLE_BAR_WIDTH, TITLE_BAR_WIDTH),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Top,
                ForeColor = isDarkMode ? darkForeColor : Color.Gray,
                Font = GetFontCache().GetFont("Arial", fontSize),
                Cursor = Cursors.Hand
            };
        }

        private void InitializeTextBox()
        {
            var textBoxContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                BackColor = isDarkMode ? darkBackColor : Color.White
            };

            var paddingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(33, 5, 5, 5),
                BackColor = isDarkMode ? darkBackColor : Color.White
            };

            textBox = new VirtualTextBox
            {
                Dock = DockStyle.Fill,
                TextFont = GetFontCache().GetFont("Consolas", currentFontSize),
                BackColor = isDarkMode ? darkBackColor : Color.White,
                ForeColor = isDarkMode ? darkForeColor : Color.Black,
                TabStop = true,
                Enabled = true
            };

            // Handle line endings in KeyDown instead of KeyPress
            textBox.KeyDown += (s, e) => 
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    int selectionStart = textBox.SelectionStart;
                    int selectionLength = textBox.SelectionLength;
                    
                    // Replace any selected text with newline, or just insert newline
                    textBox.SelectedText = Environment.NewLine;
                    
                    // The cursor is automatically positioned correctly after SelectedText assignment
                    textBox.ScrollToCaret();
                }
            };

            // Initialize context menu
            InitializeContextMenu();

            // Add VirtualTextBox event handlers
            textBox.VirtualTextChanged += VirtualTextBox_TextChanged;
            textBox.LoadProgressChanged += VirtualTextBox_LoadProgressChanged;

            // Set up control hierarchy
            paddingPanel.Controls.Add(textBox);
            textBoxContainer.Controls.Add(paddingPanel);
            this.Controls.Add(textBoxContainer);

            textBox.Select();
            textBox.Focus();
            lastTextContent = textBox.Text;

            textBoxContainer.SendToBack();
            
            // Save initial state for undo (deferred)
            _ = Task.Run(() =>
            {
                // Wait for undo buffer to be initialized
                while (undoBuffer == null && !isFullyInitialized)
                {
                    Thread.Sleep(10);
                }
                BeginInvoke(new Action(() => SaveUndoState()));
            });
        }

        private void TextBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                float delta = e.Delta > 0 ? 1f : -1f;
                float newSize = Math.Clamp(currentFontSize + delta, MIN_FONT_SIZE, MAX_FONT_SIZE);
                
                if (Math.Abs(newSize - currentFontSize) > 0.01f)
                {
                    currentFontSize = newSize;
                    
                    int selectionStart = textBox.SelectionStart;
                    int selectionLength = textBox.SelectionLength;

                    // Batch font changes to reduce UI blocking
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1).ConfigureAwait(false); // Allow UI to remain responsive
                        
                        BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (selectionLength > 0)
                                {
                                    // Preserve the style of each character in the selection
                                    for (int i = selectionStart; i < selectionStart + selectionLength; i++)
                                    {
                                        textBox.Select(i, 1);
                                        Font currentCharFont = textBox.SelectionFont ?? textBox.Font;
                                        FontStyle currentStyle = currentCharFont.Style;
                                        var newFont = GetFontCache().GetFont(currentCharFont.FontFamily.Name, currentFontSize, currentStyle);
                                        textBox.SelectionFont = newFont;
                                    }
                                }
                                else
                                {
                                    // Change entire textbox while preserving style
                                    FontStyle currentStyle = textBox.TextFont.Style;
                                    var newFont = GetFontCache().GetFont(textBox.TextFont.FontFamily.Name, currentFontSize, currentStyle);
                                    textBox.TextFont = newFont;
                                }

                                // Restore the original selection
                                textBox.Select(selectionStart, selectionLength);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error updating font size: {ex.Message}");
                            }
                        }));
                    });
                }
            }
        }

        private void Form1_LocationChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                EnsureFormIsWithinScreenBounds();
            }
        }

        private void EnsureFormIsWithinScreenBounds()
        {
            Screen[] screens = Screen.AllScreens;
            int lowestTaskbarPoint = screens.Max(s => s.WorkingArea.Bottom);

            if (this.Bottom > lowestTaskbarPoint)
            {
                this.Top = lowestTaskbarPoint - this.Height;
            }
        }

        private void InitializeBasicToolbar()
        {
            // Create basic toolbar structure only
            bottomToolbar = new Panel
            {
                Height = 30,
                Dock = DockStyle.Bottom,
                BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke
            };
            
            autoSaveLabel = new Label
            {
                Text = "⚡ Starting up...",
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(5),
                ForeColor = isDarkMode ? Color.FromArgb(255, 223, 100) : Color.DarkOrange
            };
            
            bottomToolbar.Controls.Add(autoSaveLabel);
            this.Controls.Add(bottomToolbar);
        }
        
        private void CompleteToolbarInitialization()
        {
            // Add remaining toolbar buttons
            CreateToolbarButtons();
        }
        
        private void CreateToolbarButtons()
        {
            var fontCache = GetFontCache();
            
            saveButton = new Button
            {
                Text = "💾+",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(130, 180, 255) : Color.RoyalBlue
            };

            quickSaveButton = new Button
            {
                Text = "💾",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(130, 255, 130) : Color.Green
            };

            fontButton = new Button
            {
                Text = "A",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(180, 180, 255) : Color.RoyalBlue
            };

            fontButton.FlatAppearance.BorderSize = 0;
            fontButton.Click += FontButton_Click;
            
            // Add hover effects
            fontButton.MouseEnter += FontButton_MouseEnter;
            fontButton.MouseLeave += FontButton_MouseLeave;

            hyperlinkButton = new Button
            {
                Text = "🔗",
                Width = 25,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(100, 200, 255) : Color.Blue
            };

            hyperlinkButton.FlatAppearance.BorderSize = 0;
            hyperlinkButton.Click += HyperlinkButton_Click;
            
            // Add hover effects
            hyperlinkButton.MouseEnter += HyperlinkButton_MouseEnter;
            hyperlinkButton.MouseLeave += HyperlinkButton_MouseLeave;

            themeToggleButton = new Button
            {
                Text = isDarkMode ? "☀️" : "🌙",
                Width = 25,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(255, 223, 0) : Color.FromArgb(100, 100, 200)
            };

            themeToggleButton.FlatAppearance.BorderSize = 0;
            themeToggleButton.Click += ThemeToggleButton_Click;
            
            // Add hover effects
            themeToggleButton.MouseEnter += ThemeToggleButton_MouseEnter;
            themeToggleButton.MouseLeave += ThemeToggleButton_MouseLeave;

            saveButton.FlatAppearance.BorderSize = 0;
            quickSaveButton.FlatAppearance.BorderSize = 0;
            
            saveButton.Click += SaveButton_Click;
            quickSaveButton.Click += QuickSaveButton_Click;
            
            bottomToolbar.Controls.Add(saveButton);
            bottomToolbar.Controls.Add(quickSaveButton);
            bottomToolbar.Controls.Add(fontButton);
            bottomToolbar.Controls.Add(hyperlinkButton);
            bottomToolbar.Controls.Add(themeToggleButton);
            
            saveButton.MouseEnter += SaveButton_MouseEnter;
            saveButton.MouseLeave += SaveButton_MouseLeave;

            quickSaveButton.MouseEnter += QuickSaveButton_MouseEnter;
            quickSaveButton.MouseLeave += QuickSaveButton_MouseLeave;
            
            // Update label
            autoSaveLabel.Text = "Last autosave: Never";
        }

        private void InitializeBottomToolbar()
        {
            bottomToolbar = new Panel
            {
                Height = 30,
                Dock = DockStyle.Bottom,
                BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke
            };

            saveButton = new Button
            {
                Text = "💾+",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(130, 180, 255) : Color.RoyalBlue
            };

            quickSaveButton = new Button
            {
                Text = "💾",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(130, 255, 130) : Color.Green
            };

            fontButton = new Button
            {
                Text = "A",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(180, 180, 255) : Color.RoyalBlue
            };

            fontButton.FlatAppearance.BorderSize = 0;
            fontButton.Click += FontButton_Click;
            
            // Add hover effects
            fontButton.MouseEnter += FontButton_MouseEnter;
            fontButton.MouseLeave += FontButton_MouseLeave;

            hyperlinkButton = new Button
            {
                Text = "🔗",
                Width = 25,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(100, 200, 255) : Color.Blue
            };

            hyperlinkButton.FlatAppearance.BorderSize = 0;
            hyperlinkButton.Click += HyperlinkButton_Click;
            
            // Add hover effects
            hyperlinkButton.MouseEnter += HyperlinkButton_MouseEnter;
            hyperlinkButton.MouseLeave += HyperlinkButton_MouseLeave;

            themeToggleButton = new Button
            {
                Text = isDarkMode ? "☀️" : "🌙",
                Width = 25,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = GetFontCache().GetFont("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(255, 223, 0) : Color.FromArgb(100, 100, 200)
            };

            themeToggleButton.FlatAppearance.BorderSize = 0;
            themeToggleButton.Click += ThemeToggleButton_Click;
            
            // Add hover effects
            themeToggleButton.MouseEnter += ThemeToggleButton_MouseEnter;
            themeToggleButton.MouseLeave += ThemeToggleButton_MouseLeave;

            autoSaveLabel = new Label
            {
                Text = "Last autosave: Never",
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(5),
                ForeColor = isDarkMode ? darkForeColor : Color.Gray
            };

            saveButton.FlatAppearance.BorderSize = 0;
            quickSaveButton.FlatAppearance.BorderSize = 0;
            
            saveButton.Click += SaveButton_Click;
            quickSaveButton.Click += QuickSaveButton_Click;
            
            bottomToolbar.Controls.Add(saveButton);
            bottomToolbar.Controls.Add(quickSaveButton);
            bottomToolbar.Controls.Add(fontButton);
            bottomToolbar.Controls.Add(hyperlinkButton);
            bottomToolbar.Controls.Add(themeToggleButton);
            bottomToolbar.Controls.Add(autoSaveLabel);
            this.Controls.Add(bottomToolbar);
            
            bottomToolbar.BringToFront();

            saveButton.MouseEnter += SaveButton_MouseEnter;
            saveButton.MouseLeave += SaveButton_MouseLeave;

            quickSaveButton.MouseEnter += QuickSaveButton_MouseEnter;
            quickSaveButton.MouseLeave += QuickSaveButton_MouseLeave;
        }

        // Constants for window messages
        private const int WM_MOVING = 0x0216;
        private const int WM_SIZING = 0x0214;
        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int WM_SETREDRAW = 0x000B;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public static RECT FromIntPtr(IntPtr ptr)
            {
                return Marshal.PtrToStructure<RECT>(ptr);
            }

            public void WriteToIntPtr(IntPtr ptr)
            {
                Marshal.StructureToPtr(this, ptr, false);
            }

            public void Offset(int x, int y)
            {
                Left += x;
                Right += x;
                Top += y;
                Bottom += y;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                Point pos = new Point(m.LParam.ToInt32());
                pos = this.PointToClient(pos);

                IntPtr result = IntPtr.Zero;

                if (pos.Y <= RESIZE_BORDER && pos.X <= RESIZE_BORDER)
                    result = (IntPtr)HTTOPLEFT;
                else if (pos.Y <= RESIZE_BORDER && pos.X >= ClientSize.Width - RESIZE_BORDER)
                    result = (IntPtr)HTTOPRIGHT;
                else if (pos.Y >= ClientSize.Height - RESIZE_BORDER && pos.X <= RESIZE_BORDER)
                    result = (IntPtr)HTBOTTOMLEFT;
                else if (pos.Y >= ClientSize.Height - RESIZE_BORDER && pos.X >= ClientSize.Width - RESIZE_BORDER)
                    result = (IntPtr)HTBOTTOMRIGHT;
                else if (pos.Y <= RESIZE_BORDER)
                    result = (IntPtr)HTTOP;
                else if (pos.Y >= ClientSize.Height - RESIZE_BORDER)
                    result = (IntPtr)HTBOTTOM;
                else if (pos.X <= RESIZE_BORDER)
                    result = (IntPtr)HTLEFT;
                else if (pos.X >= ClientSize.Width - RESIZE_BORDER)
                    result = (IntPtr)HTRIGHT;

                if (result != IntPtr.Zero)
                {
                    m.Result = result;
                    return;
                }
            }
            else if (m.Msg == WM_MOVING || m.Msg == WM_SIZING)
            {
                var rc = RECT.FromIntPtr(m.LParam);
                Screen[] screens = Screen.AllScreens;
                int lowestTaskbarPoint = screens.Max(s => s.WorkingArea.Bottom);

                if (rc.Bottom > lowestTaskbarPoint)
                {
                    if (m.Msg == WM_MOVING)
                        rc.Offset(0, lowestTaskbarPoint - rc.Bottom);
                    else // WM_SIZING
                        rc.Bottom = lowestTaskbarPoint;
                }

                rc.WriteToIntPtr(m.LParam);
            }

            base.WndProc(ref m);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (WindowState == FormWindowState.Normal)
            {
                if (e.Y <= RESIZE_BORDER && e.X <= RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNWSE;
                else if (e.Y <= RESIZE_BORDER && e.X >= ClientSize.Width - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNESW;
                else if (e.Y >= ClientSize.Height - RESIZE_BORDER && e.X <= RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNESW;
                else if (e.Y >= ClientSize.Height - RESIZE_BORDER && e.X >= ClientSize.Width - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNWSE;
                else if (e.Y <= RESIZE_BORDER || e.Y >= ClientSize.Height - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeNS;
                else if (e.X <= RESIZE_BORDER || e.X >= ClientSize.Width - RESIZE_BORDER)
                    this.Cursor = Cursors.SizeWE;
                else
                    this.Cursor = Cursors.Default;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            frameTimer.Restart();
            
            base.OnPaint(e);
            using (var pen = new Pen(isDarkMode ? darkToolbarColor : Color.LightGray, 1))
            {
                var rect = this.ClientRectangle;
                e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }
            
            frameTimer.Stop();
            performanceMonitor?.TrackUIFrameTime(frameTimer.Elapsed);
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog()
                {
                    Filter = "Text files (*.txt)|*.txt|Markdown files (*.md)|*.md|Subtitle files (*.srt)|*.srt|All files (*.*)|*.*",
                    FilterIndex = 1
                };
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    await SaveFileAsync(saveDialog.FileName, textBox.Text, document.Hyperlinks);
                    document.FilePath = saveDialog.FileName;
                    document.ResetDirty();
                    autoSaveLabel.Text = $"Last save: {DateTime.Now.ToString("HH:mm:ss")}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void QuickSaveButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(document.FilePath))
            {
                autoSaveLabel.Text = "Please use 'Save As' first";
                return;
            }

            try
            {
                await SaveFileAsync(document.FilePath, textBox.Text, document.Hyperlinks);
                document.ResetDirty();
                autoSaveLabel.Text = $"Successfully saved: {DateTime.Now.ToString("HH:mm:ss")}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                QuickSaveButton_Click(this, EventArgs.Empty);
                return true;
            }
            
            if (keyData == (Keys.Control | Keys.Z))
            {
                textBox.Undo();
                return true;
            }
            
            if (keyData == (Keys.Control | Keys.F))
            {
                ShowFindReplaceDialog();
                return true;
            }
            
            if (textBox.SelectionLength > 0)
            {
                switch (keyData)
                {
                    case Keys.Control | Keys.B:
                        ApplyTextStyle(FontStyle.Bold);
                        return true;
                    
                    case Keys.Control | Keys.I:
                        ApplyTextStyle(FontStyle.Italic);
                        return true;
                    
                    case Keys.Control | Keys.U:
                        ApplyTextStyle(FontStyle.Underline);
                        return true;
                }
            }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ApplyTextStyle(FontStyle style)
        {
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;
            
            // Get the current font style of the selected text
            Font currentFont = textBox.SelectionFont ?? textBox.Font;
            FontStyle newStyle;
            
            // Toggle the style
            if ((currentFont.Style & style) == style)
            {
                // Remove the style if it's already applied
                newStyle = currentFont.Style & ~style;
            }
            else
            {
                // Add the style if it's not applied
                newStyle = currentFont.Style | style;
            }
            
            // For large selections, apply styling asynchronously to avoid UI blocking
            if (selectionLength > 1000) // Only use async for large selections
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1).ConfigureAwait(false); // Allow UI to remain responsive
                    
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // Create and apply the new font
                            var newFont = GetFontCache().GetFont(currentFont.FontFamily.Name, currentFont.Size, newStyle);
                            textBox.SelectionFont = newFont;
                            
                            // Maintain the selection
                            textBox.Select(selectionStart, selectionLength);
                            textBox.Focus();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error applying text style: {ex.Message}");
                        }
                    }));
                });
            }
            else
            {
                // For small selections, apply immediately
                var newFont = GetFontCache().GetFont(currentFont.FontFamily.Name, currentFont.Size, newStyle);
                textBox.SelectionFont = newFont;
                
                // Maintain the selection
                textBox.Select(selectionStart, selectionLength);
                textBox.Focus();
            }
        }

        private void TextBox_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files?.Length == 1)
                {
                    string ext = Path.GetExtension(files[0]).ToLower();
                    if (ext == ".txt" || ext == ".srt" || ext == ".md")
                    {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private async void TextBox_DragDrop(object? sender, DragEventArgs e)
        {
            string[]? files = e.Data?.GetData(DataFormats.FileDrop) as string[];
            if (files?.Length == 1)
            {
                try
                {
                    await LoadFileAsync(files[0]);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task LoadFileAsync(string filePath)
        {
            try
            {
                // Start performance monitoring for file load
                performanceMonitor?.StartFileOperation("load", filePath, new FileInfo(filePath).Length);
                performanceStatusBar?.SetOperationStatus("Loading file...");

                // Check file size and show warning if needed
                var loadChoice = await FileSizeWarningService.ShowFileSizeWarningAsync(this, filePath);
                
                if (loadChoice == FileLoadChoice.Cancel)
                {
                    performanceMonitor?.EndFileOperation("load", filePath, false, "User cancelled due to file size");
                    performanceStatusBar?.ClearOperationStatus();
                    return;
                }

                // Check if file should use streaming
                var shouldUseStreaming = FileService.ShouldUseStreaming(filePath) || loadChoice == FileLoadChoice.LoadStreaming;
                
                if (shouldUseStreaming)
                {
                    // Enable streaming mode in document model
                    var fileInfo = await FileService.AnalyzeFileForStreamingAsync(filePath);
                    document.EnableStreamingMode(fileInfo);
                    document.FilePath = filePath;
                    
                    // Load file using VirtualTextBox streaming
                    performanceStatusBar?.SetOperationStatus("Loading file (streaming mode)...");
                    await textBox.LoadFileAsync(filePath);
                    
                    // Update UI for streaming mode
                    autoSaveLabel.Text = $"Large file loaded (streaming): {Path.GetFileName(filePath)}";
                    performanceMonitor?.EndFileOperation("load", filePath, true);
                }
                else
                {
                    // Use regular loading for smaller files
                    document.DisableStreamingMode();
                    performanceStatusBar?.SetOperationStatus("Loading file (normal mode)...");
                    var (content, hyperlinks) = await FileService.LoadFileAsync(filePath);
                    textBox.Text = content;
                    document.FilePath = filePath;
                    document.Hyperlinks = hyperlinks;
                    UpdateHyperlinkRendering();
                    autoSaveLabel.Text = $"Autosave ready for: {Path.GetFileName(filePath)}";
                    performanceMonitor?.EndFileOperation("load", filePath, true);
                }
                
                document.ResetDirty();
                performanceStatusBar?.ClearOperationStatus();
                
                // Start autosave timer
                autoSaveTimer.Stop();
                autoSaveTimer.Start();
            }
            catch (Exception ex)
            {
                performanceMonitor?.EndFileOperation("load", filePath, false, ex.Message);
                performanceStatusBar?.ClearOperationStatus();
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SaveFileAsync(string filePath, string content, List<HyperlinkModel>? hyperlinks)
        {
            try
            {
                // Start performance monitoring for save operation
                var fileSize = content?.Length * sizeof(char) ?? 0;
                performanceMonitor?.StartFileOperation("save", filePath, fileSize);
                performanceStatusBar?.SetOperationStatus("Saving file...");

                if (document.IsStreamingMode)
                {
                    await FileService.SaveStreamingFileAsync(filePath, content, hyperlinks);
                }
                else
                {
                    await FileService.SaveFileAsync(filePath, content, hyperlinks);
                }

                performanceMonitor?.EndFileOperation("save", filePath, true);
            }
            catch (Exception ex)
            {
                performanceMonitor?.EndFileOperation("save", filePath, false, ex.Message);
                throw; // Re-throw to maintain existing error handling
            }
            finally
            {
                performanceStatusBar?.ClearOperationStatus();
            }
        }

        private void VirtualTextBox_TextChanged(object? sender, VirtualTextBox.TextChangedEventArgs e)
        {
            // Update document model with changes
            document.SetStreamingContent(e.NewText, true);
            
            // Update undo buffer if not already in undo/redo operation
            if (!isUndoRedoOperation)
            {
                undoBuffer.SaveState(e.NewText, document.Hyperlinks, e.SelectionStart, e.SelectionLength);
            }
            
            // Update last text content tracking
            lastTextContent = e.NewText;
            lastSelectionStart = e.SelectionStart;
        }

        private void VirtualTextBox_LoadProgressChanged(object? sender, VirtualTextBox.ProgressEventArgs e)
        {
            // Update UI with loading progress
            autoSaveLabel.Text = $"{e.Status} - {e.PercentComplete}% complete";
        }

        private async void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(document.FilePath) && document.IsDirty)
            {
                // Cancel any previous auto-save operation
                autoSaveCancellationTokenSource?.Cancel();
                autoSaveCancellationTokenSource = new CancellationTokenSource();
                
                try
                {
                    await PerformAutoSaveAsync(autoSaveCancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Auto-save was cancelled, this is expected
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Autosave failed: {ex.Message}");
                    
                    // Update UI on main thread
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() => 
                            autoSaveLabel.Text = $"Autosave failed: {DateTime.Now.ToString("HH:mm:ss")}"));
                    }
                    else
                    {
                        autoSaveLabel.Text = $"Autosave failed: {DateTime.Now.ToString("HH:mm:ss")}";
                    }
                }
            }
        }
        
        private async Task PerformAutoSaveAsync(CancellationToken cancellationToken)
        {
            // Capture current state on UI thread
            string filePath;
            string content;
            List<HyperlinkModel> hyperlinks;
            
            if (InvokeRequired)
            {
                var tcs = new TaskCompletionSource<(string, string, List<HyperlinkModel>)>();
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        tcs.SetResult((document.FilePath, textBox.Text, document.Hyperlinks.ToList()));
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }));
                (filePath, content, hyperlinks) = await tcs.Task.ConfigureAwait(false);
            }
            else
            {
                filePath = document.FilePath;
                content = textBox.Text;
                hyperlinks = document.Hyperlinks.ToList();
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Perform file save on background thread
            if (document.IsStreamingMode)
            {
                await FileService.SaveStreamingFileAsync(filePath, content, hyperlinks, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await FileService.SaveFileAsync(filePath, content, hyperlinks, cancellationToken).ConfigureAwait(false);
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // Update UI on main thread
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        document.ResetDirty();
                        autoSaveLabel.Text = $"Last autosave: {DateTime.Now.ToString("HH:mm:ss")}";
                    }
                }));
            }
            else
            {
                document.ResetDirty();
                autoSaveLabel.Text = $"Last autosave: {DateTime.Now.ToString("HH:mm:ss")}";
            }
        }

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (e.Clicks == 2)
                    {
                        maximizeButton.PerformClick();
                    }
                    else
                    {
                        if (ReleaseCapture())
                        {
                            _ = SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TitleBar_MouseDown: {ex.Message}");
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            
            if (titleBar != null)
            {
                titleBar.BringToFront();
            }
            if (bottomToolbar != null)
            {
                bottomToolbar.BringToFront();
            }
        }

        private void InitializeButtons()
        {
            foreach (Button button in new[] { closeButton, maximizeButton, minimizeButton })
            {
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = isDarkMode ? 
                    Color.FromArgb(60, 60, 60) : Color.LightGray;
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.FlatAppearance.MouseDownBackColor = isDarkMode ? 
                    Color.FromArgb(80, 80, 80) : Color.DarkGray;
            }

            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = Color.Red;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = isDarkMode ? darkForeColor : Color.Gray;
        }

        // Add this method to handle text box refresh
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            textBox?.Refresh();  // Refresh textbox when form gets focus
        }

        private void ShowFindReplaceDialog()
        {
            if (findReplaceDialog == null || findReplaceDialog.IsDisposed)
            {
                findReplaceDialog = new FindReplaceDialog(textBox.GetUnderlyingRichTextBox(), isDarkMode);
                findReplaceDialog.Owner = this;
            }

            if (textBox.SelectionLength > 0)
            {
                findReplaceDialog.SetSearchText(textBox.SelectedText);
            }

            findReplaceDialog.Show();
            findReplaceDialog.BringToFront();
            findReplaceDialog.Focus();
        }

        private void CleanupResources()
        {
            findReplaceDialog?.Dispose();
            autoSaveTimer?.Dispose();
            hyperlinkUpdateTimer?.Dispose();
            textBox?.Dispose();
            titleBar?.Dispose();
            closeButton?.Dispose();
            maximizeButton?.Dispose();
            minimizeButton?.Dispose();
            bottomToolbar?.Dispose();
            saveButton?.Dispose();
            quickSaveButton?.Dispose();
            fontButton?.Dispose();
            hyperlinkButton?.Dispose();
            themeToggleButton?.Dispose();
            autoSaveLabel?.Dispose();
            
            // Dispose memory management components
            fontCache?.Dispose();
            undoBuffer?.Dispose();
            memoryMonitor?.Dispose();
            
            // Dispose performance monitoring components
            performanceDialog?.Close();
            performanceDialog?.Dispose();
            performanceStatusBar?.Dispose();
            performanceMonitor?.Dispose();
            
            // Dispose static font cache in HyperlinkService
            HyperlinkService.DisposeFontCache();
        }

        partial void OnDisposing()
        {
            CleanupResources();
            
            // Cancel any ongoing operations
            autoSaveCancellationTokenSource?.Cancel();
            autoSaveCancellationTokenSource?.Dispose();
            themeRefreshCancellationTokenSource?.Cancel();
            themeRefreshCancellationTokenSource?.Dispose();
            hyperlinkCancellationTokenSource?.Cancel();
            hyperlinkCancellationTokenSource?.Dispose();
            
            // Dispose the semaphores
            hyperlinkUpdateSemaphore?.Dispose();
            themeRefreshSemaphore?.Dispose();
        }

        private void FontButton_Click(object? sender, EventArgs e)
        {
            Font currentFont = textBox.SelectionLength > 0 && textBox.SelectionFont != null
                ? textBox.SelectionFont
                : textBox.Font;
                
            using (CustomFontDialog fontDialog = new CustomFontDialog(isDarkMode, currentFont))
            {
                // Show the dialog and apply selection if OK was clicked
                if (fontDialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ApplyFontSelection(fontDialog.SelectedFont);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Font dialog error: {ex.Message}");
                        MessageBox.Show("Error applying font selection", "Font Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ApplyFontSelection(Font newFont)
        {
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            if (selectionLength > 0)
            {
                // Apply to selection while preserving other styles
                for (int i = selectionStart; i < selectionStart + selectionLength; i++)
                {
                    textBox.Select(i, 1);
                    Font currentCharFont = textBox.SelectionFont ?? textBox.Font;
                    FontStyle combinedStyle = newFont.Style | (currentCharFont.Style & ~FontStyle.Regular);
                    var updatedFont = GetFontCache().GetFont(newFont.FontFamily.Name, newFont.Size, combinedStyle);
                    textBox.SelectionFont = updatedFont;
                }
            }
            else
            {
                // Change the entire textbox font - use cached font for memory efficiency
                var appliedFont = GetFontCache().GetFont(newFont.FontFamily.Name, newFont.Size, newFont.Style);
                textBox.TextFont = appliedFont;
                currentFontSize = newFont.Size;
                
                // Force refresh to ensure the change takes effect
                textBox.Invalidate();
                textBox.Update();
            }

            // Restore the selection
            textBox.Select(selectionStart, selectionLength);
            textBox.Focus();
        }

        private async void ThemeToggleButton_Click(object? sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            performanceStatusBar?.ApplyTheme(isDarkMode);
            await RefreshThemeAsync().ConfigureAwait(false);
        }

        private void UpdatePanelColors(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Panel && control != bottomToolbar && control != titleBar)
                {
                    control.BackColor = isDarkMode ? darkBackColor : Color.White;
                    // Recursively update nested panels
                    UpdatePanelColors(control);
                }
            }
        }

        private async Task RefreshThemeAsync()
        {
            // Prevent concurrent theme refresh operations
            if (isThemeRefreshInProgress)
            {
                return;
            }
            
            // Cancel any existing theme refresh
            themeRefreshCancellationTokenSource?.Cancel();
            themeRefreshCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = themeRefreshCancellationTokenSource.Token;
            
            // Use semaphore to prevent multiple concurrent theme refreshes
            if (!await themeRefreshSemaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return; // Another theme refresh is already in progress
            }
            
            try
            {
                isThemeRefreshInProgress = true;
                
                // Capture current state for background processing
                var currentIsDarkMode = isDarkMode;
                var currentHyperlinks = document.Hyperlinks.ToList();
                var currentText = textBox.Text;
                var currentSelectionStart = textBox.SelectionStart;
                var currentSelectionLength = textBox.SelectionLength;
                
                // Process text formatting in background
                var formattingOperations = await Task.Run(() =>
                {
                    var operations = new List<HyperlinkService.FormattingOperation>();
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Determine colors based on theme
                    var hyperlinkColor = currentIsDarkMode ? Color.FromArgb(77, 166, 255) : Color.Blue;
                    var defaultColor = currentIsDarkMode ? Color.FromArgb(220, 220, 220) : Color.Black;
                    
                    // Create operation to reset all text to default color
                    if (currentText.Length > 0)
                    {
                        operations.Add(new HyperlinkService.FormattingOperation
                        {
                            StartIndex = 0,
                            Length = currentText.Length,
                            Color = defaultColor,
                            IsUnderlined = false
                        });
                        
                        // Add hyperlink formatting operations
                        foreach (var hyperlink in currentHyperlinks)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            if (hyperlink.StartIndex >= 0 && hyperlink.EndIndex <= currentText.Length)
                            {
                                operations.Add(new HyperlinkService.FormattingOperation
                                {
                                    StartIndex = hyperlink.StartIndex,
                                    Length = hyperlink.Length,
                                    Color = hyperlinkColor,
                                    IsUnderlined = true
                                });
                            }
                        }
                    }
                    
                    return operations;
                }, cancellationToken).ConfigureAwait(true);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // Apply UI changes on main thread
                if (InvokeRequired)
                {
                    await Task.Factory.StartNew(() =>
                    {
                        Invoke(new Action(() => ApplyThemeChanges(formattingOperations, currentSelectionStart, currentSelectionLength, cancellationToken)));
                    }, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default).ConfigureAwait(false);
                }
                else
                {
                    ApplyThemeChanges(formattingOperations, currentSelectionStart, currentSelectionLength, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested - do nothing
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                Debug.WriteLine($"Error in theme refresh: {ex.Message}");
            }
            finally
            {
                isThemeRefreshInProgress = false;
                themeRefreshSemaphore.Release();
            }
        }
        
        private void ApplyThemeChanges(List<HyperlinkService.FormattingOperation> formattingOperations, 
            int savedSelectionStart, int savedSelectionLength, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Update form colors
                this.BackColor = isDarkMode ? darkBackColor : Color.White;
                
                // Update all panels to prevent dark edges
                UpdatePanelColors(this);
                
                // Update text box colors
                textBox.BackColor = isDarkMode ? darkBackColor : Color.White;
                textBox.ForeColor = isDarkMode ? darkForeColor : Color.Black;
                
                // Apply text formatting operations efficiently
                if (formattingOperations.Count > 0)
                {
                    HyperlinkService.ApplyFormattingOperations(textBox.GetUnderlyingRichTextBox(), formattingOperations, textBox.TextFont);
                }
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // Update bottom toolbar
                bottomToolbar.BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke;
                
                // Update buttons
                saveButton.ForeColor = isDarkMode ? Color.FromArgb(130, 180, 255) : Color.RoyalBlue;
                quickSaveButton.ForeColor = isDarkMode ? Color.FromArgb(130, 255, 130) : Color.Green;
                fontButton.ForeColor = isDarkMode ? Color.FromArgb(180, 180, 255) : Color.RoyalBlue;
                hyperlinkButton.ForeColor = isDarkMode ? Color.FromArgb(100, 200, 255) : Color.Blue;
                
                // Update theme toggle button
                themeToggleButton.Text = isDarkMode ? "☀️" : "🌙";
                themeToggleButton.ForeColor = isDarkMode ? Color.FromArgb(255, 223, 0) : Color.FromArgb(100, 100, 200);
                
                // Update auto save label
                autoSaveLabel.ForeColor = isDarkMode ? darkForeColor : Color.Gray;
                
                // Update title bar
                titleBar.BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke;
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // Update window control buttons
                foreach (Button button in new[] { closeButton, maximizeButton, minimizeButton })
                {
                    button.ForeColor = isDarkMode ? darkForeColor : Color.Gray;
                    button.FlatAppearance.MouseOverBackColor = isDarkMode ? 
                        Color.FromArgb(60, 60, 60) : Color.LightGray;
                    button.FlatAppearance.MouseDownBackColor = isDarkMode ? 
                        Color.FromArgb(80, 80, 80) : Color.DarkGray;
                }
                
                // Update hover effects
                UpdateButtonHoverHandlers();
                
                // Update context menu
                if (textBoxContextMenu != null)
                {
                    if (isDarkMode)
                    {
                        textBoxContextMenu.BackColor = darkToolbarColor;
                        textBoxContextMenu.ForeColor = darkForeColor;
                        textBoxContextMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
                    }
                    else
                    {
                        textBoxContextMenu.BackColor = SystemColors.Control;
                        textBoxContextMenu.ForeColor = SystemColors.ControlText;
                        textBoxContextMenu.Renderer = new ToolStripProfessionalRenderer();
                    }
                }
                
                // Update find/replace dialog if it exists
                if (findReplaceDialog != null && !findReplaceDialog.IsDisposed)
                {
                    findReplaceDialog.Dispose();
                    findReplaceDialog = null;
                }
                
                // Restore selection if still valid
                if (savedSelectionStart >= 0 && savedSelectionStart <= textBox.TextLength)
                {
                    var safeSelectionLength = Math.Min(savedSelectionLength, textBox.TextLength - savedSelectionStart);
                    textBox.Select(savedSelectionStart, Math.Max(0, safeSelectionLength));
                }
                
                // Refresh the form
                this.Refresh();
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying theme changes: {ex.Message}");
            }
        }

        private void HyperlinkUpdateTimer_Tick(object? sender, EventArgs e)
        {
            hyperlinkUpdateTimer.Stop();
            UpdateHyperlinkRendering();
        }

        private void SaveUndoState()
        {
            if (isUndoRedoOperation || undoBuffer == null) return;
            
            // Use efficient undo buffer with memory limits and diff-based storage
            undoBuffer.SaveState(
                textBox.Text,
                document.Hyperlinks.Select(h => h.Clone()).ToList(),
                textBox.SelectionStart,
                textBox.SelectionLength
            );
            
            // Monitor memory usage (deferred if not yet initialized)
            var monitor = GetMemoryMonitor();
            _ = Task.Run(() => monitor.CheckMemoryPressure());
        }

        private void RestoreState(UndoBuffer.UndoState state)
        {
            isUndoRedoOperation = true;
            try
            {
                // Restore text
                textBox.Text = state.Text;
                
                // Restore hyperlinks
                document.Hyperlinks.Clear();
                foreach (var hyperlink in state.Hyperlinks)
                {
                    document.Hyperlinks.Add(hyperlink.Clone());
                }
                
                // Update hyperlink rendering
                UpdateHyperlinkRendering();
                
                // Restore selection
                textBox.Select(state.SelectionStart, state.SelectionLength);
            }
            finally
            {
                isUndoRedoOperation = false;
            }
        }

        private void PerformUndo()
        {
            var undoState = undoBuffer.Undo();
            if (undoState != null)
            {
                RestoreState(undoState);
            }
        }

        private void PerformRedo()
        {
            var redoState = undoBuffer.Redo();
            if (redoState != null)
            {
                RestoreState(redoState);
            }
        }

        private void UpdateButtonHoverHandlers()
        {
            // Remove old handlers first to avoid duplicates
            saveButton.MouseEnter -= SaveButton_MouseEnter;
            saveButton.MouseLeave -= SaveButton_MouseLeave;
            quickSaveButton.MouseEnter -= QuickSaveButton_MouseEnter;
            quickSaveButton.MouseLeave -= QuickSaveButton_MouseLeave;
            fontButton.MouseEnter -= FontButton_MouseEnter;
            fontButton.MouseLeave -= FontButton_MouseLeave;
            hyperlinkButton.MouseEnter -= HyperlinkButton_MouseEnter;
            hyperlinkButton.MouseLeave -= HyperlinkButton_MouseLeave;
            themeToggleButton.MouseEnter -= ThemeToggleButton_MouseEnter;
            themeToggleButton.MouseLeave -= ThemeToggleButton_MouseLeave;
            
            // Add updated handlers
            saveButton.MouseEnter += SaveButton_MouseEnter;
            saveButton.MouseLeave += SaveButton_MouseLeave;
            quickSaveButton.MouseEnter += QuickSaveButton_MouseEnter;
            quickSaveButton.MouseLeave += QuickSaveButton_MouseLeave;
            fontButton.MouseEnter += FontButton_MouseEnter;
            fontButton.MouseLeave += FontButton_MouseLeave;
            hyperlinkButton.MouseEnter += HyperlinkButton_MouseEnter;
            hyperlinkButton.MouseLeave += HyperlinkButton_MouseLeave;
            themeToggleButton.MouseEnter += ThemeToggleButton_MouseEnter;
            themeToggleButton.MouseLeave += ThemeToggleButton_MouseLeave;
        }

        private void SaveButton_MouseEnter(object? sender, EventArgs e) => 
            saveButton.ForeColor = isDarkMode ? Color.FromArgb(160, 200, 255) : Color.DodgerBlue;
        private void SaveButton_MouseLeave(object? sender, EventArgs e) => 
            saveButton.ForeColor = isDarkMode ? Color.FromArgb(130, 180, 255) : Color.RoyalBlue;
            
        private void QuickSaveButton_MouseEnter(object? sender, EventArgs e) => 
            quickSaveButton.ForeColor = isDarkMode ? Color.FromArgb(160, 255, 160) : Color.LimeGreen;
        private void QuickSaveButton_MouseLeave(object? sender, EventArgs e) => 
            quickSaveButton.ForeColor = isDarkMode ? Color.FromArgb(130, 255, 130) : Color.Green;
            
        private void FontButton_MouseEnter(object? sender, EventArgs e) => 
            fontButton.ForeColor = isDarkMode ? Color.FromArgb(200, 200, 255) : Color.DodgerBlue;
        private void FontButton_MouseLeave(object? sender, EventArgs e) => 
            fontButton.ForeColor = isDarkMode ? Color.FromArgb(180, 180, 255) : Color.RoyalBlue;
            
        private void HyperlinkButton_MouseEnter(object? sender, EventArgs e) => 
            hyperlinkButton.ForeColor = isDarkMode ? Color.FromArgb(130, 220, 255) : Color.DodgerBlue;
        private void HyperlinkButton_MouseLeave(object? sender, EventArgs e) => 
            hyperlinkButton.ForeColor = isDarkMode ? Color.FromArgb(100, 200, 255) : Color.Blue;
            
        private void ThemeToggleButton_MouseEnter(object? sender, EventArgs e) => 
            themeToggleButton.ForeColor = isDarkMode ? Color.FromArgb(255, 240, 100) : Color.FromArgb(150, 150, 255);
        private void ThemeToggleButton_MouseLeave(object? sender, EventArgs e) => 
            themeToggleButton.ForeColor = isDarkMode ? Color.FromArgb(255, 223, 0) : Color.FromArgb(100, 100, 200);

        private void InitializeContextMenu()
        {
            textBoxContextMenu = new ContextMenuStrip();
            
            var cutItem = new ToolStripMenuItem("Cut", null, (s, e) => textBox.Cut());
            cutItem.ShortcutKeys = Keys.Control | Keys.X;
            
            var copyItem = new ToolStripMenuItem("Copy", null, (s, e) => CopyWithHyperlinks());
            copyItem.ShortcutKeys = Keys.Control | Keys.C;
            
            var pasteItem = new ToolStripMenuItem("Paste", null, (s, e) => textBox.Paste());
            pasteItem.ShortcutKeys = Keys.Control | Keys.V;
            
            var selectAllItem = new ToolStripMenuItem("Select All", null, (s, e) => textBox.SelectAll());
            selectAllItem.ShortcutKeys = Keys.Control | Keys.A;
            
            var hyperlinkItem = new ToolStripMenuItem("Add Hyperlink...", null, (s, e) => ShowHyperlinkDialog());
            hyperlinkItem.ShortcutKeys = Keys.Control | Keys.K;
            
            textBoxContextMenu.Items.AddRange(new ToolStripItem[] {
                cutItem,
                copyItem,
                pasteItem,
                new ToolStripSeparator(),
                selectAllItem,
                new ToolStripSeparator(),
                hyperlinkItem
            });
            
            textBoxContextMenu.Opening += (s, e) =>
            {
                hyperlinkItem.Enabled = textBox.SelectionLength > 0;
                cutItem.Enabled = textBox.SelectionLength > 0;
                copyItem.Enabled = textBox.SelectionLength > 0;
                
                var hyperlink = document.GetHyperlinkAtPosition(textBox.SelectionStart);
                if (hyperlink != null)
                {
                    hyperlinkItem.Text = "Edit Hyperlink...";
                }
                else
                {
                    hyperlinkItem.Text = "Add Hyperlink...";
                }
            };
            
            if (isDarkMode)
            {
                textBoxContextMenu.BackColor = darkToolbarColor;
                textBoxContextMenu.ForeColor = darkForeColor;
                textBoxContextMenu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            }
            else
            {
                textBoxContextMenu.BackColor = SystemColors.Control;
                textBoxContextMenu.ForeColor = SystemColors.ControlText;
                textBoxContextMenu.Renderer = new ToolStripProfessionalRenderer();
            }
        }

        private void HyperlinkButton_Click(object? sender, EventArgs e)
        {
            ShowHyperlinkDialog();
        }

        private void ShowHyperlinkDialog()
        {
            if (textBox.SelectionLength == 0 && document.GetHyperlinkAtPosition(textBox.SelectionStart) == null)
            {
                MessageBox.Show("Please select text to create a hyperlink.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var existingHyperlink = document.GetHyperlinkAtPosition(textBox.SelectionStart);
            string? existingUrl = existingHyperlink?.Url;
            string? existingText = existingHyperlink?.DisplayText;

            if (existingHyperlink == null)
            {
                existingText = textBox.SelectedText;
            }

            using var dialog = new HyperlinkDialog(isDarkMode, existingUrl, existingText);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                if (dialog.RemoveHyperlink && existingHyperlink != null)
                {
                    document.RemoveHyperlink(existingHyperlink);
                    UpdateHyperlinkRendering();
                    SaveUndoState(); // Save state after removing hyperlink
                }
                else if (!dialog.RemoveHyperlink)
                {
                    if (existingHyperlink != null)
                    {
                        existingHyperlink.Url = dialog.Url;
                        // Keep the existing display text
                    }
                    else
                    {
                        var newHyperlink = new HyperlinkModel
                        {
                            StartIndex = textBox.SelectionStart,
                            Length = textBox.SelectionLength,
                            Url = dialog.Url,
                            DisplayText = textBox.SelectedText
                        };
                        document.AddHyperlink(newHyperlink);
                    }
                    UpdateHyperlinkRendering();
                    SaveUndoState(); // Save state after adding/editing hyperlink
                }
            }
        }

        private void TextBox_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int charIndex = textBox.GetCharIndexFromPosition(e.Location);
                
                // Validate that we're actually over text, not empty space
                if (charIndex >= 0 && charIndex < textBox.TextLength)
                {
                    // Get the actual bounds of this character
                    Point charPos = textBox.GetPositionFromCharIndex(charIndex);
                    Point nextCharPos = charIndex + 1 < textBox.TextLength ? 
                        textBox.GetPositionFromCharIndex(charIndex + 1) : 
                        new Point(charPos.X + 10, charPos.Y);
                    
                    // For characters at end of line, use line height for bounds checking
                    Rectangle charBounds;
                    if (nextCharPos.Y > charPos.Y || charIndex + 1 >= textBox.TextLength)
                    {
                        // Character is at end of line
                        using (Graphics g = textBox.CreateGraphics())
                        {
                            SizeF charSize = g.MeasureString(textBox.Text[charIndex].ToString(), textBox.Font);
                            charBounds = new Rectangle(charPos.X, charPos.Y, (int)charSize.Width, (int)charSize.Height);
                        }
                    }
                    else
                    {
                        // Normal character in middle of line
                        charBounds = new Rectangle(charPos.X, charPos.Y, nextCharPos.X - charPos.X, textBox.Font.Height);
                    }
                    
                    // Check if click is actually within character bounds
                    if (charBounds.Contains(e.Location))
                    {
                        var hyperlink = document.GetHyperlinkAtPosition(charIndex);
                        if (hyperlink != null)
                        {
                            try
                            {
                                string url = hyperlink.Url;
                                
                                // Add https:// if no protocol is specified
                                if (!url.Contains("://") && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                                {
                                    url = "https://" + url;
                                }
                                
                                System.Diagnostics.Process.Start(new ProcessStartInfo
                                {
                                    FileName = url,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Unable to open link: {ex.Message}", "Error", 
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
        }

        private void TextBox_MouseMove(object? sender, MouseEventArgs e)
        {
            int charIndex = textBox.GetCharIndexFromPosition(e.Location);
            
            // Validate that we're actually over text, not empty space
            if (charIndex >= 0 && charIndex < textBox.TextLength)
            {
                // Get the actual bounds of this character
                Point charPos = textBox.GetPositionFromCharIndex(charIndex);
                Point nextCharPos = charIndex + 1 < textBox.TextLength ? 
                    textBox.GetPositionFromCharIndex(charIndex + 1) : 
                    new Point(charPos.X + 10, charPos.Y);
                
                // For characters at end of line, use line height for bounds checking
                Rectangle charBounds;
                if (nextCharPos.Y > charPos.Y || charIndex + 1 >= textBox.TextLength)
                {
                    // Character is at end of line
                    using (Graphics g = textBox.CreateGraphics())
                    {
                        SizeF charSize = g.MeasureString(textBox.Text[charIndex].ToString(), textBox.Font);
                        charBounds = new Rectangle(charPos.X, charPos.Y, (int)charSize.Width, (int)charSize.Height);
                    }
                }
                else
                {
                    // Normal character in middle of line
                    charBounds = new Rectangle(charPos.X, charPos.Y, nextCharPos.X - charPos.X, textBox.Font.Height);
                }
                
                // Check if mouse is actually within character bounds
                if (charBounds.Contains(e.Location))
                {
                    var hyperlink = document.GetHyperlinkAtPosition(charIndex);
                    if (hyperlink != null)
                    {
                        textBox.Cursor = Cursors.Hand;
                        return;
                    }
                }
            }
            
            textBox.Cursor = Cursors.IBeam;
        }

        private void TextBox_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int charIndex = textBox.GetCharIndexFromPosition(e.Location);
                if (charIndex >= 0 && charIndex < textBox.TextLength)
                {
                    // Find word boundaries
                    int start = charIndex;
                    int end = charIndex;
                    
                    // Move start backwards to find beginning of word
                    while (start > 0 && !char.IsWhiteSpace(textBox.Text[start - 1]) && 
                           !char.IsPunctuation(textBox.Text[start - 1]))
                    {
                        start--;
                    }
                    
                    // Move end forward to find end of word
                    while (end < textBox.TextLength && !char.IsWhiteSpace(textBox.Text[end]) && 
                           !char.IsPunctuation(textBox.Text[end]))
                    {
                        end++;
                    }
                    
                    // Select the word without trailing space
                    textBox.Select(start, end - start);
                }
            }
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            // Handle Ctrl+Z for undo
            if (e.Control && e.KeyCode == Keys.Z && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                PerformUndo();
                return;
            }
            
            // Handle Ctrl+Y or Ctrl+Shift+Z for redo
            if ((e.Control && e.KeyCode == Keys.Y) || (e.Control && e.Shift && e.KeyCode == Keys.Z))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                PerformRedo();
                return;
            }
            
            // Handle Ctrl+V for paste
            if (e.Control && e.KeyCode == Keys.V)
            {
                SaveUndoState(); // Save state before paste
            }
            
            // Handle Ctrl+X for cut
            if (e.Control && e.KeyCode == Keys.X)
            {
                SaveUndoState(); // Save state before cut
            }
            
            // Save state before any text-modifying key
            if (!e.Control && !isUndoRedoOperation && 
                (char.IsLetterOrDigit((char)e.KeyValue) || 
                 e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                 e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space ||
                 e.KeyCode == Keys.Tab))
            {
                SaveUndoState();
            }
            
            // Handle Ctrl+K for hyperlink dialog
            if (e.Control && e.KeyCode == Keys.K)
            {
                SaveUndoState(); // Save state before hyperlink changes
                e.Handled = true;
                ShowHyperlinkDialog();
            }
            // Prevent any other control key combinations from affecting hyperlinks
            else if (e.Control && document.Hyperlinks.Count > 0)
            {
                // Don't interfere with standard shortcuts
                if (e.KeyCode != Keys.C && e.KeyCode != Keys.V && e.KeyCode != Keys.X && 
                    e.KeyCode != Keys.A && e.KeyCode != Keys.Z && e.KeyCode != Keys.Y &&
                    e.KeyCode != Keys.S && e.KeyCode != Keys.O && e.KeyCode != Keys.F)
                {
                    // For any other control key combination, don't trigger hyperlink updates
                    e.SuppressKeyPress = false;
                }
            }
        }

        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            if (isUndoRedoOperation) return;
            
            document.IsDirty = true;
            
            // Calculate the change in text
            string currentText = textBox.Text;
            int currentSelectionStart = textBox.SelectionStart;
            
            // Simple heuristic: if selection moved forward and text is longer, insertion occurred
            // if selection stayed same/moved back and text is shorter, deletion occurred
            int lengthDiff = currentText.Length - lastTextContent.Length;
            
            if (lengthDiff != 0)
            {
                int changeIndex = currentSelectionStart - Math.Max(0, lengthDiff);
                
                // Check if any hyperlinks exist and if the change affects them
                bool needsHyperlinkUpdate = false;
                
                if (document.Hyperlinks.Count > 0)
                {
                    // Get the position of the last hyperlink
                    int lastHyperlinkEnd = document.Hyperlinks.Max(h => h.EndIndex);
                    
                    // Only update if the change is before or within hyperlinks
                    if (changeIndex <= lastHyperlinkEnd)
                    {
                        needsHyperlinkUpdate = true;
                        document.UpdateHyperlinksAfterTextChange(changeIndex, lengthDiff);
                    }
                }
                
                // Only update rendering if hyperlinks were actually affected
                // Use debounced timer to avoid blocking during rapid typing
                if (needsHyperlinkUpdate)
                {
                    hyperlinkUpdateTimer.Stop();
                    UpdateHyperlinkTimerInterval(); // Adjust timer delay based on document size
                    hyperlinkUpdateTimer.Start();
                }
            }
            
            lastTextContent = currentText;
            lastSelectionStart = currentSelectionStart;
        }

        private void UpdateHyperlinkRendering()
        {
            _ = UpdateHyperlinkRenderingAsync();
        }
        
        private async Task UpdateHyperlinkRenderingAsync()
        {
            // Prevent concurrent hyperlink updates
            if (isHyperlinkUpdateInProgress)
            {
                return;
            }
            
            // Cancel any existing hyperlink processing
            hyperlinkCancellationTokenSource?.Cancel();
            hyperlinkCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = hyperlinkCancellationTokenSource.Token;
            
            // Use semaphore to prevent multiple concurrent updates
            if (!await hyperlinkUpdateSemaphore.WaitAsync(0, cancellationToken).ConfigureAwait(true))
            {
                return; // Another update is already in progress
            }
            
            try
            {
                isHyperlinkUpdateInProgress = true;
                
                // Capture current state on UI thread
                var currentText = textBox.Text;
                var currentHyperlinks = document.Hyperlinks.ToList();
                var currentIsDarkMode = isDarkMode;
                var currentFont = textBox.Font;
                
                // Skip if text is empty
                if (string.IsNullOrEmpty(currentText))
                {
                    return;
                }
                
                // Process hyperlinks in background
                var formattingOperations = await HyperlinkService.ProcessHyperlinksAsync(
                    currentText, currentHyperlinks, currentIsDarkMode, cancellationToken)
                    .ConfigureAwait(true);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // Apply formatting on UI thread using BeginInvoke for non-blocking operation
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                HyperlinkService.ApplyFormattingOperations(textBox.GetUnderlyingRichTextBox(), formattingOperations, currentFont);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when cancellation is requested
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't crash the application
                            System.Diagnostics.Debug.WriteLine($"Error applying hyperlink formatting: {ex.Message}");
                        }
                    }));
                }
                else
                {
                    HyperlinkService.ApplyFormattingOperations(textBox.GetUnderlyingRichTextBox(), formattingOperations, currentFont);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested - do nothing
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error in hyperlink rendering: {ex.Message}");
            }
            finally
            {
                isHyperlinkUpdateInProgress = false;
                hyperlinkUpdateSemaphore.Release();
            }
        }

        /// <summary>
        /// Adjusts hyperlink update timer delay based on document size for optimal performance
        /// </summary>
        private void UpdateHyperlinkTimerInterval()
        {
            int textLength = textBox?.Text?.Length ?? 0;
            
            // Skip if text length hasn't changed significantly (within 10% or 5000 characters)
            if (lastTimerIntervalTextLength >= 0)
            {
                int lengthDiff = Math.Abs(textLength - lastTimerIntervalTextLength);
                int threshold = Math.Max(5000, lastTimerIntervalTextLength / 10);
                if (lengthDiff < threshold)
                {
                    return; // No significant change, keep current interval
                }
            }
            
            int newInterval;
            
            if (textLength > 250000) // Very large documents (>250KB)
            {
                newInterval = 1000; // 1 second delay
            }
            else if (textLength > 100000) // Large documents (>100KB)
            {
                newInterval = 750; // 750ms delay
            }
            else if (textLength > 50000) // Medium documents (>50KB)
            {
                newInterval = 500; // 500ms delay
            }
            else
            {
                newInterval = 250; // Default 250ms delay for small documents
            }
            
            // Only update if interval changed to avoid unnecessary timer restarts
            if (hyperlinkUpdateTimer.Interval != newInterval)
            {
                hyperlinkUpdateTimer.Interval = newInterval;
                lastTimerIntervalTextLength = textLength; // Update tracked length
            }
        }

        private void CopyWithHyperlinks()
        {
            if (textBox.SelectionLength == 0)
                return;

            string selectedText = textBox.SelectedText;
            int selectionStart = textBox.SelectionStart;
            int selectionEnd = selectionStart + textBox.SelectionLength;

            // Find hyperlinks within selection and adjust their display text
            var selectedHyperlinks = new List<HyperlinkModel>();
            
            foreach (var hyperlink in document.Hyperlinks)
            {
                // Check if hyperlink overlaps with selection
                if (hyperlink.StartIndex < selectionEnd && hyperlink.EndIndex > selectionStart)
                {
                    int linkStartInSelection = Math.Max(0, hyperlink.StartIndex - selectionStart);
                    int linkEndInSelection = Math.Min(selectionEnd - selectionStart, hyperlink.EndIndex - selectionStart);
                    int lengthInSelection = linkEndInSelection - linkStartInSelection;
                    
                    // Extract the actual text that's selected from this hyperlink
                    string displayTextInSelection = selectedText.Substring(linkStartInSelection, lengthInSelection);
                    
                    selectedHyperlinks.Add(new HyperlinkModel
                    {
                        StartIndex = linkStartInSelection,
                        Length = lengthInSelection,
                        Url = hyperlink.Url,
                        DisplayText = displayTextInSelection
                    });
                }
            }

            HyperlinkService.SetClipboardWithHyperlinks(selectedText, selectedHyperlinks);
        }

        /// <summary>
        /// Lazy-loaded FontCache getter with thread safety
        /// </summary>
        private FontCache GetFontCache()
        {
            if (fontCache == null)
            {
                lock (this)
                {
                    fontCache ??= new FontCache();
                }
            }
            return fontCache;
        }
        
        /// <summary>
        /// Lazy-loaded MemoryPressureMonitor getter with thread safety
        /// </summary>
        private MemoryPressureMonitor GetMemoryMonitor()
        {
            if (memoryMonitor == null)
            {
                lock (this)
                {
                    memoryMonitor ??= new MemoryPressureMonitor();
                }
            }
            return memoryMonitor;
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 60);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 60);
            public override Color MenuItemBorder => Color.FromArgb(100, 100, 100);
            public override Color MenuBorder => Color.FromArgb(100, 100, 100);
            public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 45);
        }
    }
}