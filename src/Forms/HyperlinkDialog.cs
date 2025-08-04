using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ModernTextViewer.src.Services;

namespace ModernTextViewer.src.Forms
{
    public partial class HyperlinkDialog : Form
    {
        private TextBox urlTextBox = null!;
        private Button okButton = null!;
        private Button cancelButton = null!;
        private Button removeButton = null!;
        private Label urlLabel = null!;
        private Panel titleBar = null!;
        private Button closeButton = null!;
        private Label titleLabel = null!;
        private bool isDarkMode;
        private const int TITLE_BAR_HEIGHT = 32;

        // Colors
        private readonly Color darkBackColor = Color.FromArgb(30, 30, 30);
        private readonly Color darkForeColor = Color.FromArgb(220, 220, 220);
        private readonly Color darkToolbarColor = Color.FromArgb(45, 45, 45);

        public string Url { get; private set; } = string.Empty;
        public string DisplayText { get; private set; } = string.Empty;
        public bool RemoveHyperlink { get; private set; } = false;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        public HyperlinkDialog(bool isDarkMode, string? existingUrl = null, string? existingDisplayText = null)
        {
            this.isDarkMode = isDarkMode;
            DisplayText = existingDisplayText ?? string.Empty;
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(existingUrl))
            {
                urlTextBox.Text = existingUrl;
                removeButton.Visible = true;
            }
            else
            {
                removeButton.Visible = false;
            }

            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(500, 180);

            // Initialize title bar
            titleBar = new Panel
            {
                Height = TITLE_BAR_HEIGHT,
                Dock = DockStyle.Top,
                BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke
            };

            titleLabel = new Label
            {
                Text = "Insert Hyperlink",
                AutoSize = true,
                Location = new Point(10, 8),
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };

            closeButton = new Button
            {
                Text = "✕",
                Size = new Size(TITLE_BAR_HEIGHT, TITLE_BAR_HEIGHT),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(closeButton);

            // Allow dragging
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            // Content panel
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 20, 20, 20)
            };

            urlLabel = new Label
            {
                Text = "URL:",
                Location = new Point(20, 60),
                Size = new Size(40, 25),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10)
            };

            urlTextBox = new TextBox
            {
                Location = new Point(70, 60),
                Size = new Size(390, 30),
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Style the placeholder
            SetPlaceholder();
            urlTextBox.Enter += (s, e) => RemovePlaceholder();
            urlTextBox.Leave += (s, e) => SetPlaceholder();

            okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(285, 110),
                Size = new Size(85, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(375, 110),
                Size = new Size(85, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };

            removeButton = new Button
            {
                Text = "Remove",
                Location = new Point(20, 110),
                Size = new Size(85, 30),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Visible = false
            };

            removeButton.Click += (s, e) =>
            {
                RemoveHyperlink = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(urlTextBox.Text) || urlTextBox.Text == "https://example.com")
                {
                    MessageBox.Show("Please enter a URL.", "Validation Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Url = urlTextBox.Text.Trim();
            };

            this.Controls.Add(contentPanel);
            this.Controls.Add(titleBar);
            
            contentPanel.Controls.AddRange(new Control[] {
                urlLabel, urlTextBox,
                okButton, cancelButton, removeButton
            });

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        private void SetPlaceholder()
        {
            if (string.IsNullOrWhiteSpace(urlTextBox.Text))
            {
                urlTextBox.Text = "https://example.com";
                urlTextBox.ForeColor = Color.Gray;
                urlTextBox.Font = new Font(urlTextBox.Font, FontStyle.Italic);
                
                // Add padding
                urlTextBox.SelectionStart = 0;
                urlTextBox.SelectionLength = 0;
            }
        }

        private void RemovePlaceholder()
        {
            if (urlTextBox.Text == "https://example.com")
            {
                urlTextBox.Text = "";
                urlTextBox.ForeColor = isDarkMode ? darkForeColor : Color.Black;
                urlTextBox.Font = new Font("Segoe UI", 11, FontStyle.Regular);
            }
        }

        private void ApplyTheme()
        {
            if (isDarkMode)
            {
                this.BackColor = darkToolbarColor;
                this.ForeColor = darkForeColor;

                titleBar.BackColor = darkToolbarColor;
                titleLabel.ForeColor = darkForeColor;
                
                closeButton.BackColor = darkToolbarColor;
                closeButton.ForeColor = darkForeColor;
                closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);

                urlLabel.ForeColor = darkForeColor;
                
                urlTextBox.BackColor = Color.FromArgb(20, 20, 20);
                urlTextBox.ForeColor = darkForeColor;
                urlTextBox.BorderStyle = BorderStyle.FixedSingle;

                foreach (var button in new[] { okButton, cancelButton, removeButton })
                {
                    button.BackColor = Color.FromArgb(60, 60, 60);
                    button.ForeColor = darkForeColor;
                    button.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80);
                }
            }
            else
            {
                this.BackColor = Color.White;
                this.ForeColor = Color.Black;

                titleBar.BackColor = Color.WhiteSmoke;
                titleLabel.ForeColor = Color.Black;
                
                closeButton.BackColor = Color.WhiteSmoke;
                closeButton.ForeColor = Color.Black;
                closeButton.FlatAppearance.MouseOverBackColor = Color.LightGray;

                urlLabel.ForeColor = Color.Black;
                
                urlTextBox.BackColor = Color.White;
                urlTextBox.ForeColor = Color.Black;

                foreach (var button in new[] { okButton, cancelButton, removeButton })
                {
                    button.BackColor = Color.WhiteSmoke;
                    button.ForeColor = Color.Black;
                    button.FlatAppearance.BorderColor = Color.Gray;
                    button.FlatAppearance.MouseOverBackColor = Color.LightGray;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw border with stronger contrast
            using (var pen = new Pen(isDarkMode ? Color.FromArgb(100, 100, 100) : Color.Gray, 2))
            {
                e.Graphics.DrawRectangle(pen, new Rectangle(1, 1, Width - 2, Height - 2));
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }
    }
}