using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using ModernTextViewer.src.Services;

namespace ModernTextViewer.src.Forms
{
    /// <summary>
    /// Dialog for displaying detailed performance metrics and diagnostics
    /// </summary>
    public partial class PerformanceMetricsDialog : Form
    {
        private readonly PerformanceMonitor performanceMonitor;
        private readonly System.Windows.Forms.Timer updateTimer;
        private bool isDarkMode = true;

        // UI Controls
        private TabControl tabControl = null!;
        private TabPage metricsTabPage = null!;
        private TabPage eventsTabPage = null!;
        private TabPage settingsTabPage = null!;
        
        private ListView metricsListView = null!;
        private ListView eventsListView = null!;
        private ComboBox monitoringLevelComboBox = null!;
        private Button exportButton = null!;
        private Button refreshButton = null!;
        private Button clearEventsButton = null!;
        private Label refreshIntervalLabel = null!;
        private TrackBar refreshIntervalTrackBar = null!;

        // Color schemes
        private readonly Color darkBackColor = Color.FromArgb(30, 30, 30);
        private readonly Color darkForeColor = Color.FromArgb(220, 220, 220);
        private readonly Color darkControlColor = Color.FromArgb(45, 45, 45);

        public PerformanceMetricsDialog(PerformanceMonitor monitor, bool darkMode = true)
        {
            performanceMonitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            isDarkMode = darkMode;

            InitializeComponent();
            InitializeControls();
            ApplyTheme();

            // Update metrics every 2 seconds
            updateTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            // Initial load
            RefreshMetrics();
            RefreshEvents();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = "Performance Metrics - ModernTextViewer";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(600, 400);
            ShowIcon = false;
            ShowInTaskbar = false;

            ResumeLayout(false);
        }

        private void InitializeControls()
        {
            // Create tab control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f)
            };

            // Metrics tab
            metricsTabPage = new TabPage("Current Metrics");
            InitializeMetricsTab();

            // Events tab  
            eventsTabPage = new TabPage("Performance Events");
            InitializeEventsTab();

            // Settings tab
            settingsTabPage = new TabPage("Settings");
            InitializeSettingsTab();

            tabControl.TabPages.AddRange(new TabPage[] { metricsTabPage, eventsTabPage, settingsTabPage });
            Controls.Add(tabControl);
        }

        private void InitializeMetricsTab()
        {
            // Metrics list view
            metricsListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Location = new Point(10, 10),
                Size = new Size(760, 450),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9f)
            };

            metricsListView.Columns.AddRange(new ColumnHeader[]
            {
                new ColumnHeader { Text = "Metric", Width = 200 },
                new ColumnHeader { Text = "Current Value", Width = 120 },
                new ColumnHeader { Text = "Unit", Width = 80 },
                new ColumnHeader { Text = "Last Updated", Width = 150 },
                new ColumnHeader { Text = "Status", Width = 100 },
                new ColumnHeader { Text = "History (Avg)", Width = 100 }
            });

            // Refresh button
            refreshButton = new Button
            {
                Text = "Refresh",
                Size = new Size(80, 30),
                Location = new Point(10, 470),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            refreshButton.Click += (s, e) => RefreshMetrics();

            // Export button
            exportButton = new Button
            {
                Text = "Export Report",
                Size = new Size(100, 30),
                Location = new Point(100, 470),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            exportButton.Click += ExportButton_Click;

            metricsTabPage.Controls.AddRange(new Control[] { metricsListView, refreshButton, exportButton });
        }

        private void InitializeEventsTab()
        {
            // Events list view
            eventsListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Location = new Point(10, 10),
                Size = new Size(760, 450),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 8.5f)
            };

            eventsListView.Columns.AddRange(new ColumnHeader[]
            {
                new ColumnHeader { Text = "Time", Width = 120 },
                new ColumnHeader { Text = "Event Type", Width = 180 },
                new ColumnHeader { Text = "Description", Width = 450 }
            });

            // Clear events button
            clearEventsButton = new Button
            {
                Text = "Clear Events",
                Size = new Size(100, 30),
                Location = new Point(10, 470),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            clearEventsButton.Click += (s, e) => 
            {
                eventsListView.Items.Clear();
                RefreshEvents();
            };

            eventsTabPage.Controls.AddRange(new Control[] { eventsListView, clearEventsButton });
        }

        private void InitializeSettingsTab()
        {
            // Monitoring level
            var levelLabel = new Label
            {
                Text = "Monitoring Level:",
                Location = new Point(20, 30),
                Size = new Size(120, 20),
                Font = new Font("Segoe UI", 9f)
            };

            monitoringLevelComboBox = new ComboBox
            {
                Location = new Point(150, 28),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            monitoringLevelComboBox.Items.AddRange(new object[] { "Off", "Basic", "Detailed" });
            monitoringLevelComboBox.SelectedIndex = (int)performanceMonitor.Level;
            monitoringLevelComboBox.SelectedIndexChanged += (s, e) =>
            {
                performanceMonitor.Level = (PerformanceMonitor.MonitoringLevel)monitoringLevelComboBox.SelectedIndex;
            };

            // Refresh interval
            refreshIntervalLabel = new Label
            {
                Text = "Refresh Interval: 2 seconds",
                Location = new Point(20, 70),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9f)
            };

            refreshIntervalTrackBar = new TrackBar
            {
                Location = new Point(20, 95),
                Size = new Size(250, 45),
                Minimum = 1,
                Maximum = 10,
                Value = 2,
                TickFrequency = 1
            };
            refreshIntervalTrackBar.ValueChanged += (s, e) =>
            {
                var interval = refreshIntervalTrackBar.Value;
                refreshIntervalLabel.Text = $"Refresh Interval: {interval} second{(interval > 1 ? "s" : "")}";
                updateTimer.Interval = interval * 1000;
            };

            // Performance impact info
            var infoLabel = new Label
            {
                Text = "Performance Monitoring Impact:\n\n" +
                       "• Off: No performance monitoring\n" +
                       "• Basic: <1% CPU overhead, essential metrics only\n" +
                       "• Detailed: ~2% CPU overhead, full metrics and history\n\n" +
                       "The monitoring system is designed to have minimal impact\n" +
                       "on application performance.",
                Location = new Point(20, 160),
                Size = new Size(400, 150),
                Font = new Font("Segoe UI", 9f)
            };

            settingsTabPage.Controls.AddRange(new Control[] 
            { 
                levelLabel, 
                monitoringLevelComboBox, 
                refreshIntervalLabel, 
                refreshIntervalTrackBar, 
                infoLabel 
            });
        }

        private void RefreshMetrics()
        {
            if (InvokeRequired)
            {
                BeginInvoke(RefreshMetrics);
                return;
            }

            try
            {
                metricsListView.Items.Clear();
                var metrics = performanceMonitor.GetCurrentMetrics();

                foreach (var metric in metrics.Values.OrderBy(m => m.Name))
                {
                    var status = GetMetricStatus(metric);
                    var historyAvg = metric.History.Count > 0 ? 
                        metric.History.Average(h => h.Value).ToString("F2") : "N/A";

                    var item = new ListViewItem(metric.Name);
                    item.SubItems.Add(metric.Value.ToString("F2"));
                    item.SubItems.Add(metric.Unit);
                    item.SubItems.Add(metric.LastUpdated.ToString("HH:mm:ss"));
                    item.SubItems.Add(status.Text);
                    item.SubItems.Add(historyAvg);

                    // Color code based on status
                    if (status.IsWarning)
                    {
                        item.ForeColor = Color.Orange;
                    }
                    else if (status.IsCritical)
                    {
                        item.ForeColor = Color.Red;
                    }

                    metricsListView.Items.Add(item);
                }

                metricsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, 
                    ErrorManager.ErrorSeverity.Warning,
                    "Error refreshing performance metrics", ex);
            }
        }

        private void RefreshEvents()
        {
            if (InvokeRequired)
            {
                BeginInvoke(RefreshEvents);
                return;
            }

            try
            {
                eventsListView.Items.Clear();
                var events = performanceMonitor.GetRecentEvents(100);

                foreach (var evt in events.OrderByDescending(e => e.Timestamp).Take(50))
                {
                    var item = new ListViewItem(evt.Timestamp.ToString("HH:mm:ss"));
                    item.SubItems.Add(evt.EventType);
                    item.SubItems.Add(evt.Description);

                    // Color code alerts
                    if (evt.EventType.StartsWith("Alert_"))
                    {
                        if (evt.Description.Contains("Critical"))
                            item.ForeColor = Color.Red;
                        else if (evt.Description.Contains("Warning"))
                            item.ForeColor = Color.Orange;
                    }

                    eventsListView.Items.Add(item);
                }

                eventsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.UI, 
                    ErrorManager.ErrorSeverity.Warning,
                    "Error refreshing performance events", ex);
            }
        }

        private (string Text, bool IsWarning, bool IsCritical) GetMetricStatus(PerformanceMetric metric)
        {
            return metric.Name switch
            {
                "MemoryUsageMB" when metric.Value > 1000 => ("Critical", false, true),
                "MemoryUsageMB" when metric.Value > 500 => ("High", true, false),
                "CpuUsage" when metric.Value > 80 => ("High", true, false),
                "UIFrameTime" when metric.Value > 16.67 => ("Slow", true, false),
                "GCPressure" when metric.Value > 10 => ("High", true, false),
                _ => ("Normal", false, false)
            };
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"PerformanceReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var report = performanceMonitor.ExportPerformanceReport();
                    System.IO.File.WriteAllText(dialog.FileName, report);
                    MessageBox.Show($"Performance report exported to:\n{dialog.FileName}", 
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.FileIO, 
                    ErrorManager.ErrorSeverity.Error,
                    "Error exporting performance report", ex);
                MessageBox.Show("Failed to export performance report. Check the error log for details.", 
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (tabControl.SelectedTab == metricsTabPage)
            {
                RefreshMetrics();
            }
            else if (tabControl.SelectedTab == eventsTabPage)
            {
                RefreshEvents();
            }
        }

        private void ApplyTheme()
        {
            if (isDarkMode)
            {
                BackColor = darkBackColor;
                ForeColor = darkForeColor;

                ApplyDarkThemeToControls(this);
            }
        }

        private void ApplyDarkThemeToControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is TabControl tabCtrl)
                {
                    tabCtrl.BackColor = darkBackColor;
                    ApplyDarkThemeToControls(control);
                }
                else if (control is TabPage tabPage)
                {
                    tabPage.BackColor = darkBackColor;
                    tabPage.ForeColor = darkForeColor;
                    ApplyDarkThemeToControls(control);
                }
                else if (control is ListView listView)
                {
                    listView.BackColor = darkControlColor;
                    listView.ForeColor = darkForeColor;
                }
                else if (control is Button button)
                {
                    button.BackColor = darkControlColor;
                    button.ForeColor = darkForeColor;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                }
                else if (control is ComboBox combo)
                {
                    combo.BackColor = darkControlColor;
                    combo.ForeColor = darkForeColor;
                }
                else if (control is TrackBar trackBar)
                {
                    trackBar.BackColor = darkBackColor;
                }
                else if (control is Label label)
                {
                    label.ForeColor = darkForeColor;
                    label.BackColor = Color.Transparent;
                }

                ApplyDarkThemeToControls(control);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}