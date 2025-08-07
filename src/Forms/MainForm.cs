using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ModernTextViewer.src.Services;
using ModernTextViewer.src.Models;
using System.Runtime.InteropServices.Marshalling;
using System.Diagnostics;
using System.IO;
using System.Drawing.Text;
using System.ComponentModel;
using System.Linq;

namespace ModernTextViewer.src.Forms
{
    public partial class MainForm : Form
    {
        private RichTextBox textBox = null!;
        private Panel titleBar = null!;
        private Button closeButton = null!;
        private Button maximizeButton = null!;
        private Button minimizeButton = null!;
        private Panel bottomToolbar = null!;
        private Button openButton = null!;
        private Button saveButton = null!;
        private Button quickSaveButton = null!;
        private Button fontButton = null!;
        private Button hyperlinkButton = null!;
        private Button themeToggleButton = null!;
        private ContextMenuStrip textBoxContextMenu = null!;
        private string lastTextContent = string.Empty;
        private ToolTip buttonToolTip = null!;
        private int lastSelectionStart = 0;
        private const int RESIZE_BORDER = 8;
        private const int TITLE_BAR_WIDTH = 32;
        private const float MIN_FONT_SIZE = 4f;
        private const float MAX_FONT_SIZE = 96f;
        private float currentFontSize = 10f;
        private readonly DocumentModel document = new DocumentModel();
        private bool isDarkMode = true;  // Default to dark mode
        private System.Windows.Forms.Timer autoSaveTimer;
        private readonly Color darkBackColor = Color.FromArgb(30, 30, 30);
        private readonly Color darkForeColor = Color.FromArgb(220, 220, 220);
        private readonly Color darkToolbarColor = Color.FromArgb(45, 45, 45);
        private Label autoSaveLabel = null!;
        private Label wordCountLabel = null!;
        private FindReplaceDialog? findReplaceDialog;
        
        // Undo/Redo state tracking
        private class UndoState
        {
            public string Text { get; set; } = "";
            public List<HyperlinkModel> Hyperlinks { get; set; } = new List<HyperlinkModel>();
            public int SelectionStart { get; set; }
            public int SelectionLength { get; set; }
        }
        
        private Stack<UndoState> undoStack = new Stack<UndoState>();
        private Stack<UndoState> redoStack = new Stack<UndoState>();
        private bool isUndoRedoOperation = false;
        private System.Windows.Forms.Timer hyperlinkUpdateTimer;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        public MainForm()
        {
            try
            {
                InitializeComponent();
                
                // Initialize timers
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
                
                // Initialize tooltips
                buttonToolTip = new ToolTip();
                buttonToolTip.ShowAlways = true;
                buttonToolTip.AutoPopDelay = 5000;
                
                InitializeUI();
                
                autoSaveTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing application: {ex.Message}", 
                    "Initialization Error", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
                throw;
            }
        }

        private void InitializeUI()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = isDarkMode ? darkBackColor : Color.White;
            this.Padding = new Padding(3);
            this.DoubleBuffered = true;
            this.MinimumSize = new Size(200, 100);

            InitializeBottomToolbar();
            InitializeTextBox();
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
                Font = new Font("Arial", fontSize),
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

            textBox = new RichTextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", currentFontSize),
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = isDarkMode ? darkBackColor : Color.White,
                ForeColor = isDarkMode ? darkForeColor : Color.Black,
                AllowDrop = true,
                TabStop = true,
                Enabled = true,
                AcceptsTab = true,
                DetectUrls = false
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
            textBox.ContextMenuStrip = textBoxContextMenu;

            // Add event handlers
            textBox.MouseWheel += TextBox_MouseWheel;
            textBox.DragEnter += TextBox_DragEnter;
            textBox.DragDrop += TextBox_DragDrop;
            textBox.TextChanged += TextBox_TextChanged;
            textBox.MouseClick += TextBox_MouseClick;
            textBox.MouseMove += TextBox_MouseMove;
            textBox.KeyDown += TextBox_KeyDown;
            textBox.MouseDoubleClick += TextBox_MouseDoubleClick;

            // Set up control hierarchy
            paddingPanel.Controls.Add(textBox);
            textBoxContainer.Controls.Add(paddingPanel);
            this.Controls.Add(textBoxContainer);

            textBox.Select();
            textBox.Focus();
            lastTextContent = textBox.Text;

            textBoxContainer.SendToBack();
            
            // Initialize word count
            UpdateWordCount();
            
            // Save initial state for undo
            SaveUndoState();
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

                    if (selectionLength > 0)
                    {
                        // Preserve the style of each character in the selection
                        for (int i = selectionStart; i < selectionStart + selectionLength; i++)
                        {
                            textBox.Select(i, 1);
                            Font currentCharFont = textBox.SelectionFont ?? textBox.Font;
                            FontStyle currentStyle = currentCharFont.Style;
                            using var newFont = new Font(currentCharFont.FontFamily, currentFontSize, currentStyle);
                            textBox.SelectionFont = newFont;
                        }
                    }
                    else
                    {
                        // Change entire textbox while preserving style
                        FontStyle currentStyle = textBox.Font.Style;
                        using var newFont = new Font(textBox.Font.FontFamily, currentFontSize, currentStyle);
                        textBox.Font = newFont;
                    }

                    // Restore the original selection
                    textBox.Select(selectionStart, selectionLength);
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

        private void InitializeBottomToolbar()
        {
            bottomToolbar = new Panel
            {
                Height = 30,
                Dock = DockStyle.Bottom,
                BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke
            };

            openButton = new Button
            {
                Text = "📁",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(255, 200, 130) : Color.DarkOrange
            };
            openButton.FlatAppearance.BorderSize = 0;
            openButton.Click += OpenButton_Click;
            buttonToolTip.SetToolTip(openButton, "Open file");

            saveButton = new Button
            {
                Text = "💾+",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(130, 180, 255) : Color.RoyalBlue
            };
            saveButton.FlatAppearance.BorderSize = 0;
            buttonToolTip.SetToolTip(saveButton, "Save as...");

            quickSaveButton = new Button
            {
                Text = "💾",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(130, 255, 130) : Color.Green
            };
            quickSaveButton.FlatAppearance.BorderSize = 0;
            buttonToolTip.SetToolTip(quickSaveButton, "Quick save");

            fontButton = new Button
            {
                Text = "A",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(180, 180, 255) : Color.RoyalBlue
            };

            fontButton.FlatAppearance.BorderSize = 0;
            fontButton.Click += FontButton_Click;
            buttonToolTip.SetToolTip(fontButton, "Change font");
            
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
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(100, 200, 255) : Color.Blue
            };

            hyperlinkButton.FlatAppearance.BorderSize = 0;
            hyperlinkButton.Click += HyperlinkButton_Click;
            buttonToolTip.SetToolTip(hyperlinkButton, "Add hyperlink");
            
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
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
                ForeColor = isDarkMode ? Color.FromArgb(255, 223, 0) : Color.FromArgb(100, 100, 200)
            };

            themeToggleButton.FlatAppearance.BorderSize = 0;
            themeToggleButton.Click += ThemeToggleButton_Click;
            buttonToolTip.SetToolTip(themeToggleButton, "Toggle dark/light mode");
            
            // Add hover effects
            themeToggleButton.MouseEnter += ThemeToggleButton_MouseEnter;
            themeToggleButton.MouseLeave += ThemeToggleButton_MouseLeave;

            wordCountLabel = new Label
            {
                Text = "Words: 0",
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(5, 5, 10, 5), // Extra right padding for spacing from autoSaveLabel
                ForeColor = isDarkMode ? darkForeColor : Color.Gray
            };

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
            
            bottomToolbar.Controls.Add(openButton);
            bottomToolbar.Controls.Add(saveButton);
            bottomToolbar.Controls.Add(quickSaveButton);
            bottomToolbar.Controls.Add(fontButton);
            bottomToolbar.Controls.Add(hyperlinkButton);
            bottomToolbar.Controls.Add(themeToggleButton);
            bottomToolbar.Controls.Add(wordCountLabel);
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
            base.OnPaint(e);
            using (var pen = new Pen(isDarkMode ? darkToolbarColor : Color.LightGray, 1))
            {
                var rect = this.ClientRectangle;
                e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }
        }

        private async void OpenButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog()
                {
                    Filter = "Text files (*.txt)|*.txt|Markdown files (*.md)|*.md|Subtitle files (*.srt)|*.srt|All files (*.*)|*.*",
                    FilterIndex = 1,
                    Title = "Open File"
                };
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    var (content, hyperlinks) = await FileService.LoadFileAsync(openDialog.FileName);
                    textBox.Text = content;
                    document.FilePath = openDialog.FileName;
                    document.Hyperlinks = hyperlinks;
                    document.ResetDirty();
                    UpdateHyperlinkRendering();
                    UpdateWordCount();
                    
                    // Start autosave immediately for existing files
                    autoSaveTimer.Stop();
                    autoSaveTimer.Start();
                    autoSaveLabel.Text = $"Opened: {Path.GetFileName(openDialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                    await FileService.SaveFileAsync(saveDialog.FileName, textBox.Text, document.Hyperlinks);
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
                await FileService.SaveFileAsync(document.FilePath, textBox.Text, document.Hyperlinks);
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
            
            // Create and apply the new font with proper bold weight
            if (style == FontStyle.Bold)
            {
                // For bold, create a new font with the Bold style
                using var newFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
                textBox.SelectionFont = newFont;
            }
            else
            {
                // For other styles, use the standard approach
                using var newFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
                textBox.SelectionFont = newFont;
            }
            
            // Maintain the selection
            textBox.Select(selectionStart, selectionLength);
            textBox.Focus();
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
                    var (content, hyperlinks) = await FileService.LoadFileAsync(files[0]);
                    textBox.Text = content;
                    document.FilePath = files[0];
                    document.Hyperlinks = hyperlinks;
                    document.ResetDirty();
                    UpdateHyperlinkRendering();
                    UpdateWordCount();
                    
                    // Start autosave immediately for existing files
                    autoSaveTimer.Stop(); // Reset the timer
                    autoSaveTimer.Start(); // Start fresh countdown
                    autoSaveLabel.Text = $"Autosave ready for: {Path.GetFileName(files[0])}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(document.FilePath) && document.IsDirty)
            {
                try
                {
                    await FileService.SaveFileAsync(document.FilePath, textBox.Text, document.Hyperlinks);
                    document.ResetDirty();
                    autoSaveLabel.Text = $"Last autosave: {DateTime.Now.ToString("HH:mm:ss")}";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Autosave failed: {ex.Message}");
                    autoSaveLabel.Text = $"Autosave failed: {DateTime.Now.ToString("HH:mm:ss")}";
                }
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
                findReplaceDialog = new FindReplaceDialog(textBox, isDarkMode);
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
            openButton?.Dispose();
            saveButton?.Dispose();
            quickSaveButton?.Dispose();
            fontButton?.Dispose();
            hyperlinkButton?.Dispose();
            themeToggleButton?.Dispose();
            wordCountLabel?.Dispose();
            autoSaveLabel?.Dispose();
            buttonToolTip?.Dispose();
        }

        partial void OnDisposing()
        {
            CleanupResources();
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
                    var updatedFont = new Font(newFont.FontFamily, newFont.Size, combinedStyle);
                    textBox.SelectionFont = updatedFont;
                }
            }
            else
            {
                // Change the entire textbox font - create a new Font object to avoid reference issues
                var appliedFont = new Font(newFont.FontFamily, newFont.Size, newFont.Style);
                textBox.Font = appliedFont;
                currentFontSize = newFont.Size;
                
                // Force refresh to ensure the change takes effect
                textBox.Invalidate();
                textBox.Update();
            }

            // Restore the selection
            textBox.Select(selectionStart, selectionLength);
            textBox.Focus();
        }

        private void ThemeToggleButton_Click(object? sender, EventArgs e)
        {
            isDarkMode = !isDarkMode;
            RefreshTheme();
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

        private void RefreshTheme()
        {
            // Update form colors
            this.BackColor = isDarkMode ? darkBackColor : Color.White;
            
            // Update all panels to prevent dark edges
            UpdatePanelColors(this);
            
            // Update text box colors
            textBox.BackColor = isDarkMode ? darkBackColor : Color.White;
            textBox.ForeColor = isDarkMode ? darkForeColor : Color.Black;
            
            // Update all text to match theme (preserve hyperlink colors)
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;
            
            // First, update hyperlink colors properly
            foreach (var hyperlink in document.Hyperlinks)
            {
                textBox.Select(hyperlink.StartIndex, hyperlink.Length);
                textBox.SelectionColor = isDarkMode ? Color.FromArgb(77, 166, 255) : Color.Blue;
            }
            
            // Then update non-hyperlink text
            for (int i = 0; i < textBox.Text.Length; i++)
            {
                textBox.Select(i, 1);
                var currentHyperlink = document.GetHyperlinkAtPosition(i);
                
                // Only update if it's not part of a hyperlink
                if (currentHyperlink == null)
                {
                    textBox.SelectionColor = isDarkMode ? darkForeColor : Color.Black;
                }
            }
            
            // Restore selection
            textBox.Select(selectionStart, selectionLength);
            
            // Update bottom toolbar
            bottomToolbar.BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke;
            
            // Update buttons
            openButton.ForeColor = isDarkMode ? Color.FromArgb(255, 200, 130) : Color.DarkOrange;
            saveButton.ForeColor = isDarkMode ? Color.FromArgb(130, 180, 255) : Color.RoyalBlue;
            quickSaveButton.ForeColor = isDarkMode ? Color.FromArgb(130, 255, 130) : Color.Green;
            fontButton.ForeColor = isDarkMode ? Color.FromArgb(180, 180, 255) : Color.RoyalBlue;
            hyperlinkButton.ForeColor = isDarkMode ? Color.FromArgb(100, 200, 255) : Color.Blue;
            
            // Update theme toggle button
            themeToggleButton.Text = isDarkMode ? "☀️" : "🌙";
            themeToggleButton.ForeColor = isDarkMode ? Color.FromArgb(255, 223, 0) : Color.FromArgb(100, 100, 200);
            
            // Update auto save label
            autoSaveLabel.ForeColor = isDarkMode ? darkForeColor : Color.Gray;
            
            // Update word count label
            wordCountLabel.ForeColor = isDarkMode ? darkForeColor : Color.Gray;
            
            // Update title bar
            titleBar.BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke;
            
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
            
            // Refresh the form
            this.Refresh();
        }

        private void HyperlinkUpdateTimer_Tick(object? sender, EventArgs e)
        {
            hyperlinkUpdateTimer.Stop();
            UpdateHyperlinkRendering();
        }

        private void SaveUndoState()
        {
            if (isUndoRedoOperation) return;
            
            var state = new UndoState
            {
                Text = textBox.Text,
                Hyperlinks = document.Hyperlinks.Select(h => h.Clone()).ToList(),
                SelectionStart = textBox.SelectionStart,
                SelectionLength = textBox.SelectionLength
            };
            
            undoStack.Push(state);
            redoStack.Clear(); // Clear redo stack when new action is performed
            
            // Limit undo stack size
            while (undoStack.Count > 100)
            {
                var items = undoStack.ToArray();
                undoStack.Clear();
                for (int i = 0; i < items.Length - 1; i++)
                {
                    undoStack.Push(items[i]);
                }
            }
        }

        private void RestoreState(UndoState state)
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
            if (undoStack.Count > 1) // Keep at least one state
            {
                // Save current state to redo stack
                var currentState = new UndoState
                {
                    Text = textBox.Text,
                    Hyperlinks = document.Hyperlinks.Select(h => h.Clone()).ToList(),
                    SelectionStart = textBox.SelectionStart,
                    SelectionLength = textBox.SelectionLength
                };
                redoStack.Push(currentState);
                
                // Pop current state and restore previous
                undoStack.Pop();
                if (undoStack.Count > 0)
                {
                    RestoreState(undoStack.Peek());
                }
            }
        }

        private void PerformRedo()
        {
            if (redoStack.Count > 0)
            {
                // Save current state to undo stack
                SaveUndoState();
                
                // Restore state from redo stack
                var redoState = redoStack.Pop();
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

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private void UpdateWordCount()
        {
            if (wordCountLabel != null)
            {
                int wordCount = CountWords(textBox.Text);
                wordCountLabel.Text = $"Words: {wordCount}";
            }
        }

        private void TextBox_TextChanged(object? sender, EventArgs e)
        {
            if (isUndoRedoOperation) return;
            
            document.IsDirty = true;
            
            // Update word count in real-time
            UpdateWordCount();
            
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
                    hyperlinkUpdateTimer.Start();
                }
            }
            
            lastTextContent = currentText;
            lastSelectionStart = currentSelectionStart;
        }

        private void UpdateHyperlinkRendering()
        {
            int savedSelectionStart = textBox.SelectionStart;
            int savedSelectionLength = textBox.SelectionLength;
            
            // Use BeginUpdate/EndUpdate to prevent flickering
            SendMessage(textBox.Handle, WM_SETREDRAW, 0, 0);
            
            try
            {
                // First, reset ALL text formatting
                textBox.SelectAll();
                textBox.SelectionColor = isDarkMode ? darkForeColor : Color.Black;
                
                // Remove all underlines character by character
                for (int i = 0; i < textBox.Text.Length; i++)
                {
                    textBox.Select(i, 1);
                    Font charFont = textBox.SelectionFont ?? textBox.Font;
                    if ((charFont.Style & FontStyle.Underline) != 0)
                    {
                        using var nonUnderlinedFont = new Font(charFont, charFont.Style & ~FontStyle.Underline);
                        textBox.SelectionFont = nonUnderlinedFont;
                    }
                    textBox.SelectionColor = isDarkMode ? darkForeColor : Color.Black;
                }
                
                // Apply hyperlink formatting only to valid hyperlinks
                foreach (var hyperlink in document.Hyperlinks.ToList())
                {
                    if (hyperlink.StartIndex >= 0 && hyperlink.EndIndex <= textBox.TextLength)
                    {
                        textBox.Select(hyperlink.StartIndex, hyperlink.Length);
                        textBox.SelectionColor = isDarkMode ? Color.FromArgb(77, 166, 255) : Color.Blue;
                        
                        // Apply underline
                        Font currentFont = textBox.SelectionFont ?? textBox.Font;
                        using var underlinedFont = new Font(currentFont, currentFont.Style | FontStyle.Underline);
                        textBox.SelectionFont = underlinedFont;
                    }
                }
            }
            finally
            {
                // Restore original selection
                textBox.Select(savedSelectionStart, savedSelectionLength);
                
                // Re-enable drawing
                SendMessage(textBox.Handle, WM_SETREDRAW, 1, 0);
                textBox.Invalidate();
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