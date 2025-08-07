using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using ModernTextViewer.src.Services;
using ModernTextViewer.Tests.TestData;

namespace ModernTextViewer.Tests.PerformanceTests
{
    /// <summary>
    /// Performance tests for file loading operations
    /// Tests various file sizes and validates loading performance requirements
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class FileLoadingPerformanceTests
    {
        private TestFileSet? testFiles;
        private FileService? fileService;
        private StreamingTextProcessor? streamingProcessor;
        private PerformanceMonitor? performanceMonitor;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            // Generate test files once for all tests
            testFiles = await TestFileGenerator.GenerateTestFileSuiteAsync();
            
            fileService = new FileService();
            streamingProcessor = new StreamingTextProcessor();
            performanceMonitor = new PerformanceMonitor();
            performanceMonitor.Level = PerformanceMonitor.MonitoringLevel.Detailed;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestFileGenerator.CleanupTestFiles();
            performanceMonitor?.Dispose();
            streamingProcessor?.Dispose();
        }

        [Test]
        [TestCase("SmallFile1KB", 1024, 100)] // Should load in <100ms
        [TestCase("SmallFile10KB", 10240, 150)]
        [TestCase("SmallFile100KB", 102400, 300)]
        public async Task FileLoading_SmallFiles_ShouldLoadWithinPerformanceThresholds(string fileProperty, long expectedSize, int maxLoadTimeMs)
        {
            // Arrange
            var filePath = GetTestFilePath(fileProperty);
            var stopwatch = Stopwatch.StartNew();

            // Act
            performanceMonitor!.StartFileOperation("load", filePath, expectedSize);
            var content = await fileService!.LoadFileAsync(filePath);
            stopwatch.Stop();
            performanceMonitor.EndFileOperation("load", filePath, true);

            // Assert
            content.Should().NotBeNull();
            content.Should().NotBeEmpty();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxLoadTimeMs,
                $"Small file {fileProperty} should load within {maxLoadTimeMs}ms, but took {stopwatch.ElapsedMilliseconds}ms");

            // Verify file was loaded correctly
            var fileInfo = new FileInfo(filePath);
            var actualSize = content.Length * sizeof(char); // Approximate size check
            actualSize.Should().BeGreaterThan(0);
        }

        [Test]
        [TestCase("MediumFile1MB", 1024 * 1024, 1000)] // Should load in <1s
        [TestCase("MediumFile10MB", 10 * 1024 * 1024, 5000)] // Should load in <5s
        public async Task FileLoading_MediumFiles_ShouldLoadWithinPerformanceThresholds(string fileProperty, long expectedSize, int maxLoadTimeMs)
        {
            // Arrange
            var filePath = GetTestFilePath(fileProperty);
            var stopwatch = Stopwatch.StartNew();

            // Act
            performanceMonitor!.StartFileOperation("load", filePath, expectedSize);
            var content = await fileService!.LoadFileAsync(filePath);
            stopwatch.Stop();
            performanceMonitor.EndFileOperation("load", filePath, true);

            // Assert
            content.Should().NotBeNull();
            content.Should().NotBeEmpty();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxLoadTimeMs,
                $"Medium file {fileProperty} should load within {maxLoadTimeMs}ms, but took {stopwatch.ElapsedMilliseconds}ms");

            // Calculate throughput
            var throughputMBps = (expectedSize / 1024.0 / 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0);
            throughputMBps.Should().BeGreaterThan(1.0, "Throughput should be at least 1 MB/s");

            TestContext.WriteLine($"Loaded {expectedSize / 1024 / 1024}MB in {stopwatch.ElapsedMilliseconds}ms (Throughput: {throughputMBps:F2} MB/s)");
        }

        [Test]
        [TestCase("LargeFile50MB", 50L * 1024 * 1024, 30000)] // Should load in <30s
        [TestCase("LargeFile100MB", 100L * 1024 * 1024, 60000)] // Should load in <60s
        public async Task FileLoading_LargeFiles_ShouldLoadWithinPerformanceThresholds(string fileProperty, long expectedSize, int maxLoadTimeMs)
        {
            // Arrange
            var filePath = GetTestFilePath(fileProperty);
            var stopwatch = Stopwatch.StartNew();
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            performanceMonitor!.StartFileOperation("load", filePath, expectedSize);
            var content = await fileService!.LoadFileAsync(filePath);
            stopwatch.Stop();
            performanceMonitor.EndFileOperation("load", filePath, true);

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;

            // Assert
            content.Should().NotBeNull();
            content.Should().NotBeEmpty();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxLoadTimeMs,
                $"Large file {fileProperty} should load within {maxLoadTimeMs}ms, but took {stopwatch.ElapsedMilliseconds}ms");

            // Memory usage should be reasonable (less than 2x file size)
            memoryUsed.Should().BeLessThan(expectedSize * 2,
                $"Memory usage ({memoryUsed / 1024 / 1024}MB) should be less than 2x file size ({expectedSize * 2 / 1024 / 1024}MB)");

            // Calculate throughput
            var throughputMBps = (expectedSize / 1024.0 / 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0);
            throughputMBps.Should().BeGreaterThan(0.5, "Throughput should be at least 0.5 MB/s for large files");

            TestContext.WriteLine($"Loaded {expectedSize / 1024 / 1024}MB in {stopwatch.ElapsedMilliseconds}ms (Throughput: {throughputMBps:F2} MB/s, Memory: {memoryUsed / 1024 / 1024}MB)");
        }

        [Test]
        public async Task StreamingFileLoading_LargeFile_ShouldUseStreamingMode()
        {
            // Arrange
            var filePath = testFiles!.LargeFile50MB;
            var stopwatch = Stopwatch.StartNew();
            var segmentsLoaded = 0;

            // Act
            var isLargeFile = streamingProcessor!.IsLargeFile(filePath);
            var fileInfo = await streamingProcessor.AnalyzeFileAsync(filePath);

            await foreach (var segment in streamingProcessor.StreamFileAsync(filePath))
            {
                segmentsLoaded++;
                segment.Should().NotBeNull();
                segment.Content.Should().NotBeEmpty();
                
                // Process first 10 segments for performance test
                if (segmentsLoaded >= 10) break;
            }
            
            stopwatch.Stop();

            // Assert
            isLargeFile.Should().BeTrue("File should be detected as large");
            fileInfo.RequiresStreaming.Should().BeTrue("File should require streaming");
            segmentsLoaded.Should().BeGreaterThan(0, "Should load at least one segment");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Streaming should start within 5 seconds");

            TestContext.WriteLine($"Streamed {segmentsLoaded} segments in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        public async Task StartupTime_ColdStart_ShouldBeUnder2Seconds()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            // Act - Simulate application startup
            var monitor = new PerformanceMonitor();
            var fileService = new FileService();
            var streamingProcessor = new StreamingTextProcessor();

            // Load a medium file to simulate typical startup
            var content = await fileService.LoadFileAsync(testFiles!.MediumFile1MB);
            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000,
                $"Application startup with file load should complete in <2 seconds, but took {stopwatch.ElapsedMilliseconds}ms");

            content.Should().NotBeEmpty();

            // Cleanup
            monitor.Dispose();
            streamingProcessor.Dispose();

            TestContext.WriteLine($"Startup with 1MB file load completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        public async Task MemoryUsage_LargeFileLoad_ShouldStayUnder200MB()
        {
            // Arrange
            var filePath = testFiles!.LargeFile50MB;
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            var content = await fileService!.LoadFileAsync(filePath);
            var peakMemory = GC.GetTotalMemory(false);
            var memoryUsed = peakMemory - initialMemory;

            // Force garbage collection to get accurate reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var afterGCMemory = GC.GetTotalMemory(false);
            var retainedMemory = afterGCMemory - initialMemory;

            // Assert
            content.Should().NotBeEmpty();
            memoryUsed.Should().BeLessThan(200 * 1024 * 1024, // 200MB
                $"Peak memory usage ({memoryUsed / 1024 / 1024}MB) should be less than 200MB");

            retainedMemory.Should().BeLessThan(100 * 1024 * 1024, // 100MB
                $"Retained memory after GC ({retainedMemory / 1024 / 1024}MB) should be less than 100MB");

            TestContext.WriteLine($"Memory usage: Peak={memoryUsed / 1024 / 1024}MB, Retained={retainedMemory / 1024 / 1024}MB");
        }

        [Test]
        [Timeout(120000)] // 2 minutes timeout
        public async Task UIResponsiveness_FileLoadingWithProgress_ShouldNotBlockUI()
        {
            // Arrange
            var filePath = testFiles!.LargeFile50MB;
            var uiBlockingDetected = false;
            var maxBlockingTime = TimeSpan.Zero;
            var progressUpdates = 0;

            // Monitor for UI thread blocking
            var timer = new System.Timers.Timer(50); // Check every 50ms
            var lastCheck = DateTime.Now;
            timer.Elapsed += (s, e) =>
            {
                var now = DateTime.Now;
                var timeSinceLastCheck = now - lastCheck;
                if (timeSinceLastCheck > TimeSpan.FromMilliseconds(200)) // >200ms indicates blocking
                {
                    uiBlockingDetected = true;
                    if (timeSinceLastCheck > maxBlockingTime)
                        maxBlockingTime = timeSinceLastCheck;
                }
                lastCheck = now;
            };

            timer.Start();

            try
            {
                // Act - Load file and monitor progress
                performanceMonitor!.StartFileOperation("load", filePath, 50 * 1024 * 1024);
                
                // Simulate progress monitoring
                var loadTask = Task.Run(async () => await fileService!.LoadFileAsync(filePath));
                
                while (!loadTask.IsCompleted)
                {
                    progressUpdates++;
                    await Task.Delay(100); // Simulate UI update cycle
                }
                
                var content = await loadTask;
                performanceMonitor.EndFileOperation("load", filePath, true);

                // Assert
                content.Should().NotBeEmpty();
                uiBlockingDetected.Should().BeFalse($"UI thread was blocked for {maxBlockingTime.TotalMilliseconds}ms");
                progressUpdates.Should().BeGreaterThan(5, "Should have multiple progress updates");

                TestContext.WriteLine($"File loaded with {progressUpdates} progress updates. Max blocking time: {maxBlockingTime.TotalMilliseconds}ms");
            }
            finally
            {
                timer.Stop();
                timer.Dispose();
            }
        }

        private string GetTestFilePath(string propertyName)
        {
            var property = typeof(TestFileSet).GetProperty(propertyName);
            return property?.GetValue(testFiles) as string ?? throw new ArgumentException($"Invalid test file property: {propertyName}");
        }
    }
}