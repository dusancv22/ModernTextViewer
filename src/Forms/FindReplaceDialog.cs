using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ModernTextViewer.src.Forms
{
    public partial class FindReplaceDialog : Form
    {
        private TextBox _findTextBox = null!;
        private TextBox _replaceTextBox = null!;
        private Button _findNextButton = null!;
        private Button _replaceButton = null!;
        private Button _replaceAllButton = null!;
        private Button _closeButton = null!;
        private CheckBox _caseSensitiveCheckBox = null!;
        private CheckBox _wholeWordCheckBox = null!;
        private Label _findLabel = null!;
        private Label _replaceLabel = null!;
        private Label _statusLabel = null!;

        private RichTextBox _targetTextBox;
        private int _currentSearchIndex = 0;
        private string _lastSearchText = "";
        private bool _isDarkMode = true;

        private readonly Color _darkBackground = Color.FromArgb(45, 45, 45);
        private readonly Color _darkForeground = Color.FromArgb(220, 220, 220);
        private readonly Color _darkInputBackground = Color.FromArgb(30, 30, 30);
        private readonly Color _darkButtonBackground = Color.FromArgb(60, 60, 60);
        private readonly Color _darkButtonHover = Color.FromArgb(75, 75, 75);
        private readonly Color _lightBackground = Color.FromArgb(240, 240, 240);
        private readonly Color _lightForeground = Color.Black;
        private readonly Color _lightInputBackground = Color.White;
        private readonly Color _lightButtonBackground = Color.FromArgb(225, 225, 225);
        private readonly Color _lightButtonHover = Color.FromArgb(210, 210, 210);

        public FindReplaceDialog(RichTextBox targetTextBox, bool isDarkMode)
        {
            _targetTextBox = targetTextBox;
            _isDarkMode = isDarkMode;
            InitializeComponent();
            ApplyTheme();
        }

        private void InitializeComponent()
        {
            this.Text = "Find and Replace";
            this.Size = new Size(500, 280);
            this.MinimumSize = new Size(450, 250);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.KeyPreview = true;
            this.KeyDown += OnDialogKeyDown;
            this.Paint += OnFormPaint;

            // Create custom title bar
            var titleBar = new Panel();
            titleBar.Height = 30;
            titleBar.Dock = DockStyle.Top;
            titleBar.BackColor = _isDarkMode ? _darkBackground : _lightBackground;
            titleBar.MouseDown += TitleBar_MouseDown;
            
            var titleLabel = new Label();
            titleLabel.Text = "Find and Replace";
            titleLabel.Location = new Point(10, 5);
            titleLabel.Size = new Size(200, 20);
            titleLabel.ForeColor = _isDarkMode ? _darkForeground : _lightForeground;
            titleBar.Controls.Add(titleLabel);
            
            var closeTitleButton = new Button();
            closeTitleButton.Text = "×";
            closeTitleButton.Size = new Size(30, 30);
            closeTitleButton.Location = new Point(this.Width - 35, 0);
            closeTitleButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeTitleButton.FlatStyle = FlatStyle.Flat;
            closeTitleButton.FlatAppearance.BorderSize = 0;
            closeTitleButton.ForeColor = _isDarkMode ? _darkForeground : _lightForeground;
            closeTitleButton.BackColor = _isDarkMode ? _darkBackground : _lightBackground;
            closeTitleButton.Font = new Font("Arial", 12);
            closeTitleButton.Cursor = Cursors.Hand;
            closeTitleButton.Click += (s, e) => this.Close();
            closeTitleButton.MouseEnter += (s, e) => closeTitleButton.BackColor = _isDarkMode ? _darkButtonHover : _lightButtonHover;
            closeTitleButton.MouseLeave += (s, e) => closeTitleButton.BackColor = _isDarkMode ? _darkBackground : _lightBackground;
            titleBar.Controls.Add(closeTitleButton);
            
            this.Controls.Add(titleBar);

            int leftMargin = 20;
            int topMargin = 50; // Adjusted for title bar
            int controlHeight = 25;
            int labelWidth = 80;
            int textBoxWidth = 250;
            int buttonWidth = 90;
            int verticalSpacing = 35;

            _findLabel = new Label();
            _findLabel.Text = "Find:";
            _findLabel.Location = new Point(leftMargin, topMargin);
            _findLabel.Size = new Size(labelWidth, controlHeight);
            _findLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.Controls.Add(_findLabel);

            _findTextBox = new TextBox();
            _findTextBox.Location = new Point(leftMargin + labelWidth, topMargin);
            _findTextBox.Size = new Size(textBoxWidth, controlHeight);
            _findTextBox.TextChanged += OnFindTextChanged;
            this.Controls.Add(_findTextBox);

            _findNextButton = CreateButton("Find Next", new Point(leftMargin + labelWidth + textBoxWidth + 10, topMargin), new Size(buttonWidth, controlHeight));
            _findNextButton.Click += OnFindNextClick;
            this.Controls.Add(_findNextButton);

            _replaceLabel = new Label();
            _replaceLabel.Text = "Replace with:";
            _replaceLabel.Location = new Point(leftMargin, topMargin + verticalSpacing);
            _replaceLabel.Size = new Size(labelWidth, controlHeight);
            _replaceLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.Controls.Add(_replaceLabel);

            _replaceTextBox = new TextBox();
            _replaceTextBox.Location = new Point(leftMargin + labelWidth, topMargin + verticalSpacing);
            _replaceTextBox.Size = new Size(textBoxWidth, controlHeight);
            this.Controls.Add(_replaceTextBox);

            _replaceButton = CreateButton("Replace", new Point(leftMargin + labelWidth + textBoxWidth + 10, topMargin + verticalSpacing), new Size(buttonWidth, controlHeight));
            _replaceButton.Click += OnReplaceClick;
            _replaceButton.Enabled = false;
            this.Controls.Add(_replaceButton);

            _caseSensitiveCheckBox = new CheckBox();
            _caseSensitiveCheckBox.Text = "Case sensitive";
            _caseSensitiveCheckBox.Location = new Point(leftMargin + labelWidth, topMargin + verticalSpacing * 2);
            _caseSensitiveCheckBox.Size = new Size(120, controlHeight);
            _caseSensitiveCheckBox.CheckedChanged += OnSearchOptionChanged;
            this.Controls.Add(_caseSensitiveCheckBox);

            _wholeWordCheckBox = new CheckBox();
            _wholeWordCheckBox.Text = "Whole word";
            _wholeWordCheckBox.Location = new Point(leftMargin + labelWidth + 130, topMargin + verticalSpacing * 2);
            _wholeWordCheckBox.Size = new Size(120, controlHeight);
            _wholeWordCheckBox.CheckedChanged += OnSearchOptionChanged;
            this.Controls.Add(_wholeWordCheckBox);

            _replaceAllButton = CreateButton("Replace All", new Point(leftMargin + labelWidth, topMargin + verticalSpacing * 3), new Size(buttonWidth, controlHeight));
            _replaceAllButton.Click += OnReplaceAllClick;
            _replaceAllButton.Enabled = false;
            this.Controls.Add(_replaceAllButton);

            _closeButton = CreateButton("Close", new Point(leftMargin + labelWidth + buttonWidth + 10, topMargin + verticalSpacing * 3), new Size(buttonWidth, controlHeight));
            _closeButton.Click += (s, e) => this.Close();
            this.Controls.Add(_closeButton);

            _statusLabel = new Label();
            _statusLabel.Location = new Point(leftMargin, topMargin + verticalSpacing * 4);
            _statusLabel.Size = new Size(this.Width - leftMargin * 2, controlHeight);
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            this.Controls.Add(_statusLabel);

            _findTextBox.Focus();
        }

        private Button CreateButton(string text, Point location, Size size)
        {
            var button = new Button();
            button.Text = text;
            button.Location = location;
            button.Size = size;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Cursor = Cursors.Hand;
            button.MouseEnter += OnButtonMouseEnter;
            button.MouseLeave += OnButtonMouseLeave;
            return button;
        }

        private void OnButtonMouseEnter(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.BackColor = _isDarkMode ? _darkButtonHover : _lightButtonHover;
            }
        }

        private void OnButtonMouseLeave(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.BackColor = _isDarkMode ? _darkButtonBackground : _lightButtonBackground;
            }
        }

        private void ApplyTheme()
        {
            if (_isDarkMode)
            {
                this.BackColor = _darkBackground;
                this.ForeColor = _darkForeground;

                _findTextBox.BackColor = _darkInputBackground;
                _findTextBox.ForeColor = _darkForeground;
                _replaceTextBox.BackColor = _darkInputBackground;
                _replaceTextBox.ForeColor = _darkForeground;

                foreach (Control control in this.Controls)
                {
                    if (control is Button button)
                    {
                        button.BackColor = _darkButtonBackground;
                        button.ForeColor = _darkForeground;
                        button.FlatAppearance.BorderColor = _darkForeground;
                    }
                    else if (control is Label || control is CheckBox)
                    {
                        control.ForeColor = _darkForeground;
                    }
                }
            }
            else
            {
                this.BackColor = _lightBackground;
                this.ForeColor = _lightForeground;

                _findTextBox.BackColor = _lightInputBackground;
                _findTextBox.ForeColor = _lightForeground;
                _replaceTextBox.BackColor = _lightInputBackground;
                _replaceTextBox.ForeColor = _lightForeground;

                foreach (Control control in this.Controls)
                {
                    if (control is Button button)
                    {
                        button.BackColor = _lightButtonBackground;
                        button.ForeColor = _lightForeground;
                        button.FlatAppearance.BorderColor = _lightForeground;
                    }
                    else if (control is Label || control is CheckBox)
                    {
                        control.ForeColor = _lightForeground;
                    }
                }
            }
        }

        private void OnDialogKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
            else if (e.KeyCode == Keys.F3 || (e.Control && e.KeyCode == Keys.F))
            {
                OnFindNextClick(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (_findTextBox.Focused)
                {
                    OnFindNextClick(this, EventArgs.Empty);
                }
                else if (_replaceTextBox.Focused)
                {
                    OnReplaceClick(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
        }

        private void OnFindTextChanged(object? sender, EventArgs e)
        {
            if (_findTextBox.Text != _lastSearchText)
            {
                _currentSearchIndex = 0;
                _lastSearchText = _findTextBox.Text;
            }

            bool hasText = !string.IsNullOrEmpty(_findTextBox.Text);
            _replaceButton.Enabled = hasText;
            _replaceAllButton.Enabled = hasText;
            
            if (hasText)
            {
                _statusLabel.Text = "";
            }
        }

        private void OnSearchOptionChanged(object? sender, EventArgs e)
        {
            _currentSearchIndex = 0;
        }

        private void OnFindNextClick(object? sender, EventArgs e)
        {
            FindNext();
        }

        private bool FindNext()
        {
            if (string.IsNullOrEmpty(_findTextBox.Text))
            {
                _statusLabel.Text = "Please enter text to find.";
                return false;
            }

            string searchText = _findTextBox.Text;
            string content = _targetTextBox.Text;
            
            StringComparison comparison = _caseSensitiveCheckBox.Checked 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            int searchStart = _currentSearchIndex;
            if (_targetTextBox.SelectionLength > 0 && _currentSearchIndex == _targetTextBox.SelectionStart)
            {
                searchStart = _currentSearchIndex + 1;
            }

            int foundIndex = -1;

            if (_wholeWordCheckBox.Checked)
            {
                foundIndex = FindWholeWord(content, searchText, searchStart, comparison);
            }
            else
            {
                foundIndex = content.IndexOf(searchText, searchStart, comparison);
            }

            if (foundIndex == -1 && searchStart > 0)
            {
                if (_wholeWordCheckBox.Checked)
                {
                    foundIndex = FindWholeWord(content, searchText, 0, comparison);
                }
                else
                {
                    foundIndex = content.IndexOf(searchText, 0, comparison);
                }
            }

            if (foundIndex != -1)
            {
                _targetTextBox.Select(foundIndex, searchText.Length);
                _targetTextBox.ScrollToCaret();
                _currentSearchIndex = foundIndex;
                _statusLabel.Text = "";
                _targetTextBox.Focus();
                return true;
            }
            else
            {
                _statusLabel.Text = $"Cannot find \"{searchText}\"";
                return false;
            }
        }

        private int FindWholeWord(string content, string searchText, int startIndex, StringComparison comparison)
        {
            int index = startIndex;
            while (index < content.Length)
            {
                index = content.IndexOf(searchText, index, comparison);
                if (index == -1)
                    return -1;

                bool isStartOfWord = index == 0 || !char.IsLetterOrDigit(content[index - 1]);
                bool isEndOfWord = index + searchText.Length >= content.Length || !char.IsLetterOrDigit(content[index + searchText.Length]);

                if (isStartOfWord && isEndOfWord)
                    return index;

                index++;
            }
            return -1;
        }

        private void OnReplaceClick(object? sender, EventArgs e)
        {
            if (_targetTextBox.SelectionLength > 0)
            {
                string selectedText = _targetTextBox.SelectedText;
                string searchText = _findTextBox.Text;
                
                StringComparison comparison = _caseSensitiveCheckBox.Checked 
                    ? StringComparison.Ordinal 
                    : StringComparison.OrdinalIgnoreCase;

                if (string.Equals(selectedText, searchText, comparison))
                {
                    _targetTextBox.SelectedText = _replaceTextBox.Text;
                    _statusLabel.Text = "Replaced 1 occurrence.";
                }
            }
            
            FindNext();
        }

        private void OnReplaceAllClick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_findTextBox.Text))
            {
                _statusLabel.Text = "Please enter text to find.";
                return;
            }

            string searchText = _findTextBox.Text;
            string replaceText = _replaceTextBox.Text;
            string content = _targetTextBox.Text;
            int replacements = 0;

            if (_wholeWordCheckBox.Checked)
            {
                StringComparison comparison = _caseSensitiveCheckBox.Checked 
                    ? StringComparison.Ordinal 
                    : StringComparison.OrdinalIgnoreCase;

                var result = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    @"\b" + System.Text.RegularExpressions.Regex.Escape(searchText) + @"\b",
                    replaceText,
                    _caseSensitiveCheckBox.Checked 
                        ? System.Text.RegularExpressions.RegexOptions.None 
                        : System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                replacements = (content.Length - result.Length) / (searchText.Length - replaceText.Length);
                if (searchText.Length == replaceText.Length)
                {
                    replacements = System.Text.RegularExpressions.Regex.Matches(
                        content,
                        @"\b" + System.Text.RegularExpressions.Regex.Escape(searchText) + @"\b",
                        _caseSensitiveCheckBox.Checked 
                            ? System.Text.RegularExpressions.RegexOptions.None 
                            : System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    ).Count;
                }

                _targetTextBox.Text = result;
            }
            else
            {
                if (_caseSensitiveCheckBox.Checked)
                {
                    string newContent = content.Replace(searchText, replaceText);
                    replacements = (content.Length - newContent.Length) / (searchText.Length - replaceText.Length);
                    if (searchText.Length == replaceText.Length)
                    {
                        replacements = 0;
                        int index = 0;
                        while ((index = content.IndexOf(searchText, index)) != -1)
                        {
                            replacements++;
                            index += searchText.Length;
                        }
                    }
                    _targetTextBox.Text = newContent;
                }
                else
                {
                    string result = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        System.Text.RegularExpressions.Regex.Escape(searchText),
                        replaceText,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    );

                    replacements = System.Text.RegularExpressions.Regex.Matches(
                        content,
                        System.Text.RegularExpressions.Regex.Escape(searchText),
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    ).Count;

                    _targetTextBox.Text = result;
                }
            }

            _statusLabel.Text = $"Replaced {replacements} occurrence(s).";
            _currentSearchIndex = 0;
        }

        public void SetSearchText(string text)
        {
            _findTextBox.Text = text;
            _findTextBox.SelectAll();
            _findTextBox.Focus();
        }

        // P/Invoke for window dragging
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void OnFormPaint(object? sender, PaintEventArgs e)
        {
            // Draw border around the form
            using (var pen = new Pen(_isDarkMode ? _darkButtonBackground : Color.LightGray, 1))
            {
                var rect = this.ClientRectangle;
                e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }
        }
    }
}