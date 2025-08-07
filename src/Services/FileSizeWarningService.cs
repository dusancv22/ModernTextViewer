using System;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace ModernTextViewer.src.Services
{
    /// <summary>
    /// Service for providing file size warnings and loading recommendations
    /// </summary>
    public static class FileSizeWarningService
    {
        private const long WARNING_SIZE_THRESHOLD = 10 * 1024 * 1024; // 10MB
        private const long CRITICAL_SIZE_THRESHOLD = 50 * 1024 * 1024; // 50MB
        private const long MAXIMUM_SIZE_THRESHOLD = 500 * 1024 * 1024; // 500MB

        public enum FileSizeCategory
        {
            Normal,
            Large,
            VeryLarge,
            Extreme
        }

        public enum LoadingRecommendation
        {
            Normal,
            Streaming,
            NotRecommended
        }

        public class FileSizeInfo
        {
            public long SizeBytes { get; set; }
            public FileSizeCategory Category { get; set; }
            public LoadingRecommendation Recommendation { get; set; }
            public TimeSpan EstimatedLoadTime { get; set; }
            public long EstimatedMemoryUsageMB { get; set; }
            public string FormattedSize { get; set; } = string.Empty;
            public string WarningMessage { get; set; } = string.Empty;
        }

        /// <summary>
        /// Analyzes file size and returns comprehensive information
        /// </summary>
        public static FileSizeInfo AnalyzeFileSize(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            var sizeBytes = fileInfo.Length;
            
            var info = new FileSizeInfo
            {
                SizeBytes = sizeBytes,
                FormattedSize = FormatBytes(sizeBytes)
            };

            // Categorize file size
            if (sizeBytes < WARNING_SIZE_THRESHOLD)
            {
                info.Category = FileSizeCategory.Normal;
                info.Recommendation = LoadingRecommendation.Normal;
            }
            else if (sizeBytes < CRITICAL_SIZE_THRESHOLD)
            {
                info.Category = FileSizeCategory.Large;
                info.Recommendation = LoadingRecommendation.Normal;
                info.WarningMessage = "This is a large file. Loading may take some time and use significant memory.";
            }
            else if (sizeBytes < MAXIMUM_SIZE_THRESHOLD)
            {
                info.Category = FileSizeCategory.VeryLarge;
                info.Recommendation = LoadingRecommendation.Streaming;
                info.WarningMessage = "This is a very large file. Streaming mode is recommended for better performance.";
            }
            else
            {
                info.Category = FileSizeCategory.Extreme;
                info.Recommendation = LoadingRecommendation.NotRecommended;
                info.WarningMessage = "This file is extremely large and may cause performance issues or system instability.";
            }

            // Estimate loading time and memory usage
            EstimatePerformanceMetrics(info);

            return info;
        }

        /// <summary>
        /// Shows file size warning dialog and gets user's choice
        /// </summary>
        public static async Task<FileLoadChoice> ShowFileSizeWarningAsync(IWin32Window? owner, string filePath)
        {
            return await Task.Run(() =>
            {
                var info = AnalyzeFileSize(filePath);

                // No warning needed for normal sized files
                if (info.Category == FileSizeCategory.Normal)
                {
                    return FileLoadChoice.LoadNormal;
                }

                return ShowWarningDialog(owner, info, Path.GetFileName(filePath));
            });
        }

        private static FileLoadChoice ShowWarningDialog(IWin32Window? owner, FileSizeInfo info, string fileName)
        {
            var title = GetDialogTitle(info.Category);
            var message = BuildWarningMessage(info, fileName);

            // Create custom dialog
            using var dialog = new Form
            {
                Text = title,
                Size = new System.Drawing.Size(500, 350),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowIcon = false,
                TopMost = true
            };

            // Icon panel
            var iconPanel = new Panel
            {
                Size = new System.Drawing.Size(64, 64),
                Location = new System.Drawing.Point(20, 20)
            };

            var icon = new PictureBox
            {
                Size = new System.Drawing.Size(48, 48),
                Location = new System.Drawing.Point(8, 8),
                Image = SystemIcons.Warning.ToBitmap(),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            iconPanel.Controls.Add(icon);

            // Message label
            var messageLabel = new Label
            {
                Text = message,
                Location = new System.Drawing.Point(100, 20),
                Size = new System.Drawing.Size(360, 200),
                Font = new System.Drawing.Font("Segoe UI", 9),
                AutoSize = false
            };

            // Buttons
            var buttonPanel = new Panel
            {
                Size = new System.Drawing.Size(460, 50),
                Location = new System.Drawing.Point(20, 240),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var choice = FileLoadChoice.Cancel;

            // Button layout depends on recommendation
            if (info.Recommendation == LoadingRecommendation.NotRecommended)
            {
                // Only Cancel and Force Load buttons
                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Size = new System.Drawing.Size(100, 35),
                    Location = new System.Drawing.Point(250, 10),
                    UseVisualStyleBackColor = true,
                    DialogResult = DialogResult.Cancel
                };
                cancelButton.Click += (s, e) => { choice = FileLoadChoice.Cancel; dialog.Close(); };

                var forceLoadButton = new Button
                {
                    Text = "Force Load (Not Recommended)",
                    Size = new System.Drawing.Size(200, 35),
                    Location = new System.Drawing.Point(360, 10),
                    UseVisualStyleBackColor = true,
                    ForeColor = System.Drawing.Color.Red
                };
                forceLoadButton.Click += (s, e) => { choice = FileLoadChoice.ForceLoad; dialog.Close(); };

                buttonPanel.Controls.AddRange(new Control[] { cancelButton, forceLoadButton });
            }
            else
            {
                // Normal, Streaming, and Cancel buttons
                var cancelButton = new Button
                {
                    Text = "Cancel",
                    Size = new System.Drawing.Size(80, 35),
                    Location = new System.Drawing.Point(70, 10),
                    UseVisualStyleBackColor = true,
                    DialogResult = DialogResult.Cancel
                };
                cancelButton.Click += (s, e) => { choice = FileLoadChoice.Cancel; dialog.Close(); };

                var normalButton = new Button
                {
                    Text = "Load Normally",
                    Size = new System.Drawing.Size(120, 35),
                    Location = new System.Drawing.Point(160, 10),
                    UseVisualStyleBackColor = true
                };
                normalButton.Click += (s, e) => { choice = FileLoadChoice.LoadNormal; dialog.Close(); };

                var streamingButton = new Button
                {
                    Text = "Use Streaming",
                    Size = new System.Drawing.Size(120, 35),
                    Location = new System.Drawing.Point(290, 10),
                    UseVisualStyleBackColor = true,
                    ForeColor = info.Recommendation == LoadingRecommendation.Streaming ? 
                        System.Drawing.Color.Green : System.Drawing.Color.Black
                };
                streamingButton.Click += (s, e) => { choice = FileLoadChoice.LoadStreaming; dialog.Close(); };

                // Set default button based on recommendation
                if (info.Recommendation == LoadingRecommendation.Streaming)
                {
                    dialog.AcceptButton = streamingButton;
                    streamingButton.Font = new System.Drawing.Font(streamingButton.Font, System.Drawing.FontStyle.Bold);
                }
                else
                {
                    dialog.AcceptButton = normalButton;
                }

                buttonPanel.Controls.AddRange(new Control[] { cancelButton, normalButton, streamingButton });
            }

            dialog.CancelButton = buttonPanel.Controls.OfType<Button>().First(b => b.Text == "Cancel");

            // Add all controls to dialog
            dialog.Controls.AddRange(new Control[] { iconPanel, messageLabel, buttonPanel });

            // Show dialog
            if (owner != null && owner is Control ownerControl && ownerControl.InvokeRequired)
            {
                return (FileLoadChoice)ownerControl.Invoke(() => 
                {
                    dialog.ShowDialog(owner);
                    return choice;
                });
            }
            else
            {
                dialog.ShowDialog(owner);
                return choice;
            }
        }

        private static string GetDialogTitle(FileSizeCategory category)
        {
            return category switch
            {
                FileSizeCategory.Large => "Large File Warning",
                FileSizeCategory.VeryLarge => "Very Large File Warning",
                FileSizeCategory.Extreme => "Extremely Large File Warning",
                _ => "File Size Warning"
            };
        }

        private static string BuildWarningMessage(FileSizeInfo info, string fileName)
        {
            var message = $"File: {fileName}\n";
            message += $"Size: {info.FormattedSize}\n\n";
            message += $"{info.WarningMessage}\n\n";
            message += $"Estimated load time: {info.EstimatedLoadTime.TotalSeconds:F1} seconds\n";
            message += $"Estimated memory usage: {info.EstimatedMemoryUsageMB} MB\n\n";

            if (info.Recommendation == LoadingRecommendation.Streaming)
            {
                message += "Streaming mode will:\n";
                message += "• Load file content on-demand\n";
                message += "• Use less memory\n";
                message += "• Provide better responsiveness\n";
                message += "• Some features may be limited\n\n";
            }

            message += "How would you like to proceed?";

            return message;
        }

        private static void EstimatePerformanceMetrics(FileSizeInfo info)
        {
            var sizeMB = info.SizeBytes / 1024.0 / 1024.0;

            // Estimate load time based on typical disk I/O and processing
            // Assumes ~100 MB/s disk read + text processing overhead
            var baseLoadTimeSeconds = sizeMB / 50.0; // Conservative estimate
            info.EstimatedLoadTime = TimeSpan.FromSeconds(Math.Max(0.1, baseLoadTimeSeconds));

            // Estimate memory usage (text typically uses 2-3x file size in memory)
            info.EstimatedMemoryUsageMB = (long)(sizeMB * 2.5);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} bytes";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
        }
    }

    public enum FileLoadChoice
    {
        Cancel,
        LoadNormal,
        LoadStreaming,
        ForceLoad
    }
}