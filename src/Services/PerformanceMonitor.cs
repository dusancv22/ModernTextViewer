using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModernTextViewer.src.Services
{
    /// <summary>
    /// Comprehensive performance monitoring system for tracking application performance metrics
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        public enum MonitoringLevel
        {
            Off = 0,
            Basic = 1,
            Detailed = 2
        }

        private readonly System.Threading.Timer performanceUpdateTimer;
        private readonly ConcurrentDictionary<string, PerformanceMetric> metrics = new();
        private readonly List<PerformanceEvent> performanceEvents = new();
        private readonly object eventLock = new();
        private MonitoringLevel currentLevel = MonitoringLevel.Basic;
        private bool disposed = false;

        // Performance counters
        private readonly PerformanceCounter? cpuCounter;
        private readonly PerformanceCounter? memoryCounter;
        private readonly Process currentProcess;
        private readonly Stopwatch uptime = Stopwatch.StartNew();

        // Memory tracking
        private long peakMemoryUsage = 0;
        private long lastGCMemory = 0;
        private int gcCollectionCount = 0;

        // File operation tracking
        private readonly Dictionary<string, FileOperationStats> fileOperationStats = new();

        // UI responsiveness tracking
        private readonly Queue<TimeSpan> frameTimes = new();
        private const int MAX_FRAME_SAMPLES = 100;

        public event EventHandler<PerformanceAlertEventArgs>? PerformanceAlert;
        public event EventHandler<PerformanceMetricsEventArgs>? MetricsUpdated;

        public PerformanceMonitor()
        {
            currentProcess = Process.GetCurrentProcess();
            
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance, 
                    ErrorManager.ErrorSeverity.Warning,
                    "Failed to initialize performance counters", ex);
            }

            // Update performance metrics every 2 seconds
            performanceUpdateTimer = new System.Threading.Timer(UpdatePerformanceMetrics, null, 2000, 2000);

            InitializeMetrics();
        }

        public MonitoringLevel Level
        {
            get => currentLevel;
            set
            {
                currentLevel = value;
                LogPerformanceEvent("MonitoringLevelChanged", $"Level set to {value}");
            }
        }

        private void InitializeMetrics()
        {
            // Initialize core metrics
            metrics["CpuUsage"] = new PerformanceMetric("CPU Usage", "%", 0.0);
            metrics["MemoryUsageMB"] = new PerformanceMetric("Memory Usage", "MB", 0.0);
            metrics["PeakMemoryMB"] = new PerformanceMetric("Peak Memory", "MB", 0.0);
            metrics["GCPressure"] = new PerformanceMetric("GC Pressure", "Collections/min", 0.0);
            metrics["UptimeSeconds"] = new PerformanceMetric("Uptime", "seconds", 0.0);
            metrics["FileLoadTime"] = new PerformanceMetric("File Load Time", "ms", 0.0);
            metrics["FileLoadThroughput"] = new PerformanceMetric("File Load Throughput", "MB/s", 0.0);
            metrics["UIFrameTime"] = new PerformanceMetric("UI Frame Time", "ms", 0.0);
            metrics["UIThreadBlocking"] = new PerformanceMetric("UI Thread Blocking", "ms", 0.0);
        }

        public void StartFileOperation(string operationType, string filePath, long fileSizeBytes = 0)
        {
            if (currentLevel == MonitoringLevel.Off) return;

            var operation = new FileOperationStats
            {
                OperationType = operationType,
                FilePath = filePath,
                FileSizeBytes = fileSizeBytes,
                StartTime = DateTime.Now,
                Timer = Stopwatch.StartNew()
            };

            var key = $"{operationType}_{Path.GetFileName(filePath)}_{DateTime.Now.Ticks}";
            fileOperationStats[key] = operation;

            LogPerformanceEvent($"FileOperation_Started", 
                $"{operationType} started for {Path.GetFileName(filePath)} ({FormatBytes(fileSizeBytes)})");
        }

        public void EndFileOperation(string operationType, string filePath, bool success = true, string? error = null)
        {
            if (currentLevel == MonitoringLevel.Off) return;

            var key = fileOperationStats.Keys.FirstOrDefault(k => 
                k.StartsWith($"{operationType}_{Path.GetFileName(filePath)}"));

            if (key != null && fileOperationStats.TryGetValue(key, out var stats))
            {
                stats.Timer.Stop();
                stats.EndTime = DateTime.Now;
                stats.Success = success;
                stats.ErrorMessage = error;

                var duration = stats.Timer.ElapsedMilliseconds;
                var throughput = stats.FileSizeBytes > 0 ? 
                    (stats.FileSizeBytes / 1024.0 / 1024.0) / (duration / 1000.0) : 0.0;

                // Update metrics
                UpdateMetric("FileLoadTime", duration);
                if (throughput > 0)
                {
                    UpdateMetric("FileLoadThroughput", throughput);
                }

                LogPerformanceEvent($"FileOperation_Completed",
                    $"{operationType} completed for {Path.GetFileName(filePath)} " +
                    $"in {duration}ms ({throughput:F2} MB/s) - {(success ? "Success" : "Failed")}");

                // Check for performance alerts
                if (duration > GetFileOperationThreshold(operationType, stats.FileSizeBytes))
                {
                    FirePerformanceAlert(PerformanceAlertType.SlowFileOperation,
                        $"{operationType} took {duration}ms, expected <{GetFileOperationThreshold(operationType, stats.FileSizeBytes)}ms",
                        AlertSeverity.Warning);
                }

                fileOperationStats.Remove(key);
            }
        }

        public void TrackUIFrameTime(TimeSpan frameTime)
        {
            if (currentLevel == MonitoringLevel.Off) return;

            lock (frameTimes)
            {
                frameTimes.Enqueue(frameTime);
                if (frameTimes.Count > MAX_FRAME_SAMPLES)
                {
                    frameTimes.Dequeue();
                }

                var avgFrameTime = frameTimes.Average(t => t.TotalMilliseconds);
                UpdateMetric("UIFrameTime", avgFrameTime);

                // Alert if frame time is consistently high (> 16.67ms for 60fps)
                if (avgFrameTime > 16.67 && frameTimes.Count >= MAX_FRAME_SAMPLES)
                {
                    FirePerformanceAlert(PerformanceAlertType.PoorUIPerformance,
                        $"Average frame time is {avgFrameTime:F2}ms (target: <16.67ms)",
                        AlertSeverity.Warning);
                }
            }
        }

        public void TrackUIThreadBlocking(TimeSpan blockingTime)
        {
            if (currentLevel == MonitoringLevel.Off) return;

            UpdateMetric("UIThreadBlocking", blockingTime.TotalMilliseconds);

            // Alert if UI thread is blocked for more than 100ms
            if (blockingTime.TotalMilliseconds > 100)
            {
                FirePerformanceAlert(PerformanceAlertType.UIThreadBlocking,
                    $"UI thread was blocked for {blockingTime.TotalMilliseconds:F2}ms",
                    AlertSeverity.Warning);
            }
        }

        public void UpdateMetric(string name, double value)
        {
            if (metrics.TryGetValue(name, out var metric))
            {
                metric.Value = value;
                metric.LastUpdated = DateTime.Now;

                if (currentLevel == MonitoringLevel.Detailed)
                {
                    metric.History.Add(new MetricHistoryPoint(DateTime.Now, value));
                    
                    // Keep only last 1000 history points
                    if (metric.History.Count > 1000)
                    {
                        metric.History.RemoveAt(0);
                    }
                }
            }
        }

        private void UpdatePerformanceMetrics(object? state)
        {
            if (disposed || currentLevel == MonitoringLevel.Off) return;

            try
            {
                // Update memory usage
                var memoryUsage = currentProcess.WorkingSet64;
                var memoryUsageMB = memoryUsage / 1024.0 / 1024.0;
                UpdateMetric("MemoryUsageMB", memoryUsageMB);

                // Track peak memory usage
                if (memoryUsage > peakMemoryUsage)
                {
                    peakMemoryUsage = memoryUsage;
                    UpdateMetric("PeakMemoryMB", peakMemoryUsage / 1024.0 / 1024.0);
                }

                // Update GC pressure
                var currentGCMemory = GC.GetTotalMemory(false);
                var currentGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
                if (gcCollectionCount > 0)
                {
                    var gcPressure = (currentGCCount - gcCollectionCount) * 30; // Collections per minute
                    UpdateMetric("GCPressure", gcPressure);
                }
                lastGCMemory = currentGCMemory;
                gcCollectionCount = currentGCCount;

                // Update uptime
                UpdateMetric("UptimeSeconds", uptime.Elapsed.TotalSeconds);

                // Update CPU usage (if available)
                try
                {
                    var cpuUsage = cpuCounter?.NextValue() ?? 0;
                    UpdateMetric("CpuUsage", cpuUsage);
                }
                catch (Exception ex)
                {
                    if (currentLevel == MonitoringLevel.Detailed)
                    {
                        ErrorManager.LogError(ErrorManager.ErrorCategory.Performance,
                            ErrorManager.ErrorSeverity.Info,
                            "Failed to update CPU usage metric", ex);
                    }
                }

                // Check for memory pressure alerts
                if (memoryUsageMB > 500) // 500MB threshold
                {
                    FirePerformanceAlert(PerformanceAlertType.HighMemoryUsage,
                        $"Memory usage is {memoryUsageMB:F2}MB",
                        memoryUsageMB > 1000 ? AlertSeverity.Critical : AlertSeverity.Warning);
                }

                // Fire metrics updated event
                MetricsUpdated?.Invoke(this, new PerformanceMetricsEventArgs(GetCurrentMetrics()));
            }
            catch (Exception ex)
            {
                ErrorManager.LogError(ErrorManager.ErrorCategory.Performance,
                    ErrorManager.ErrorSeverity.Warning,
                    "Error updating performance metrics", ex);
            }
        }

        public Dictionary<string, PerformanceMetric> GetCurrentMetrics()
        {
            return new Dictionary<string, PerformanceMetric>(metrics);
        }

        public List<PerformanceEvent> GetRecentEvents(int count = 100)
        {
            lock (eventLock)
            {
                return performanceEvents.TakeLast(count).ToList();
            }
        }

        public string ExportPerformanceReport()
        {
            var report = new StringBuilder();
            report.AppendLine("=== ModernTextViewer Performance Report ===");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Monitoring Level: {currentLevel}");
            report.AppendLine();

            report.AppendLine("=== Current Metrics ===");
            foreach (var metric in metrics.Values)
            {
                report.AppendLine($"{metric.Name}: {metric.Value:F2} {metric.Unit}");
                if (currentLevel == MonitoringLevel.Detailed && metric.History.Count > 0)
                {
                    var avg = metric.History.Average(h => h.Value);
                    var min = metric.History.Min(h => h.Value);
                    var max = metric.History.Max(h => h.Value);
                    report.AppendLine($"  History: Avg={avg:F2}, Min={min:F2}, Max={max:F2}");
                }
            }

            report.AppendLine();
            report.AppendLine("=== Recent Performance Events ===");
            var recentEvents = GetRecentEvents(50);
            foreach (var evt in recentEvents.TakeLast(20))
            {
                report.AppendLine($"[{evt.Timestamp:HH:mm:ss}] {evt.EventType}: {evt.Description}");
            }

            return report.ToString();
        }

        private void LogPerformanceEvent(string eventType, string description)
        {
            if (currentLevel == MonitoringLevel.Off) return;

            var evt = new PerformanceEvent
            {
                Timestamp = DateTime.Now,
                EventType = eventType,
                Description = description
            };

            lock (eventLock)
            {
                performanceEvents.Add(evt);
                
                // Keep only last 1000 events
                if (performanceEvents.Count > 1000)
                {
                    performanceEvents.RemoveAt(0);
                }
            }
        }

        private void FirePerformanceAlert(PerformanceAlertType alertType, string message, AlertSeverity severity)
        {
            var alert = new PerformanceAlertEventArgs
            {
                AlertType = alertType,
                Message = message,
                Severity = severity,
                Timestamp = DateTime.Now,
                Metrics = GetCurrentMetrics()
            };

            PerformanceAlert?.Invoke(this, alert);
            
            LogPerformanceEvent($"Alert_{alertType}", $"{severity}: {message}");
        }

        private long GetFileOperationThreshold(string operationType, long fileSizeBytes)
        {
            // Dynamic thresholds based on file size
            var sizeMB = fileSizeBytes / 1024.0 / 1024.0;
            
            return operationType.ToLower() switch
            {
                "load" => Math.Max(1000, (long)(sizeMB * 100)), // 100ms per MB, minimum 1s
                "save" => Math.Max(500, (long)(sizeMB * 50)),   // 50ms per MB, minimum 500ms
                _ => 5000 // 5s default
            };
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} bytes";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
        }

        public void Dispose()
        {
            if (!disposed)
            {
                performanceUpdateTimer?.Dispose();
                cpuCounter?.Dispose();
                memoryCounter?.Dispose();
                currentProcess?.Dispose();
                disposed = true;
            }
        }
    }

    public class PerformanceMetric
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public double Value { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<MetricHistoryPoint> History { get; set; } = new();

        public PerformanceMetric(string name, string unit, double value)
        {
            Name = name;
            Unit = unit;
            Value = value;
            LastUpdated = DateTime.Now;
        }
    }

    public class MetricHistoryPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }

        public MetricHistoryPoint(DateTime timestamp, double value)
        {
            Timestamp = timestamp;
            Value = value;
        }
    }

    public class PerformanceEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class FileOperationStats
    {
        public string OperationType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Stopwatch Timer { get; set; } = new();
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }

    public class PerformanceAlertEventArgs : EventArgs
    {
        public PerformanceAlertType AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, PerformanceMetric> Metrics { get; set; } = new();
    }

    public class PerformanceMetricsEventArgs : EventArgs
    {
        public Dictionary<string, PerformanceMetric> Metrics { get; set; }

        public PerformanceMetricsEventArgs(Dictionary<string, PerformanceMetric> metrics)
        {
            Metrics = metrics;
        }
    }

    public enum PerformanceAlertType
    {
        HighMemoryUsage,
        SlowFileOperation,
        PoorUIPerformance,
        UIThreadBlocking,
        MemoryLeak,
        HighCPUUsage,
        GCPressure
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}