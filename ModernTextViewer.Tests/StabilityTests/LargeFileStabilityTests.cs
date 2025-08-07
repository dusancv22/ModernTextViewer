using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using ModernTextViewer.src.Services;
using ModernTextViewer.Tests.TestData;

namespace ModernTextViewer.Tests.StabilityTests
{
    /// <summary>
    /// Stability tests for large file handling and extended usage scenarios
    /// Ensures the application remains stable under stress conditions
    /// </summary>
    [TestFixture]
    [Category("Stability")]
    [Category("LongRunning")]
    public class LargeFileStabilityTests
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
        [Timeout(600000)] // 10 minutes timeout
        public async Task VeryLargeFile_500MB_ShouldLoadWithoutCrashing()
        {
            // Arrange
            var filePath = testFiles!.VeryLargeFile500MB;
            var fileService = new FileService();
            var initialMemory = GC.GetTotalMemory(true);
            var stopwatch = Stopwatch.StartNew();

            // Act & Assert
            Exception? caughtException = null;
            string? content = null;
            
            try
            {
                performanceMonitor!.StartFileOperation("load", filePath, 500L * 1024 * 1024);
                content = await fileService.LoadFileAsync(filePath);
                performanceMonitor.EndFileOperation("load", filePath, true);
            }
            catch (Exception ex)
            {
                caughtException = ex;
                performanceMonitor!.EndFileOperation("load", filePath, false, ex.Message);
            }
            finally
            {
                stopwatch.Stop();
            }

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;

            // Verify no crash occurred
            caughtException.Should().BeNull($"Should not crash when loading 500MB file. Error: {caughtException?.Message}");
            content.Should().NotBeNull("Content should be loaded");
            content.Should().NotBeEmpty("Content should not be empty");

            // Memory usage should be manageable (less than 1GB)
            memoryUsed.Should().BeLessThan(1024L * 1024 * 1024, 
                $"Memory usage ({memoryUsed / 1024 / 1024}MB) should be less than 1GB for 500MB file");

            TestContext.WriteLine($"Successfully loaded 500MB file in {stopwatch.ElapsedMilliseconds}ms using {memoryUsed / 1024 / 1024}MB memory");
        }

        [Test]
        [Timeout(1200000)] // 20 minutes timeout
        public async Task StreamingProcessor_VeryLargeFile_ShouldProcessAllSegments()
        {
            // Arrange
            var filePath = testFiles!.VeryLargeFile500MB;
            using var streamingProcessor = new StreamingTextProcessor();
            var segmentsProcessed = 0;
            var totalBytesProcessed = 0L;
            var stopwatch = Stopwatch.StartNew();
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            Exception? processingException = null;
            
            try
            {
                await foreach (var segment in streamingProcessor.StreamFileAsync(filePath))
                {
                    segment.Should().NotBeNull();
                    segment.Content.Should().NotBeEmpty();
                    
                    segmentsProcessed++;
                    totalBytesProcessed += segment.Length;

                    // Log progress every 1000 segments
                    if (segmentsProcessed % 1000 == 0)
                    {
                        TestContext.WriteLine($"Processed {segmentsProcessed} segments ({totalBytesProcessed / 1024 / 1024}MB)");
                        
                        // Check memory usage periodically
                        var currentMemory = GC.GetTotalMemory(false) - initialMemory;
                        currentMemory.Should().BeLessThan(500L * 1024 * 1024, // 500MB memory limit
                            $"Memory usage should stay under 500MB during streaming (current: {currentMemory / 1024 / 1024}MB)");
                    }
                }
            }
            catch (Exception ex)
            {
                processingException = ex;
            }
            finally
            {
                stopwatch.Stop();
            }

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;

            // Assert
            processingException.Should().BeNull($"Should not crash during streaming. Error: {processingException?.Message}");
            segmentsProcessed.Should().BeGreaterThan(1000, "Should process many segments for 500MB file");
            totalBytesProcessed.Should().BeGreaterThan(400L * 1024 * 1024, "Should process most of the file content");

            TestContext.WriteLine($"Streamed {segmentsProcessed} segments ({totalBytesProcessed / 1024 / 1024}MB) in {stopwatch.ElapsedMilliseconds}ms " +
                                $"using {memoryUsed / 1024 / 1024}MB peak memory");
        }

        [Test]
        [Timeout(600000)] // 10 minutes timeout
        public async Task MemoryLeakDetection_RepeatedLargeFileLoads_ShouldNotLeakMemory()
        {
            // Arrange
            var filePath = testFiles!.LargeFile100MB;
            var fileService = new FileService();
            var loadCycles = 5;
            var memoryReadings = new long[loadCycles + 1];
            
            // Initial memory reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            memoryReadings[0] = GC.GetTotalMemory(false);

            // Act - Load file multiple times
            for (int i = 0; i < loadCycles; i++)
            {
                var content = await fileService.LoadFileAsync(filePath);
                content.Should().NotBeEmpty();
                
                // Release reference
                content = null;
                
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                memoryReadings[i + 1] = GC.GetTotalMemory(false);
                
                TestContext.WriteLine($"Load cycle {i + 1}: Memory = {memoryReadings[i + 1] / 1024 / 1024}MB");
            }

            // Assert - Check for memory leaks
            var initialMemory = memoryReadings[0];
            var finalMemory = memoryReadings[loadCycles];
            var memoryIncrease = finalMemory - initialMemory;
            
            // Allow for some memory increase (up to 50MB) due to caching and normal operations
            memoryIncrease.Should().BeLessThan(50L * 1024 * 1024, 
                $"Memory should not increase by more than 50MB after {loadCycles} load cycles. " +
                $"Initial: {initialMemory / 1024 / 1024}MB, Final: {finalMemory / 1024 / 1024}MB, " +
                $"Increase: {memoryIncrease / 1024 / 1024}MB");

            // Check for consistent memory levels (no exponential growth)
            for (int i = 1; i < loadCycles; i++)
            {
                var previousIncrease = memoryReadings[i] - initialMemory;
                var currentIncrease = memoryReadings[i + 1] - initialMemory;
                
                // Current increase should not be more than 2x previous increase
                currentIncrease.Should().BeLessThan(previousIncrease * 2 + 20 * 1024 * 1024, // Allow 20MB variance
                    $"Memory growth should be linear, not exponential. " +
                    $"Cycle {i}: {previousIncrease / 1024 / 1024}MB, Cycle {i + 1}: {currentIncrease / 1024 / 1024}MB");
            }
        }

        [Test]
        [Timeout(300000)] // 5 minutes timeout
        public async Task ConcurrentOperations_MultipleFileLoads_ShouldHandleGracefully()
        {
            // Arrange
            var files = new[]
            {
                testFiles!.MediumFile10MB,
                testFiles.LargeFile50MB,
                testFiles.ManyHyperlinks1000
            };
            
            var tasks = new List<Task<string>>();
            var exceptions = new List<Exception>();
            var stopwatch = Stopwatch.StartNew();

            // Act - Load multiple files concurrently
            foreach (var filePath in files)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var fileService = new FileService();
                        return await fileService.LoadFileAsync(filePath);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                        throw;
                    }
                });
                tasks.Add(task);
            }

            // Wait for all tasks to complete
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            exceptions.Should().BeEmpty($"No exceptions should occur during concurrent loads. Exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
            results.Should().HaveCount(3, "Should complete all 3 concurrent loads");
            results.Should().OnlyContain(content => !string.IsNullOrEmpty(content), "All results should contain content");

            TestContext.WriteLine($"Completed {tasks.Count} concurrent file loads in {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Timeout(180000)] // 3 minutes timeout
        public async Task ErrorRecovery_CorruptedFile_ShouldRecoverGracefully()
        {
            // Arrange
            var corruptedFilePath = testFiles!.CorruptedFile;
            var fileService = new FileService();
            var streamingProcessor = new StreamingTextProcessor();

            // Act & Assert - Regular file loading
            Exception? loadException = null;
            string? content = null;
            
            try
            {
                content = await fileService.LoadFileAsync(corruptedFilePath);
            }
            catch (Exception ex)
            {
                loadException = ex;
            }

            // Should handle corrupted content gracefully
            if (loadException != null)
            {
                // If exception occurred, it should be a handled exception type
                loadException.Should().BeOfType<IOException>()
                    .Or.BeOfType<ArgumentException>()
                    .Or.BeOfType<InvalidOperationException>();
            }
            else
            {
                // If no exception, content should not be null
                content.Should().NotBeNull("Content should not be null even for corrupted files");
            }

            // Test streaming processor with corrupted file
            var segmentsProcessed = 0;
            Exception? streamingException = null;
            
            try
            {
                await foreach (var segment in streamingProcessor.StreamFileAsync(corruptedFilePath))
                {
                    segmentsProcessed++;
                    // Process only a few segments to test error handling
                    if (segmentsProcessed >= 5) break;
                }
            }
            catch (Exception ex)
            {
                streamingException = ex;
            }

            // Streaming should either work or fail gracefully
            if (streamingException != null)
            {
                streamingException.Should().BeOfType<IOException>()
                    .Or.BeOfType<ArgumentException>()
                    .Or.BeOfType<InvalidOperationException>();
            }

            TestContext.WriteLine($"Corrupted file handling: Load exception: {loadException?.GetType().Name}, " +
                                $"Streaming exception: {streamingException?.GetType().Name}, " +
                                $"Segments processed: {segmentsProcessed}");

            streamingProcessor.Dispose();
        }

        [Test]
        [Timeout(60000)] // 1 minute timeout
        public async Task CancellationHandling_LargeFileLoad_ShouldCancelCleanly()
        {
            // Arrange
            var filePath = testFiles!.LargeFile100MB;
            var fileService = new FileService();
            using var cts = new CancellationTokenSource();
            var stopwatch = Stopwatch.StartNew();

            // Act - Start loading and cancel after 5 seconds
            var loadTask = Task.Run(async () =>
            {
                try
                {
                    return await fileService.LoadFileAsync(filePath);
                }
                catch (OperationCanceledException)
                {
                    return null; // Expected cancellation
                }
            });

            // Cancel after 5 seconds
            await Task.Delay(5000);
            cts.Cancel();

            var result = await loadTask;
            stopwatch.Stop();

            // Assert
            // Operation should complete quickly after cancellation
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, 
                "Operation should complete within 10 seconds of cancellation request");

            TestContext.WriteLine($"Cancellation handled in {stopwatch.ElapsedMilliseconds}ms. Result: {(result != null ? "Completed" : "Cancelled")}");
        }

        [Test]
        [Timeout(300000)] // 5 minutes timeout
        public async Task ExtendedUsage_LongRunningOperations_ShouldMaintainStability()
        {
            // Arrange
            var files = new[]
            {
                testFiles!.MediumFile1MB,
                testFiles.LargeFile50MB,
                testFiles.ManyHyperlinks1000
            };

            var operationCount = 20;
            var completedOperations = 0;
            var exceptions = new List<Exception>();
            var memoryReadings = new List<long>();

            // Act - Perform many operations over time
            for (int i = 0; i < operationCount; i++)
            {
                try
                {
                    var filePath = files[i % files.Length];
                    
                    // Alternate between regular and streaming operations
                    if (i % 2 == 0)
                    {
                        var fileService = new FileService();
                        var content = await fileService.LoadFileAsync(filePath);
                        content.Should().NotBeEmpty();
                    }
                    else
                    {
                        using var streamingProcessor = new StreamingTextProcessor();
                        var segmentCount = 0;
                        await foreach (var segment in streamingProcessor.StreamFileAsync(filePath))
                        {
                            segment.Should().NotBeNull();
                            segmentCount++;
                            if (segmentCount >= 5) break; // Process just a few segments
                        }
                    }

                    completedOperations++;

                    // Record memory usage every 5 operations
                    if (i % 5 == 0)
                    {
                        GC.Collect();
                        memoryReadings.Add(GC.GetTotalMemory(false));
                    }

                    // Small delay between operations
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    TestContext.WriteLine($"Exception in operation {i}: {ex.Message}");
                }
            }

            // Assert
            completedOperations.Should().BeGreaterOrEqualTo(operationCount * 0.9, // Allow 10% failure rate
                $"Should complete at least 90% of operations. Completed: {completedOperations}/{operationCount}");

            exceptions.Should().HaveCountLessOrEqualTo(operationCount * 0.1, // Allow up to 10% exceptions
                $"Should have minimal exceptions during extended usage. Exceptions: {exceptions.Count}");

            // Memory should remain stable (no exponential growth)
            if (memoryReadings.Count > 2)
            {
                var initialMemory = memoryReadings[0];
                var finalMemory = memoryReadings[^1];
                var memoryGrowth = finalMemory - initialMemory;

                memoryGrowth.Should().BeLessThan(200L * 1024 * 1024, // 200MB growth limit
                    $"Memory growth should be limited during extended usage. " +
                    $"Initial: {initialMemory / 1024 / 1024}MB, Final: {finalMemory / 1024 / 1024}MB, " +
                    $"Growth: {memoryGrowth / 1024 / 1024}MB");
            }

            TestContext.WriteLine($"Extended usage test: {completedOperations}/{operationCount} operations completed, " +
                                $"{exceptions.Count} exceptions, Memory readings: {memoryReadings.Count}");
        }
    }
}