using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ModernTextViewer.src.Services
{
    public static class ErrorDialogService
    {
        public enum UserChoice
        {
            Retry,
            Cancel,
            Ignore,
            SaveAndExit,
            ViewDetails,
            GetHelp
        }

        public class ErrorDialogResult
        {
            public UserChoice Choice { get; set; }
            public bool DontShowAgain { get; set; }
            public string? UserInput { get; set; }
        }

        private static readonly HashSet<string> _suppressedErrors = new();

        public static ErrorDialogResult ShowError(
            string title,
            string message,
            ErrorManager.ErrorSeverity severity,
            List<string>? suggestedActions = null,
            bool canRetry = false,
            bool canIgnore = false,
            string? details = null,
            IWin32Window? parent = null)
        {
            // Check if this error type is suppressed
            var suppressKey = $"{title}:{message}";
            if (_suppressedErrors.Contains(suppressKey))
            {
                return new ErrorDialogResult { Choice = UserChoice.Ignore };
            }

            using var dialog = CreateErrorDialog(title, message, severity, suggestedActions, canRetry, canIgnore, details);
            
            var dialogResult = dialog.ShowDialog(parent);
            
            var result = new ErrorDialogResult();
            
            switch (dialogResult)
            {
                case DialogResult.Retry:
                    result.Choice = UserChoice.Retry;
                    break;
                case DialogResult.Cancel:
                case DialogResult.Abort:
                    result.Choice = UserChoice.Cancel;
                    break;
                case DialogResult.Ignore:
                    result.Choice = UserChoice.Ignore;
                    break;
                default:
                    result.Choice = UserChoice.Cancel;
                    break;
            }

            // Handle "Don't show again" if the dialog has this option
            if (dialog.Controls.Find("chkDontShowAgain", true).FirstOrDefault() is CheckBox dontShowAgainCheckBox && 
                dontShowAgainCheckBox.Checked)
            {
                result.DontShowAgain = true;
                _suppressedErrors.Add(suppressKey);
            }

            return result;
        }

        public static ErrorDialogResult ShowCriticalError(
            string title,
            string message,
            string recoveryInstructions,
            bool requiresRestart = false,
            string? details = null,
            IWin32Window? parent = null)
        {
            using var dialog = CreateCriticalErrorDialog(title, message, recoveryInstructions, requiresRestart, details);
            
            var dialogResult = dialog.ShowDialog(parent);
            
            return new ErrorDialogResult
            {
                Choice = dialogResult == DialogResult.Yes ? UserChoice.SaveAndExit : UserChoice.Cancel
            };
        }

        public static void ShowQuickError(string message, ErrorManager.ErrorSeverity severity = ErrorManager.ErrorSeverity.Warning)
        {
            var icon = severity switch
            {
                ErrorManager.ErrorSeverity.Critical => MessageBoxIcon.Error,
                ErrorManager.ErrorSeverity.Error => MessageBoxIcon.Error,
                ErrorManager.ErrorSeverity.Warning => MessageBoxIcon.Warning,
                _ => MessageBoxIcon.Information
            };

            MessageBox.Show(message, "ModernTextViewer", MessageBoxButtons.OK, icon);
        }

        private static Form CreateErrorDialog(
            string title,
            string message,
            ErrorManager.ErrorSeverity severity,
            List<string>? suggestedActions,
            bool canRetry,
            bool canIgnore,
            string? details)
        {
            var dialog = new Form
            {
                Text = "ModernTextViewer - Error",
                Size = new Size(500, 350),
                MinimumSize = new Size(400, 250),
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                BackColor = Color.White
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10),
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 5
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Icon and title
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Message
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Suggestions/Details
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Don't show again
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

            // Icon
            var iconPictureBox = new PictureBox
            {
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = GetErrorIcon(severity),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            layout.Controls.Add(iconPictureBox, 0, 0);

            // Title
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = GetSeverityColor(severity),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            layout.Controls.Add(titleLabel, 1, 0);

            // Message
            var messageLabel = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                MaximumSize = new Size(400, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                UseMnemonic = false
            };
            layout.Controls.Add(messageLabel, 1, 1);

            // Suggestions and details
            var contentPanel = CreateContentPanel(suggestedActions, details);
            layout.Controls.Add(contentPanel, 1, 2);

            // Don't show again checkbox (for non-critical errors)
            if (severity != ErrorManager.ErrorSeverity.Critical)
            {
                var dontShowAgainCheckBox = new CheckBox
                {
                    Text = "Don't show this error again",
                    Name = "chkDontShowAgain",
                    AutoSize = true,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left
                };
                layout.Controls.Add(dontShowAgainCheckBox, 1, 3);
            }

            // Buttons
            var buttonPanel = CreateButtonPanel(canRetry, canIgnore, details != null);
            layout.Controls.Add(buttonPanel, 1, 4);

            dialog.Controls.Add(layout);

            return dialog;
        }

        private static Form CreateCriticalErrorDialog(
            string title,
            string message,
            string recoveryInstructions,
            bool requiresRestart,
            string? details)
        {
            var dialog = new Form
            {
                Text = "ModernTextViewer - Critical Error",
                Size = new Size(550, 400),
                MinimumSize = new Size(450, 300),
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                BackColor = Color.FromArgb(252, 248, 248)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(15),
                Padding = new Padding(15),
                ColumnCount = 2,
                RowCount = 5
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Critical error icon
            var iconPictureBox = new PictureBox
            {
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = SystemIcons.Error.ToBitmap()
            };
            layout.Controls.Add(iconPictureBox, 0, 0);

            // Title
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                AutoSize = true,
                UseMnemonic = false
            };
            layout.Controls.Add(titleLabel, 1, 0);

            // Message
            var messageLabel = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                MaximumSize = new Size(450, 0),
                UseMnemonic = false
            };
            layout.Controls.Add(messageLabel, 1, 1);

            // Recovery instructions
            var recoveryLabel = new Label
            {
                Text = $"Recovery: {recoveryInstructions}",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.DarkBlue,
                AutoSize = true,
                MaximumSize = new Size(450, 0),
                UseMnemonic = false
            };
            layout.Controls.Add(recoveryLabel, 1, 2);

            // Details (if provided)
            if (!string.IsNullOrEmpty(details))
            {
                var detailsTextBox = new TextBox
                {
                    Text = details,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 8F)
                };
                layout.Controls.Add(detailsTextBox, 1, 3);
            }

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft
            };

            if (requiresRestart)
            {
                var saveAndExitButton = new Button
                {
                    Text = "Save && Exit",
                    DialogResult = DialogResult.Yes,
                    Size = new Size(100, 30),
                    Margin = new Padding(5, 0, 0, 0)
                };
                buttonPanel.Controls.Add(saveAndExitButton);

                var exitButton = new Button
                {
                    Text = "Exit",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(75, 30),
                    Margin = new Padding(5, 0, 0, 0)
                };
                buttonPanel.Controls.Add(exitButton);

                dialog.AcceptButton = saveAndExitButton;
                dialog.CancelButton = exitButton;
            }
            else
            {
                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(75, 30)
                };
                buttonPanel.Controls.Add(okButton);
                
                dialog.AcceptButton = okButton;
                dialog.CancelButton = okButton;
            }

            layout.Controls.Add(buttonPanel, 1, 4);
            dialog.Controls.Add(layout);

            return dialog;
        }

        private static Panel CreateContentPanel(List<string>? suggestedActions, string? details)
        {
            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // Suggestions tab
            if (suggestedActions?.Any() == true)
            {
                var suggestionsTab = new TabPage("Suggestions");
                var suggestionsListBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9F),
                    IntegralHeight = false,
                    SelectionMode = SelectionMode.None
                };

                foreach (var action in suggestedActions)
                {
                    suggestionsListBox.Items.Add($"• {action}");
                }

                suggestionsTab.Controls.Add(suggestionsListBox);
                tabControl.TabPages.Add(suggestionsTab);
            }

            // Details tab
            if (!string.IsNullOrEmpty(details))
            {
                var detailsTab = new TabPage("Technical Details");
                var detailsTextBox = new TextBox
                {
                    Text = details,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 8F),
                    WordWrap = true
                };
                detailsTab.Controls.Add(detailsTextBox);
                tabControl.TabPages.Add(detailsTab);
            }

            // If no tabs, return empty panel
            if (tabControl.TabPages.Count == 0)
            {
                return new Panel { Dock = DockStyle.Fill };
            }

            var panel = new Panel { Dock = DockStyle.Fill };
            panel.Controls.Add(tabControl);
            return panel;
        }

        private static Panel CreateButtonPanel(bool canRetry, bool canIgnore, bool hasDetails)
        {
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft
            };

            // Always have Cancel/Close button
            var cancelButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.Cancel,
                Size = new Size(75, 30),
                Margin = new Padding(5, 0, 0, 0)
            };
            buttonPanel.Controls.Add(cancelButton);

            if (canIgnore)
            {
                var ignoreButton = new Button
                {
                    Text = "Ignore",
                    DialogResult = DialogResult.Ignore,
                    Size = new Size(75, 30),
                    Margin = new Padding(5, 0, 0, 0)
                };
                buttonPanel.Controls.Add(ignoreButton);
            }

            if (canRetry)
            {
                var retryButton = new Button
                {
                    Text = "Retry",
                    DialogResult = DialogResult.Retry,
                    Size = new Size(75, 30),
                    Margin = new Padding(5, 0, 0, 0)
                };
                buttonPanel.Controls.Add(retryButton);
            }

            return buttonPanel;
        }

        private static Image GetErrorIcon(ErrorManager.ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorManager.ErrorSeverity.Critical => SystemIcons.Error.ToBitmap(),
                ErrorManager.ErrorSeverity.Error => SystemIcons.Error.ToBitmap(),
                ErrorManager.ErrorSeverity.Warning => SystemIcons.Warning.ToBitmap(),
                _ => SystemIcons.Information.ToBitmap()
            };
        }

        private static Color GetSeverityColor(ErrorManager.ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorManager.ErrorSeverity.Critical => Color.DarkRed,
                ErrorManager.ErrorSeverity.Error => Color.Red,
                ErrorManager.ErrorSeverity.Warning => Color.DarkOrange,
                _ => Color.DarkBlue
            };
        }

        public static void ClearSuppressedErrors()
        {
            _suppressedErrors.Clear();
        }

        public static void UnsuppressError(string title, string message)
        {
            var suppressKey = $"{title}:{message}";
            _suppressedErrors.Remove(suppressKey);
        }
    }
}