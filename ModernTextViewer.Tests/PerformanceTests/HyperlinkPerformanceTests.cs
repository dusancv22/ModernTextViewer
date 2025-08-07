using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using ModernTextViewer.src.Services;
using ModernTextViewer.Tests.TestData;

namespace ModernTextViewer.Tests.PerformanceTests
{
    /// <summary>
    /// Performance tests for hyperlink processing operations
    /// Tests various hyperlink densities and processing performance
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class HyperlinkPerformanceTests
    {
        private TestFileSet? testFiles;
        private PerformanceMonitor? performanceMonitor;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            testFiles = await TestFileGenerator.GenerateTestFileSuiteAsync();
            performanceMonitor = new PerformanceMonitor();
            performanceMonitor.Level = PerformanceMonitor.MonitoringLevel.Detailed;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestFileGenerator.CleanupTestFiles();
            performanceMonitor?.Dispose();
        }

        [Test]
        [TestCase("ManyHyperlinks1000", 1000, 5000)] // Should process in <5s
        [TestCase("ManyHyperlinks10000", 10000, 30000)] // Should process in <30s
        public async Task HyperlinkProcessing_HighDensity_ShouldProcessWithinThresholds(string fileProperty, int expectedHyperlinks, int maxProcessingTimeMs)
        {
            // Arrange
            var filePath = GetTestFilePath(fileProperty);
            var fileService = new FileService();
            var stopwatch = Stopwatch.StartNew();
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            performanceMonitor!.StartFileOperation("hyperlink_processing", filePath);
            var content = await fileService.LoadFileAsync(filePath);
            
            var hyperlinkStopwatch = Stopwatch.StartNew();
            var (processedContent, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);
            hyperlinkStopwatch.Stop();
            
            stopwatch.Stop();
            performanceMonitor.EndFileOperation("hyperlink_processing", filePath, true);

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;

            // Assert
            hyperlinks.Should().NotBeNull();
            hyperlinks.Count.Should().BeGreaterOrEqualTo(expectedHyperlinks * 0.8, // Allow 20% variance
                $"Should find approximately {expectedHyperlinks} hyperlinks");
            
            hyperlinkStopwatch.ElapsedMilliseconds.Should().BeLessThan(maxProcessingTimeMs,
                $"Hyperlink processing should complete within {maxProcessingTimeMs}ms, but took {hyperlinkStopwatch.ElapsedMilliseconds}ms");

            // Memory usage should be reasonable
            memoryUsed.Should().BeLessThan(100 * 1024 * 1024, // 100MB
                $"Memory usage for hyperlink processing ({memoryUsed / 1024 / 1024}MB) should be reasonable");

            // Calculate processing rate
            var hyperlinksPerSecond = hyperlinks.Count / (hyperlinkStopwatch.ElapsedMilliseconds / 1000.0);
            hyperlinksPerSecond.Should().BeGreaterThan(100, "Should process at least 100 hyperlinks per second");

            TestContext.WriteLine($"Processed {hyperlinks.Count} hyperlinks in {hyperlinkStopwatch.ElapsedMilliseconds}ms " +
                                $"(Rate: {hyperlinksPerSecond:F0} links/sec, Memory: {memoryUsed / 1024 / 1024}MB)");
        }

        [Test]
        public async Task HyperlinkProcessing_StreamingMode_ShouldNotBlockUI()
        {
            // Arrange
            var filePath = testFiles!.ManyHyperlinks10000;
            var streamingProcessor = new StreamingTextProcessor();
            var totalHyperlinks = 0;
            var segmentsProcessed = 0;
            var uiBlockingDetected = false;
            var maxBlockingTime = TimeSpan.Zero;

            // Monitor for UI thread blocking
            var timer = new System.Timers.Timer(50);
            var lastCheck = DateTime.Now;
            timer.Elapsed += (s, e) =>
            {
                var now = DateTime.Now;
                var timeSinceLastCheck = now - lastCheck;
                if (timeSinceLastCheck > TimeSpan.FromMilliseconds(200))
                {
                    uiBlockingDetected = true;
                    if (timeSinceLastCheck > maxBlockingTime)
                        maxBlockingTime = timeSinceLastCheck;
                }
                lastCheck = now;
            };

            timer.Start();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Act - Process file in streaming mode
                await foreach (var segment in streamingProcessor.StreamFileAsync(filePath))
                {
                    totalHyperlinks += segment.Hyperlinks.Count;
                    segmentsProcessed++;

                    // Simulate UI update after each segment
                    await Task.Delay(1);

                    // Process first 20 segments for performance test
                    if (segmentsProcessed >= 20) break;
                }
                
                stopwatch.Stop();

                // Assert
                totalHyperlinks.Should().BeGreaterThan(0, "Should find hyperlinks in streamed segments");
                segmentsProcessed.Should().Be(20, "Should process exactly 20 segments");
                uiBlockingDetected.Should().BeFalse($"UI thread was blocked for {maxBlockingTime.TotalMilliseconds}ms");
                stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "Streaming processing should be fast");

                TestContext.WriteLine($"Streamed {segmentsProcessed} segments with {totalHyperlinks} hyperlinks in {stopwatch.ElapsedMilliseconds}ms. " +
                                    $"Max blocking: {maxBlockingTime.TotalMilliseconds}ms");
            }
            finally
            {
                timer.Stop();
                timer.Dispose();
                streamingProcessor.Dispose();
            }
        }

        [Test]
        public async Task HyperlinkValidation_LargeList_ShouldValidateEfficiently()
        {
            // Arrange
            var filePath = testFiles!.ManyHyperlinks1000;
            var fileService = new FileService();
            var content = await fileService.LoadFileAsync(filePath);
            var (_, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);

            var stopwatch = Stopwatch.StartNew();
            var validatedCount = 0;

            // Act - Validate each hyperlink
            foreach (var hyperlink in hyperlinks.Take(100)) // Test first 100 for performance
            {
                var isValid = HyperlinkService.ValidateHyperlink(hyperlink.Url);
                if (isValid) validatedCount++;
            }
            
            stopwatch.Stop();

            // Assert
            validatedCount.Should().BeGreaterThan(80, "Most hyperlinks should be valid"); // 80% should be valid
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Hyperlink validation should be fast");

            var validationRate = 100 / (stopwatch.ElapsedMilliseconds / 1000.0);
            validationRate.Should().BeGreaterThan(50, "Should validate at least 50 hyperlinks per second");

            TestContext.WriteLine($"Validated 100 hyperlinks in {stopwatch.ElapsedMilliseconds}ms " +
                                $"(Rate: {validationRate:F0} validations/sec, {validatedCount}% valid)");
        }

        [Test]
        public async Task HyperlinkCaching_RepeatedAccess_ShouldImprovePerformance()
        {
            // Arrange
            var filePath = testFiles!.ManyHyperlinks1000;
            var fileService = new FileService();
            var content = await fileService.LoadFileAsync(filePath);

            // First pass - cold cache
            var coldStopwatch = Stopwatch.StartNew();
            var (processedContent1, hyperlinks1) = HyperlinkService.ExtractHyperlinkMetadata(content);
            coldStopwatch.Stop();

            // Second pass - warm cache
            var warmStopwatch = Stopwatch.StartNew();
            var (processedContent2, hyperlinks2) = HyperlinkService.ExtractHyperlinkMetadata(content);
            warmStopwatch.Stop();

            // Assert
            hyperlinks1.Count.Should().Be(hyperlinks2.Count, "Should find same number of hyperlinks");
            warmStopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(coldStopwatch.ElapsedMilliseconds,
                "Warm cache should be equal or faster than cold cache");

            var improvementRatio = (double)coldStopwatch.ElapsedMilliseconds / warmStopwatch.ElapsedMilliseconds;
            
            TestContext.WriteLine($"Cold cache: {coldStopwatch.ElapsedMilliseconds}ms, Warm cache: {warmStopwatch.ElapsedMilliseconds}ms " +
                                $"(Improvement ratio: {improvementRatio:F2}x)");
        }

        [Test]
        public async Task HyperlinkHighlighting_LargeContent_ShouldRenderEfficiently()
        {
            // Arrange
            var filePath = testFiles!.ManyHyperlinks1000;
            var fileService = new FileService();
            var content = await fileService.LoadFileAsync(filePath);
            var (processedContent, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);

            var stopwatch = Stopwatch.StartNew();

            // Act - Simulate hyperlink highlighting (processing rich text format)
            var highlightedContent = HyperlinkService.ApplyHyperlinkFormatting(processedContent, hyperlinks);
            
            stopwatch.Stop();

            // Assert
            highlightedContent.Should().NotBeNullOrEmpty();
            highlightedContent.Length.Should().BeGreaterOrEqualTo(content.Length, "Formatted content should be at least as long");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Hyperlink highlighting should complete in <5s");

            var processingRate = (content.Length / 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0);
            processingRate.Should().BeGreaterThan(100, "Should process at least 100 KB/s for highlighting");

            TestContext.WriteLine($"Highlighted {hyperlinks.Count} hyperlinks in {content.Length / 1024}KB content " +
                                $"in {stopwatch.ElapsedMilliseconds}ms (Rate: {processingRate:F0} KB/s)");
        }

        [Test]
        public async Task HyperlinkMemoryUsage_LargeHyperlinkList_ShouldBeMemoryEfficient()
        {
            // Arrange
            var filePath = testFiles!.ManyHyperlinks10000;
            var fileService = new FileService();
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            var content = await fileService.LoadFileAsync(filePath);
            var memoryAfterLoad = GC.GetTotalMemory(false);

            var (processedContent, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);
            var memoryAfterProcessing = GC.GetTotalMemory(false);

            var loadMemory = memoryAfterLoad - initialMemory;
            var processingMemory = memoryAfterProcessing - memoryAfterLoad;

            // Assert
            hyperlinks.Count.Should().BeGreaterThan(5000, "Should find a significant number of hyperlinks");
            
            // Memory per hyperlink should be reasonable (less than 1KB per hyperlink)
            var memoryPerHyperlink = processingMemory / (double)hyperlinks.Count;
            memoryPerHyperlink.Should().BeLessThan(1024, $"Memory per hyperlink ({memoryPerHyperlink:F0} bytes) should be less than 1KB");

            TestContext.WriteLine($"Processed {hyperlinks.Count} hyperlinks. " +
                                $"Load memory: {loadMemory / 1024 / 1024}MB, Processing memory: {processingMemory / 1024 / 1024}MB, " +
                                $"Memory per hyperlink: {memoryPerHyperlink:F0} bytes");
        }

        private string GetTestFilePath(string propertyName)
        {
            var property = typeof(TestFileSet).GetProperty(propertyName);
            return property?.GetValue(testFiles) as string ?? throw new ArgumentException($"Invalid test file property: {propertyName}");
        }
    }
}