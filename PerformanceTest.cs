using System;
using System.Threading.Tasks;
using ModernTextViewer.src.Services;

/// <summary>
/// Simple test to verify performance monitoring components work correctly
/// </summary>
class PerformanceTest
{
    public static async Task TestPerformanceMonitoring()
    {
        try
        {
            Console.WriteLine("=== Performance Monitoring System Test ===");
            
            // Create performance monitor
            using var monitor = new PerformanceMonitor();
            
            // Set monitoring level
            monitor.Level = PerformanceMonitor.MonitoringLevel.Detailed;
            Console.WriteLine($"Monitoring Level: {monitor.Level}");
            
            // Subscribe to alerts
            monitor.PerformanceAlert += (s, e) => 
            {
                Console.WriteLine($"Alert: {e.AlertType} - {e.Message} (Severity: {e.Severity})");
            };
            
            // Test file operation tracking
            var testFilePath = "test.txt";
            var testFileSize = 1024; // 1KB
            
            monitor.StartFileOperation("load", testFilePath, testFileSize);
            await Task.Delay(100); // Simulate file operation
            monitor.EndFileOperation("load", testFilePath, true);
            
            // Test metrics retrieval
            var metrics = monitor.GetCurrentMetrics();
            Console.WriteLine($"\nCurrent Metrics ({metrics.Count} total):");
            foreach (var metric in metrics)
            {
                Console.WriteLine($"  {metric.Key}: {metric.Value.Value:F2} {metric.Value.Unit}");
            }
            
            // Test file size analysis
            Console.WriteLine("\n=== File Size Warning System Test ===");
            var fileInfo = FileSizeWarningService.AnalyzeFileSize("C:\\Windows\\notepad.exe");
            Console.WriteLine($"File: {fileInfo.FormattedSize}");
            Console.WriteLine($"Category: {fileInfo.Category}");
            Console.WriteLine($"Recommendation: {fileInfo.Recommendation}");
            Console.WriteLine($"Estimated Load Time: {fileInfo.EstimatedLoadTime.TotalSeconds:F1}s");
            Console.WriteLine($"Estimated Memory: {fileInfo.EstimatedMemoryUsageMB} MB");
            
            // Test performance report export
            Console.WriteLine("\n=== Performance Report ===");
            var report = monitor.ExportPerformanceReport();
            Console.WriteLine(report);
            
            Console.WriteLine("\n✅ Performance monitoring system test completed successfully!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}