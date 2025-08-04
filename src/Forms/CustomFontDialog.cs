using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace ModernTextViewer.src.Forms
{
    public partial class CustomFontDialog : Form
    {
        private ComboBox fontFamilyCombo = null!;
        private ComboBox fontSizeCombo = null!;
        private CheckBox boldCheckBox = null!;
        private CheckBox italicCheckBox = null!;
        private CheckBox underlineCheckBox = null!;
        private Label previewLabel = null!;
        private Button okButton = null!;
        private Button cancelButton = null!;
        private Panel titleBar = null!;
        private Button closeButton = null!;
        private Label titleLabel = null!;
        private GroupBox previewGroupBox = null!;
        
        private readonly bool isDarkMode;
        private readonly Color darkBackColor = Color.FromArgb(30, 30, 30);
        private readonly Color darkForeColor = Color.FromArgb(220, 220, 220);
        private readonly Color darkToolbarColor = Color.FromArgb(45, 45, 45);
        
        public Font SelectedFont { get; private set; }
        
        private const int TITLE_BAR_HEIGHT = 32;
        
        public CustomFontDialog(bool isDarkMode, Font currentFont)
        {
            this.isDarkMode = isDarkMode;
            this.SelectedFont = currentFont;
            InitializeComponent();
            ApplyTheme();
            LoadFontSettings(currentFont);
        }
        
        private void InitializeComponent()
        {
            this.Text = "Font Selection";
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(400, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            
            // Title bar - match HyperlinkDialog structure exactly
            titleBar = new Panel
            {
                Height = TITLE_BAR_HEIGHT,
                Dock = DockStyle.Top,
                BackColor = isDarkMode ? darkToolbarColor : Color.WhiteSmoke
            };
            
            titleLabel = new Label
            {
                Text = "Font Selection",
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
            closeButton.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            
            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(closeButton);
            
            // Main content panel
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            
            // Font Family
            var fontFamilyLabel = new Label
            {
                Text = "Font Family:",
                Location = new Point(20, 50),
                AutoSize = true
            };
            
            fontFamilyCombo = new ComboBox
            {
                Location = new Point(20, 75),
                Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            
            // Load system fonts
            foreach (FontFamily family in FontFamily.Families)
            {
                fontFamilyCombo.Items.Add(family.Name);
            }
            
            // Font Size
            var fontSizeLabel = new Label
            {
                Text = "Font Size:",
                Location = new Point(20, 110),
                AutoSize = true
            };
            
            fontSizeCombo = new ComboBox
            {
                Location = new Point(20, 135),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            
            // Add common font sizes
            int[] sizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
            foreach (int size in sizes)
            {
                fontSizeCombo.Items.Add(size.ToString());
            }
            
            // Style checkboxes
            boldCheckBox = new CheckBox
            {
                Text = "Bold",
                Location = new Point(140, 135),
                AutoSize = true
            };
            
            italicCheckBox = new CheckBox
            {
                Text = "Italic",
                Location = new Point(200, 135),
                AutoSize = true
            };
            
            underlineCheckBox = new CheckBox
            {
                Text = "Underline",
                Location = new Point(260, 135),
                AutoSize = true
            };
            
            // Preview
            previewGroupBox = new GroupBox
            {
                Text = "Preview",
                Location = new Point(20, 170),
                Size = new Size(340, 80),
                ForeColor = isDarkMode ? darkForeColor : Color.Black
            };
            
            previewLabel = new Label
            {
                Text = "AaBbCc 123",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = isDarkMode ? darkBackColor : Color.White,
                ForeColor = isDarkMode ? darkForeColor : Color.Black
            };
            
            previewGroupBox.Controls.Add(previewLabel);
            
            // Buttons
            okButton = new Button
            {
                Text = "OK",
                Location = new Point(210, 270),
                Size = new Size(75, 30),
                FlatStyle = FlatStyle.Flat
            };
            okButton.Click += OkButton_Click;
            
            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(290, 270),
                Size = new Size(75, 30),
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            
            // Add controls
            contentPanel.Controls.AddRange(new Control[] {
                fontFamilyLabel, fontFamilyCombo,
                fontSizeLabel, fontSizeCombo,
                boldCheckBox, italicCheckBox, underlineCheckBox,
                previewGroupBox,
                okButton, cancelButton
            });
            
            this.Controls.Add(contentPanel);
            this.Controls.Add(titleBar);
            
            // Wire up events
            fontFamilyCombo.SelectedIndexChanged += UpdatePreview;
            fontSizeCombo.SelectedIndexChanged += UpdatePreview;
            boldCheckBox.CheckedChanged += UpdatePreview;
            italicCheckBox.CheckedChanged += UpdatePreview;
            underlineCheckBox.CheckedChanged += UpdatePreview;
            
            // Enable dragging
            titleBar.MouseDown += TitleBar_MouseDown;
        }
        
        private void LoadFontSettings(Font font)
        {
            fontFamilyCombo.SelectedItem = font.FontFamily.Name;
            fontSizeCombo.SelectedItem = ((int)font.Size).ToString();
            boldCheckBox.Checked = font.Bold;
            italicCheckBox.Checked = font.Italic;
            underlineCheckBox.Checked = font.Underline;
            UpdatePreview(null, null);
        }
        
        private void ApplyTheme()
        {
            if (isDarkMode)
            {
                this.BackColor = darkToolbarColor;
                this.ForeColor = darkForeColor;
                
                titleLabel.ForeColor = darkForeColor;
                closeButton.ForeColor = darkForeColor;
                closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
                
                fontFamilyCombo.BackColor = darkToolbarColor;
                fontFamilyCombo.ForeColor = darkForeColor;
                fontSizeCombo.BackColor = darkToolbarColor;
                fontSizeCombo.ForeColor = darkForeColor;
                
                boldCheckBox.ForeColor = darkForeColor;
                italicCheckBox.ForeColor = darkForeColor;
                underlineCheckBox.ForeColor = darkForeColor;
                
                previewGroupBox.ForeColor = darkForeColor;
                
                okButton.BackColor = darkToolbarColor;
                okButton.ForeColor = darkForeColor;
                okButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                
                cancelButton.BackColor = darkToolbarColor;
                cancelButton.ForeColor = darkForeColor;
                cancelButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                
                previewLabel.BackColor = darkBackColor;
                previewLabel.ForeColor = darkForeColor;
            }
            else
            {
                this.BackColor = Color.White;
                this.ForeColor = Color.Black;
                
                titleLabel.ForeColor = Color.Black;
                closeButton.ForeColor = Color.Gray;
                closeButton.FlatAppearance.MouseOverBackColor = Color.LightGray;
                
                fontFamilyCombo.BackColor = Color.White;
                fontFamilyCombo.ForeColor = Color.Black;
                fontSizeCombo.BackColor = Color.White;
                fontSizeCombo.ForeColor = Color.Black;
                
                boldCheckBox.ForeColor = Color.Black;
                italicCheckBox.ForeColor = Color.Black;
                underlineCheckBox.ForeColor = Color.Black;
                
                previewGroupBox.ForeColor = Color.Black;
                
                okButton.BackColor = Color.WhiteSmoke;
                okButton.ForeColor = Color.Black;
                okButton.FlatAppearance.BorderColor = Color.Gray;
                
                cancelButton.BackColor = Color.WhiteSmoke;
                cancelButton.ForeColor = Color.Black;
                cancelButton.FlatAppearance.BorderColor = Color.Gray;
                
                previewLabel.BackColor = Color.White;
                previewLabel.ForeColor = Color.Black;
            }
        }
        
        private void UpdatePreview(object? sender, EventArgs? e)
        {
            if (fontFamilyCombo.SelectedItem == null || fontSizeCombo.SelectedItem == null)
                return;
                
            FontStyle style = FontStyle.Regular;
            if (boldCheckBox.Checked) style |= FontStyle.Bold;
            if (italicCheckBox.Checked) style |= FontStyle.Italic;
            if (underlineCheckBox.Checked) style |= FontStyle.Underline;
            
            try
            {
                var newFont = new Font(
                    fontFamilyCombo.SelectedItem.ToString()!,
                    float.Parse(fontSizeCombo.SelectedItem.ToString()!),
                    style
                );
                previewLabel.Font = newFont;
                
                // Force correct text color based on theme - do this after setting font
                previewLabel.ForeColor = isDarkMode ? darkForeColor : Color.Black;
                previewLabel.BackColor = isDarkMode ? darkBackColor : Color.White;
            }
            catch
            {
                // If font creation fails, keep the current font and ensure correct colors
                previewLabel.ForeColor = isDarkMode ? darkForeColor : Color.Black;
                previewLabel.BackColor = isDarkMode ? darkBackColor : Color.White;
            }
        }
        
        private void OkButton_Click(object? sender, EventArgs e)
        {
            if (fontFamilyCombo.SelectedItem == null || fontSizeCombo.SelectedItem == null)
            {
                MessageBox.Show("Please select a font family and size.", "Invalid Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            FontStyle style = FontStyle.Regular;
            if (boldCheckBox.Checked) style |= FontStyle.Bold;
            if (italicCheckBox.Checked) style |= FontStyle.Italic;
            if (underlineCheckBox.Checked) style |= FontStyle.Underline;
            
            try
            {
                SelectedFont = new Font(
                    fontFamilyCombo.SelectedItem.ToString()!,
                    float.Parse(fontSizeCombo.SelectedItem.ToString()!),
                    style
                );
                this.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating font: {ex.Message}", "Font Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
    }
}