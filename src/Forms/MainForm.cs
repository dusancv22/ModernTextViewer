using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ModernTextViewer.src.Services;
using ModernTextViewer.src.Models;
using System.Runtime.InteropServices.Marshalling;
using System.Diagnostics;
using System.IO;

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
        private Button saveButton = null!;
        private const int RESIZE_BORDER = 8;
        private const int TITLE_BAR_WIDTH = 32;
        private const float MIN_FONT_SIZE = 6f;
        private const float MAX_FONT_SIZE = 72f;
        private float currentFontSize = 10f;
        private readonly DocumentModel document = new DocumentModel();
        private bool isDarkMode = true;  // Default to dark mode
        private System.Windows.Forms.Timer autoSaveTimer;
        private readonly Color darkBackColor = Color.FromArgb(30, 30, 30);
        private readonly Color darkForeColor = Color.FromArgb(220, 220, 220);
        private readonly Color darkToolbarColor = Color.FromArgb(45, 45, 45);
        private Label autoSaveLabel = null!;

        public MainForm()
        {
            try
            {
                InitializeComponent();
                
                autoSaveTimer = new System.Windows.Forms.Timer
                {
                    Interval = 5 * 60 * 1000
                };
                autoSaveTimer.Tick += AutoSaveTimer_Tick;
                
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
                BackColor = isDarkMode ? darkToolbarColor : Color.LightGray
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
                DetectUrls = false,
                EnableAutoDragDrop = false,
            };

            // Add event handlers
            textBox.MouseWheel += TextBox_MouseWheel;
            textBox.DragEnter += TextBox_DragEnter;
            textBox.DragDrop += TextBox_DragDrop;
            textBox.TextChanged += (s, e) => document.IsDirty = true;
            textBox.GotFocus += (s, e) => textBox.Refresh();
            textBox.KeyDown += (s, e) => 
            {
                if (e.KeyCode == Keys.Enter)
                {
                    textBox.Refresh();
                }
            };

            // Set up control hierarchy
            paddingPanel.Controls.Add(textBox);
            textBoxContainer.Controls.Add(paddingPanel);
            this.Controls.Add(textBoxContainer);

            // Ensure the textbox can receive focus
            textBox.Select();
            textBox.Focus();

            textBoxContainer.SendToBack();
        }

        private void TextBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                float newSize = currentFontSize + (e.Delta > 0 ? 1f : -1f);

                if (newSize >= MIN_FONT_SIZE && newSize <= MAX_FONT_SIZE)
                {
                    currentFontSize = newSize;
                    textBox.Font = new Font(textBox.Font.FontFamily, currentFontSize);
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

            saveButton = new Button
            {
                Text = "💾",
                Width = 20,
                Height = 20,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Symbol", 12),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0),
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
            saveButton.Click += SaveButton_Click;
            
            bottomToolbar.Controls.Add(saveButton);
            bottomToolbar.Controls.Add(autoSaveLabel);
            this.Controls.Add(bottomToolbar);
            
            bottomToolbar.BringToFront();
        }

        // Constants for window messages
        private const int WM_MOVING = 0x0216;
        private const int WM_SIZING = 0x0214;
        private const int WM_NCHITTEST = 0x84;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

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

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog()
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 1
                };
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    await FileService.SaveFileAsync(saveDialog.FileName, textBox.Text);
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

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveButton_Click(this, EventArgs.Empty);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TextBox_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files?.Length == 1 && Path.GetExtension(files[0]).ToLower() == ".txt")
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
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
                    string content = await FileService.LoadFileAsync(files[0]);
                    textBox.Text = content;
                    document.FilePath = files[0];
                    document.ResetDirty();
                    
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
                    await FileService.SaveFileAsync(document.FilePath, textBox.Text);
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

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            document.IsDirty = true;
        }

        private void CleanupResources()
        {
            autoSaveTimer?.Dispose();
            textBox?.Dispose();
            titleBar?.Dispose();
            closeButton?.Dispose();
            maximizeButton?.Dispose();
            minimizeButton?.Dispose();
            bottomToolbar?.Dispose();
            saveButton?.Dispose();
            autoSaveLabel?.Dispose();
        }

        partial void OnDisposing()
        {
            CleanupResources();
        }
    }
}