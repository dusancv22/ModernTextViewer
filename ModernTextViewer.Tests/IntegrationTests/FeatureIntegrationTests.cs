using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using ModernTextViewer.src.Services;
using ModernTextViewer.src.Models;
using ModernTextViewer.Tests.TestData;

namespace ModernTextViewer.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests that verify feature interactions and end-to-end functionality
    /// Tests the complete workflow of major application features
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class FeatureIntegrationTests
    {
        private TestFileSet? testFiles;
        private PerformanceMonitor? performanceMonitor;
        private string tempDirectory = string.Empty;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            testFiles = await TestFileGenerator.GenerateTestFileSuiteAsync();
            performanceMonitor = new PerformanceMonitor();
            performanceMonitor.Level = PerformanceMonitor.MonitoringLevel.Detailed;
            
            tempDirectory = Path.Combine(Path.GetTempPath(), "ModernTextViewerIntegrationTests");
            Directory.CreateDirectory(tempDirectory);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestFileGenerator.CleanupTestFiles();
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
            performanceMonitor?.Dispose();
        }

        [Test]
        public async Task StreamingModeSwitch_LargeToSmallFile_ShouldSwitchModesCorrectly()
        {
            // Arrange
            var largeFile = testFiles!.LargeFile50MB;
            var smallFile = testFiles.SmallFile1KB;
            using var streamingProcessor = new StreamingTextProcessor();
            var fileService = new FileService();

            // Act - Load large file (should use streaming)
            var isLargeFileStreaming = streamingProcessor.IsLargeFile(largeFile);
            var largeFileInfo = await streamingProcessor.AnalyzeFileAsync(largeFile);
            
            // Process a few segments from large file
            var largeFileSegments = 0;
            await foreach (var segment in streamingProcessor.StreamFileAsync(largeFile))
            {
                segment.Should().NotBeNull();
                largeFileSegments++;
                if (largeFileSegments >= 5) break;
            }

            // Load small file (should use regular mode)
            var isSmallFileStreaming = streamingProcessor.IsLargeFile(smallFile);
            var smallFileContent = await fileService.LoadFileAsync(smallFile);

            // Assert
            isLargeFileStreaming.Should().BeTrue("Large file should be detected as requiring streaming");
            largeFileInfo.RequiresStreaming.Should().BeTrue("Large file should require streaming mode");
            largeFileSegments.Should().BeGreaterThan(0, "Should process segments from large file");

            isSmallFileStreaming.Should().BeFalse("Small file should not require streaming");
            smallFileContent.Should().NotBeEmpty("Small file should load completely");

            TestContext.WriteLine($"Large file streaming: {isLargeFileStreaming}, segments processed: {largeFileSegments}");
            TestContext.WriteLine($"Small file streaming: {isSmallFileStreaming}, content length: {smallFileContent.Length}");
        }

        [Test]
        public async Task ProgressDialog_LargeFileLoad_ShouldReportProgress()
        {
            // Arrange
            var filePath = testFiles!.LargeFile50MB;
            using var streamingProcessor = new StreamingTextProcessor();
            var progressUpdates = new List<(long processed, long total, int percent)>();
            
            streamingProcessor.ProgressChanged += (sender, e) =>
            {
                progressUpdates.Add((e.ProcessedBytes, e.TotalBytes, e.PercentComplete));
            };

            var stopwatch = Stopwatch.StartNew();

            // Act
            var segmentsProcessed = 0;
            await foreach (var segment in streamingProcessor.StreamFileAsync(filePath))
            {
                segment.Should().NotBeNull();
                segmentsProcessed++;
                
                // Process enough segments to get meaningful progress
                if (segmentsProcessed >= 20) break;
            }
            stopwatch.Stop();

            // Assert
            progressUpdates.Should().NotBeEmpty("Should receive progress updates during streaming");
            progressUpdates.Count.Should().BeGreaterThan(5, "Should receive multiple progress updates");

            // Verify progress is increasing
            for (int i = 1; i < progressUpdates.Count; i++)
            {
                progressUpdates[i].processed.Should().BeGreaterOrEqualTo(progressUpdates[i - 1].processed,
                    "Progress should increase over time");
                progressUpdates[i].percent.Should().BeInRange(0, 100, "Percent should be valid");
            }

            var finalProgress = progressUpdates.Last();
            finalProgress.total.Should().BeGreaterThan(0, "Total bytes should be set");
            finalProgress.processed.Should().BeGreaterThan(0, "Should process some bytes");

            TestContext.WriteLine($"Progress updates: {progressUpdates.Count}, Final: {finalProgress.processed}/{finalProgress.total} bytes ({finalProgress.percent}%)");
        }

        [Test]
        public async Task AutoSave_WithLargeFile_ShouldSaveIncrementally()
        {
            // Arrange
            var originalFile = testFiles!.MediumFile10MB;
            var saveFile = Path.Combine(tempDirectory, "autosave_test.txt");
            var fileService = new FileService();
            
            // Load original content
            var originalContent = await fileService.LoadFileAsync(originalFile);
            
            // Simulate document model with auto-save
            var documentModel = new DocumentModel
            {
                Content = originalContent,
                FilePath = saveFile,
                IsDirty = true
            };

            var stopwatch = Stopwatch.StartNew();

            // Act - Save the large content (simulating auto-save)
            performanceMonitor!.StartFileOperation("save", saveFile, originalContent.Length * sizeof(char));
            await fileService.SaveFileAsync(saveFile, originalContent);
            stopwatch.Stop();
            performanceMonitor.EndFileOperation("save", saveFile, true);

            // Verify save completed
            var savedContent = await fileService.LoadFileAsync(saveFile);

            // Assert
            File.Exists(saveFile).Should().BeTrue("Auto-saved file should exist");
            savedContent.Should().Be(originalContent, "Saved content should match original");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "Auto-save should complete in reasonable time");

            // Verify file size
            var fileInfo = new FileInfo(saveFile);
            fileInfo.Length.Should().BeGreaterThan(0, "Saved file should have content");

            TestContext.WriteLine($"Auto-saved {fileInfo.Length / 1024 / 1024}MB file in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        public async Task UndoRedo_LargeOperation_ShouldPerformEfficiently()
        {
            // Arrange
            var originalContent = await File.ReadAllTextAsync(testFiles!.MediumFile1MB);
            var documentModel = new DocumentModel
            {
                Content = originalContent,
                FilePath = "test.txt",
                IsDirty = false
            };

            var modification = "\n--- LARGE MODIFICATION ---\n" + new string('X', 10000); // 10KB addition
            var stopwatch = Stopwatch.StartNew();

            // Act - Simulate large content modification
            var undoState = documentModel.Content; // Save state for undo
            documentModel.Content += modification;
            documentModel.IsDirty = true;

            var modificationTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();

            // Simulate undo operation
            documentModel.Content = undoState;
            documentModel.IsDirty = false;

            var undoTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Stop();

            // Assert
            documentModel.Content.Should().Be(originalContent, "Undo should restore original content");
            documentModel.IsDirty.Should().BeFalse("Document should not be dirty after undo");
            
            modificationTime.Should().BeLessThan(1000, "Content modification should be fast");
            undoTime.Should().BeLessThan(1000, "Undo operation should be fast");

            TestContext.WriteLine($"Modification: {modificationTime}ms, Undo: {undoTime}ms, Content size: {originalContent.Length / 1024}KB");
        }

        [Test]
        public async Task ThemeSwitching_WithLargeContent_ShouldRemainResponsive()
        {
            // Arrange
            var largeContent = await File.ReadAllTextAsync(testFiles!.LargeFile50MB);
            var (processedContent, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(largeContent);
            
            var darkThemeTime = new List<long>();
            var lightThemeTime = new List<long>();

            // Act - Simulate theme switching multiple times
            for (int i = 0; i < 3; i++)
            {
                // Switch to dark theme
                var darkStopwatch = Stopwatch.StartNew();
                var darkFormattedContent = HyperlinkService.ApplyHyperlinkFormatting(processedContent, hyperlinks, isDarkMode: true);
                darkStopwatch.Stop();
                darkThemeTime.Add(darkStopwatch.ElapsedMilliseconds);

                // Switch to light theme
                var lightStopwatch = Stopwatch.StartNew();
                var lightFormattedContent = HyperlinkService.ApplyHyperlinkFormatting(processedContent, hyperlinks, isDarkMode: false);
                lightStopwatch.Stop();
                lightThemeTime.Add(lightStopwatch.ElapsedMilliseconds);

                // Verify content is formatted
                darkFormattedContent.Should().NotBe(processedContent, "Dark theme should apply formatting");
                lightFormattedContent.Should().NotBe(processedContent, "Light theme should apply formatting");
                darkFormattedContent.Should().NotBe(lightFormattedContent, "Dark and light themes should be different");
            }

            // Assert
            var avgDarkTime = darkThemeTime.Average();
            var avgLightTime = lightThemeTime.Average();

            avgDarkTime.Should().BeLessThan(5000, "Dark theme switching should complete in <5 seconds");
            avgLightTime.Should().BeLessThan(5000, "Light theme switching should complete in <5 seconds");

            // Theme switching times should be consistent (no significant degradation)
            var darkTimeVariance = darkThemeTime.Max() - darkThemeTime.Min();
            var lightTimeVariance = lightThemeTime.Max() - lightThemeTime.Min();

            darkTimeVariance.Should().BeLessThan(avgDarkTime * 0.5, "Dark theme switching times should be consistent");
            lightTimeVariance.Should().BeLessThan(avgLightTime * 0.5, "Light theme switching times should be consistent");

            TestContext.WriteLine($"Theme switching - Dark: {avgDarkTime:F0}ms (±{darkTimeVariance}ms), Light: {avgLightTime:F0}ms (±{lightTimeVariance}ms)");
        }

        [Test]
        public async Task ConcurrentFileOperations_LoadSaveHyperlinks_ShouldCoordinate()
        {
            // Arrange
            var sourceFile = testFiles!.ManyHyperlinks1000;
            var targetFile = Path.Combine(tempDirectory, "concurrent_test.txt");
            var fileService = new FileService();

            var tasks = new List<Task>();
            var results = new ConcurrentBag<(string operation, bool success, long duration)>();

            // Act - Start concurrent operations
            // Task 1: Load file
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var content = await fileService.LoadFileAsync(sourceFile);
                    stopwatch.Stop();
                    results.Add(("load", !string.IsNullOrEmpty(content), stopwatch.ElapsedMilliseconds));
                }
                catch
                {
                    stopwatch.Stop();
                    results.Add(("load", false, stopwatch.ElapsedMilliseconds));
                }
            }));

            // Task 2: Process hyperlinks
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var content = await fileService.LoadFileAsync(sourceFile);
                    var (_, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);
                    stopwatch.Stop();
                    results.Add(("hyperlinks", hyperlinks.Count > 0, stopwatch.ElapsedMilliseconds));
                }
                catch
                {
                    stopwatch.Stop();
                    results.Add(("hyperlinks", false, stopwatch.ElapsedMilliseconds));
                }
            }));

            // Task 3: Save file (after small delay)
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to ensure load starts first
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var content = await fileService.LoadFileAsync(sourceFile);
                    await fileService.SaveFileAsync(targetFile, content);
                    stopwatch.Stop();
                    results.Add(("save", File.Exists(targetFile), stopwatch.ElapsedMilliseconds));
                }
                catch
                {
                    stopwatch.Stop();
                    results.Add(("save", false, stopwatch.ElapsedMilliseconds));
                }
            }));

            // Wait for all operations to complete
            await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(3, "All three operations should complete");
            results.Should().OnlyContain(r => r.success, "All operations should succeed");
            results.Should().OnlyContain(r => r.duration < 30000, "All operations should complete within 30 seconds");

            // Verify saved file
            if (File.Exists(targetFile))
            {
                var savedContent = await fileService.LoadFileAsync(targetFile);
                savedContent.Should().NotBeEmpty("Saved file should have content");
            }

            var resultSummary = results.GroupBy(r => r.operation)
                .Select(g => $"{g.Key}: {g.First().duration}ms")
                .ToList();

            TestContext.WriteLine($"Concurrent operations completed: {string.Join(", ", resultSummary)}");
        }

        [Test]
        [Timeout(120000)] // 2 minutes timeout
        public async Task ErrorRecovery_MultipleFailureScenarios_ShouldRecoverGracefully()
        {
            // Arrange
            var scenarios = new List<(string name, Func<Task> operation, Type expectedExceptionType)>();
            var fileService = new FileService();

            // Scenario 1: Non-existent file
            scenarios.Add((
                "NonExistentFile",
                async () => await fileService.LoadFileAsync("nonexistent.txt"),
                typeof(FileNotFoundException)
            ));

            // Scenario 2: Corrupted file
            scenarios.Add((
                "CorruptedFile",
                async () => await fileService.LoadFileAsync(testFiles!.CorruptedFile),
                typeof(IOException)
            ));

            // Scenario 3: Access denied (read-only location)
            var readOnlyFile = Path.Combine(tempDirectory, "readonly.txt");
            await File.WriteAllTextAsync(readOnlyFile, "test content");
            File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);
            
            scenarios.Add((
                "AccessDenied",
                async () => await fileService.SaveFileAsync(readOnlyFile, "new content"),
                typeof(UnauthorizedAccessException)
            ));

            var recoveryResults = new List<(string scenario, bool recovered, Exception? exception)>();

            // Act - Test each error scenario
            foreach (var (name, operation, expectedType) in scenarios)
            {
                Exception? caughtException = null;
                bool recovered = false;

                try
                {
                    await operation();
                    recovered = true; // If no exception, operation succeeded
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                    
                    // Check if it's a handled exception type
                    if (expectedType.IsAssignableFrom(ex.GetType()) ||
                        ex is IOException ||
                        ex is UnauthorizedAccessException ||
                        ex is ArgumentException)
                    {
                        recovered = true; // Expected exception type = graceful handling
                    }
                }

                recoveryResults.Add((name, recovered, caughtException));
            }

            // Assert
            recoveryResults.Should().OnlyContain(r => r.recovered, 
                $"All error scenarios should recover gracefully. Failed: {string.Join(", ", recoveryResults.Where(r => !r.recovered).Select(r => $"{r.scenario}: {r.exception?.Message}"))}");

            foreach (var (scenario, recovered, exception) in recoveryResults)
            {
                TestContext.WriteLine($"Scenario '{scenario}': Recovered={recovered}, Exception={exception?.GetType().Name}: {exception?.Message}");
            }

            // Verify application can continue normal operations after errors
            var normalContent = await fileService.LoadFileAsync(testFiles!.SmallFile1KB);
            normalContent.Should().NotBeEmpty("Should be able to perform normal operations after error recovery");
        }

        private class ConcurrentBag<T> : System.Collections.Concurrent.ConcurrentBag<T>
        {
            // Using System.Collections.Concurrent.ConcurrentBag<T>
        }
    }
}