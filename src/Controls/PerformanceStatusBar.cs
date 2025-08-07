using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ModernTextViewer.src.Services;

namespace ModernTextViewer.src.Controls
{
    /// <summary>
    /// Status bar component that displays real-time performance metrics
    /// </summary>
    public class PerformanceStatusBar : Panel
    {
        private readonly Label memoryLabel;
        private readonly Label cpuLabel;
        private readonly Label statusLabel;
        private readonly ProgressBar memoryProgressBar;
        private readonly Button detailsButton;
        
        private PerformanceMonitor? performanceMonitor;
        private bool isDarkMode = true;
        
        // Color schemes
        private readonly Color darkBackColor = Color.FromArgb(45, 45, 45);
        private readonly Color darkForeColor = Color.FromArgb(220, 220, 220);
        private readonly Color lightBackColor = Color.FromArgb(240, 240, 240);
        private readonly Color lightForeColor = Color.FromArgb(30, 30, 30);
        
        // Performance thresholds for color coding
        private const double HIGH_MEMORY_THRESHOLD = 500; // MB
        private const double CRITICAL_MEMORY_THRESHOLD = 1000; // MB
        private const double HIGH_CPU_THRESHOLD = 70; // %

        public event EventHandler? ShowDetailedMetrics;

        public PerformanceStatusBar()
        {
            Height = 25;
            Dock = DockStyle.Bottom;
            BorderStyle = BorderStyle.FixedSingle;

            // Memory label
            memoryLabel = new Label
            {
                Text = "Memory: -- MB",
                Size = new Size(100, 20),
                Location = new Point(5, 2),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.25f)
            };

            // Memory progress bar (mini)
            memoryProgressBar = new ProgressBar
            {
                Size = new Size(60, 16),
                Location = new Point(110, 4),
                Minimum = 0,
                Maximum = 1000, // Up to 1GB
                Style = ProgressBarStyle.Continuous,
                Visible = false // Initially hidden, shown when memory usage is significant
            };

            // CPU label
            cpuLabel = new Label
            {
                Text = "CPU: --%",
                Size = new Size(70, 20),
                Location = new Point(180, 2),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.25f)
            };

            // Status label (for current operations)
            statusLabel = new Label
            {
                Text = "Ready",
                Size = new Size(200, 20),
                Location = new Point(260, 2),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.25f)
            };

            // Details button
            detailsButton = new Button
            {
                Text = "Performance",
                Size = new Size(80, 20),
                Location = new Point(Width - 85, 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.25f),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            detailsButton.Click += (s, e) => ShowDetailedMetrics?.Invoke(this, EventArgs.Empty);

            Controls.AddRange(new Control[] 
            { 
                memoryLabel, 
                memoryProgressBar, 
                cpuLabel, 
                statusLabel, 
                detailsButton 
            });

            ApplyTheme(isDarkMode);
        }

        public void SetPerformanceMonitor(PerformanceMonitor monitor)
        {
            if (performanceMonitor != null)
            {
                performanceMonitor.MetricsUpdated -= OnMetricsUpdated;
                performanceMonitor.PerformanceAlert -= OnPerformanceAlert;
            }

            performanceMonitor = monitor;
            
            if (performanceMonitor != null)
            {
                performanceMonitor.MetricsUpdated += OnMetricsUpdated;
                performanceMonitor.PerformanceAlert += OnPerformanceAlert;
            }
        }

        public void ApplyTheme(bool darkMode)
        {
            isDarkMode = darkMode;
            
            var backColor = darkMode ? darkBackColor : lightBackColor;
            var foreColor = darkMode ? darkForeColor : lightForeColor;
            
            BackColor = backColor;
            
            foreach (Control control in Controls)
            {
                if (control is Label label)
                {
                    label.ForeColor = foreColor;
                    label.BackColor = backColor;
                }
                else if (control is Button button)
                {
                    button.BackColor = darkMode ? Color.FromArgb(60, 60, 60) : Color.FromArgb(220, 220, 220);
                    button.ForeColor = foreColor;
                    button.FlatAppearance.BorderColor = darkMode ? Color.FromArgb(100, 100, 100) : Color.FromArgb(160, 160, 160);
                }
            }
            
            // Progress bar colors
            if (darkMode)
            {
                memoryProgressBar.ForeColor = Color.FromArgb(100, 150, 200);
            }
            else
            {
                memoryProgressBar.ForeColor = Color.FromArgb(50, 100, 150);
            }
        }

        public void SetStatus(string status, bool isTemporary = true)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetStatus(status, isTemporary));
                return;
            }

            statusLabel.Text = status;
            
            if (isTemporary)
            {
                // Clear status after 3 seconds
                var timer = new System.Windows.Forms.Timer { Interval = 3000 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (statusLabel.Text == status)
                    {
                        statusLabel.Text = "Ready";
                        ApplyStatusColor(Color.Empty);
                    }
                };
                timer.Start();
            }
        }

        public void SetOperationStatus(string operation, int progressPercent = -1)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetOperationStatus(operation, progressPercent));
                return;
            }

            var statusText = progressPercent >= 0 ? 
                $"{operation} ({progressPercent}%)" : 
                operation;
                
            statusLabel.Text = statusText;
        }

        public void ClearOperationStatus()
        {
            if (InvokeRequired)
            {
                Invoke(ClearOperationStatus);
                return;
            }

            statusLabel.Text = "Ready";
            ApplyStatusColor(Color.Empty);
        }

        private void OnMetricsUpdated(object? sender, PerformanceMetricsEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => OnMetricsUpdated(sender, e));
                return;
            }

            try
            {
                UpdateMemoryDisplay(e.Metrics);
                UpdateCPUDisplay(e.Metrics);
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, 
                    ErrorManager.ErrorSeverity.Warning,
                    "Error updating performance status bar", ex);
            }
        }

        private void UpdateMemoryDisplay(System.Collections.Generic.Dictionary<string, PerformanceMetric> metrics)
        {
            if (metrics.TryGetValue("MemoryUsageMB", out var memoryMetric))
            {
                var memoryUsage = memoryMetric.Value;
                memoryLabel.Text = $"Memory: {memoryUsage:F0} MB";
                
                // Update progress bar
                memoryProgressBar.Value = Math.Min((int)memoryUsage, memoryProgressBar.Maximum);
                
                // Show/hide progress bar based on usage
                memoryProgressBar.Visible = memoryUsage > 50; // Show when using more than 50MB
                
                // Color code based on usage
                if (memoryUsage > CRITICAL_MEMORY_THRESHOLD)
                {
                    memoryLabel.ForeColor = Color.Red;
                }
                else if (memoryUsage > HIGH_MEMORY_THRESHOLD)
                {
                    memoryLabel.ForeColor = Color.Orange;
                }
                else
                {
                    memoryLabel.ForeColor = isDarkMode ? darkForeColor : lightForeColor;
                }
            }
        }

        private void UpdateCPUDisplay(System.Collections.Generic.Dictionary<string, PerformanceMetric> metrics)
        {
            if (metrics.TryGetValue("CpuUsage", out var cpuMetric))
            {
                var cpuUsage = cpuMetric.Value;
                cpuLabel.Text = $"CPU: {cpuUsage:F0}%";
                
                // Color code based on usage
                if (cpuUsage > HIGH_CPU_THRESHOLD)
                {
                    cpuLabel.ForeColor = Color.Red;
                }
                else if (cpuUsage > 50)
                {
                    cpuLabel.ForeColor = Color.Orange;
                }
                else
                {
                    cpuLabel.ForeColor = isDarkMode ? darkForeColor : lightForeColor;
                }
            }
            else
            {
                cpuLabel.Text = "CPU: --%";
                cpuLabel.ForeColor = isDarkMode ? darkForeColor : lightForeColor;
            }
        }

        private void OnPerformanceAlert(object? sender, PerformanceAlertEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => OnPerformanceAlert(sender, e));
                return;
            }

            // Flash the status with alert color
            var alertColor = e.Severity switch
            {
                AlertSeverity.Critical => Color.Red,
                AlertSeverity.Warning => Color.Orange,
                _ => Color.Yellow
            };

            SetStatus($"Alert: {e.Message}");
            ApplyStatusColor(alertColor);

            // Show tooltip with more details
            var tooltip = new ToolTip();
            var detailMessage = $"{e.AlertType}: {e.Message}\nTime: {e.Timestamp:HH:mm:ss}";
            tooltip.Show(detailMessage, this, statusLabel.Location, 5000);
        }

        private void ApplyStatusColor(Color color)
        {
            if (color == Color.Empty)
            {
                statusLabel.ForeColor = isDarkMode ? darkForeColor : lightForeColor;
            }
            else
            {
                statusLabel.ForeColor = color;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            
            // Adjust details button position
            if (detailsButton != null)
            {
                detailsButton.Location = new Point(Width - 85, 2);
            }
            
            // Adjust status label width
            if (statusLabel != null && detailsButton != null)
            {
                var availableWidth = detailsButton.Left - statusLabel.Left - 10;
                statusLabel.Size = new Size(Math.Max(100, availableWidth), statusLabel.Height);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (performanceMonitor != null)
                {
                    performanceMonitor.MetricsUpdated -= OnMetricsUpdated;
                    performanceMonitor.PerformanceAlert -= OnPerformanceAlert;
                }
            }
            
            base.Dispose(disposing);
        }
    }
}