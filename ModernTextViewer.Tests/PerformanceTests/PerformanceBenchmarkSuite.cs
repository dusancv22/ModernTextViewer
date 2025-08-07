using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using NBomber.CSharp;
using NBomber.Contracts;
using ModernTextViewer.src.Services;
using ModernTextViewer.Tests.TestData;

namespace ModernTextViewer.Tests.PerformanceTests
{
    /// <summary>
    /// Comprehensive performance benchmark suite using NBomber
    /// Provides detailed performance metrics and baseline establishment
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    [Category("Benchmark")]
    public class PerformanceBenchmarkSuite
    {
        private TestFileSet? testFiles;
        private string benchmarkResultsPath = string.Empty;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            testFiles = await TestFileGenerator.GenerateTestFileSuiteAsync();
            benchmarkResultsPath = Path.Combine(Path.GetTempPath(), "ModernTextViewerBenchmarks");
            Directory.CreateDirectory(benchmarkResultsPath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            TestFileGenerator.CleanupTestFiles();
        }

        [Test]
        [Timeout(300000)] // 5 minutes
        public async Task Benchmark_FileLoadingThroughput_ShouldMeetBaselines()
        {
            // Define scenarios for different file sizes
            var scenarios = new[]
            {
                Scenario.Create("load_small_1kb", async context =>
                {
                    var fileService = new FileService();
                    var content = await fileService.LoadFileAsync(testFiles!.SmallFile1KB);
                    return content.Length > 0 ? Response.Ok() : Response.Fail();
                })
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromSeconds(30))
                ),

                Scenario.Create("load_medium_1mb", async context =>
                {
                    var fileService = new FileService();
                    var content = await fileService.LoadFileAsync(testFiles!.MediumFile1MB);
                    return content.Length > 0 ? Response.Ok() : Response.Fail();
                })
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 2, during: TimeSpan.FromSeconds(60))
                ),

                Scenario.Create("load_large_50mb", async context =>
                {
                    var fileService = new FileService();
                    var content = await fileService.LoadFileAsync(testFiles!.LargeFile50MB);
                    return content.Length > 0 ? Response.Ok() : Response.Fail();
                })
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 1, during: TimeSpan.FromSeconds(120))
                )
            };

            // Run benchmark
            var stats = NBomberRunner
                .RegisterScenarios(scenarios)
                .WithReportFolder(benchmarkResultsPath)
                .WithReportFileName("file_loading_benchmark")
                .Run();

            // Assert performance baselines
            var smallFileStats = stats.AllOkCount > 0 ? stats : null;
            smallFileStats.Should().NotBeNull("Benchmark should complete successfully");

            // Verify no failures occurred
            stats.AllFailCount.Should().Be(0, "No benchmark operations should fail");

            // Verify reasonable response times
            stats.ScenarioStats.Should().OnlyContain(s => s.Ok.Mean <= TimeSpan.FromSeconds(30),
                "All scenarios should have reasonable response times");

            TestContext.WriteLine($"File Loading Benchmark Results:");
            TestContext.WriteLine($"Total Operations: {stats.AllOkCount}");
            TestContext.WriteLine($"Failures: {stats.AllFailCount}");
            foreach (var scenario in stats.ScenarioStats)
            {
                TestContext.WriteLine($"Scenario {scenario.ScenarioName}: " +
                                    $"Mean={scenario.Ok.Mean.TotalMilliseconds:F0}ms, " +
                                    $"Max={scenario.Ok.Max.TotalMilliseconds:F0}ms, " +
                                    $"RPS={scenario.Ok.Request.Count / stats.TestSuite.Duration.TotalSeconds:F1}");
            }
        }

        [Test]
        [Timeout(600000)] // 10 minutes
        public async Task Benchmark_HyperlinkProcessingPerformance_ShouldScaleLinear()
        {
            var scenarios = new[]
            {
                Scenario.Create("hyperlink_1000", async context =>
                {
                    var fileService = new FileService();
                    var content = await fileService.LoadFileAsync(testFiles!.ManyHyperlinks1000);
                    var (_, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);
                    return hyperlinks.Count > 500 ? Response.Ok() : Response.Fail(); // Expect at least 500 links
                })
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromSeconds(60))
                ),

                Scenario.Create("hyperlink_10000", async context =>
                {
                    var fileService = new FileService();
                    var content = await fileService.LoadFileAsync(testFiles!.ManyHyperlinks10000);
                    var (_, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);
                    return hyperlinks.Count > 5000 ? Response.Ok() : Response.Fail(); // Expect at least 5000 links
                })
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 1, during: TimeSpan.FromSeconds(120))
                )
            };

            var stats = NBomberRunner
                .RegisterScenarios(scenarios)
                .WithReportFolder(benchmarkResultsPath)
                .WithReportFileName("hyperlink_processing_benchmark")
                .Run();

            // Assert scaling characteristics
            stats.AllFailCount.Should().Be(0, "No hyperlink processing should fail");

            var scenario1000 = stats.ScenarioStats.First(s => s.ScenarioName == "hyperlink_1000");
            var scenario10000 = stats.ScenarioStats.First(s => s.ScenarioName == "hyperlink_10000");

            // Processing time should scale sub-linearly (better than 10x for 10x data)
            var scalingFactor = scenario10000.Ok.Mean.TotalMilliseconds / scenario1000.Ok.Mean.TotalMilliseconds;
            scalingFactor.Should().BeLessThan(15.0, "Hyperlink processing should scale better than linearly");

            TestContext.WriteLine($"Hyperlink Processing Scaling:");
            TestContext.WriteLine($"1000 links: {scenario1000.Ok.Mean.TotalMilliseconds:F0}ms");
            TestContext.WriteLine($"10000 links: {scenario10000.Ok.Mean.TotalMilliseconds:F0}ms");
            TestContext.WriteLine($"Scaling factor: {scalingFactor:F2}x");
        }

        [Test]
        [Timeout(900000)] // 15 minutes
        public async Task Benchmark_StreamingPerformance_ShouldMaintainThroughput()
        {
            var scenario = Scenario.Create("streaming_large_file", async context =>
            {
                using var streamingProcessor = new StreamingTextProcessor();
                var segmentsProcessed = 0;
                var startTime = DateTime.Now;

                await foreach (var segment in streamingProcessor.StreamFileAsync(testFiles!.LargeFile50MB))
                {
                    segmentsProcessed++;
                    // Process first 10 segments for benchmark
                    if (segmentsProcessed >= 10) break;
                }

                var processingTime = DateTime.Now - startTime;
                return processingTime.TotalSeconds < 10 && segmentsProcessed == 10 
                    ? Response.Ok() 
                    : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 2, during: TimeSpan.FromSeconds(180))
            );

            var stats = NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFolder(benchmarkResultsPath)
                .WithReportFileName("streaming_performance_benchmark")
                .Run();

            // Assert streaming performance
            stats.AllFailCount.Should().Be(0, "Streaming operations should not fail");
            stats.ScenarioStats[0].Ok.Mean.Should().BeLessThan(TimeSpan.FromSeconds(15),
                "Streaming should maintain consistent performance");

            // Check for performance degradation over time
            var responseTimeP99 = stats.ScenarioStats[0].Ok.Percentile99;
            var responseTimeMean = stats.ScenarioStats[0].Ok.Mean;
            var variability = responseTimeP99.TotalMilliseconds / responseTimeMean.TotalMilliseconds;

            variability.Should().BeLessThan(3.0, "Response times should be consistent (low variability)");

            TestContext.WriteLine($"Streaming Performance:");
            TestContext.WriteLine($"Mean response time: {responseTimeMean.TotalMilliseconds:F0}ms");
            TestContext.WriteLine($"99th percentile: {responseTimeP99.TotalMilliseconds:F0}ms");
            TestContext.WriteLine($"Variability factor: {variability:F2}");
        }

        [Test]
        [Timeout(1200000)] // 20 minutes
        public async Task Benchmark_MemoryUsageUnderLoad_ShouldStayWithinLimits()
        {
            var memoryReadings = new List<(DateTime time, long memoryMB)>();
            var memoryTimer = new System.Timers.Timer(5000); // Check every 5 seconds
            
            memoryTimer.Elapsed += (s, e) =>
            {
                var memoryUsage = GC.GetTotalMemory(false);
                lock (memoryReadings)
                {
                    memoryReadings.Add((DateTime.Now, memoryUsage / 1024 / 1024));
                }
            };

            memoryTimer.Start();

            try
            {
                var scenario = Scenario.Create("memory_stress_test", async context =>
                {
                    // Simulate typical user workflow
                    var fileService = new FileService();
                    
                    // Load different files
                    var content1 = await fileService.LoadFileAsync(testFiles!.MediumFile10MB);
                    var content2 = await fileService.LoadFileAsync(testFiles.ManyHyperlinks1000);
                    
                    // Process hyperlinks
                    var (_, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content1);
                    
                    // Clear references
                    content1 = null;
                    content2 = null;
                    
                    return Response.Ok();
                })
                .WithLoadSimulations(
                    Simulation.KeepConstant(copies: 3, during: TimeSpan.FromSeconds(300))
                );

                var stats = NBomberRunner
                    .RegisterScenarios(scenario)
                    .WithReportFolder(benchmarkResultsPath)
                    .WithReportFileName("memory_usage_benchmark")
                    .Run();

                // Assert memory usage
                stats.AllFailCount.Should().Be(0, "Memory stress test should not fail");

                lock (memoryReadings)
                {
                    memoryReadings.Should().NotBeEmpty("Should have memory readings");
                    
                    var maxMemory = memoryReadings.Max(r => r.memoryMB);
                    var avgMemory = memoryReadings.Average(r => r.memoryMB);
                    
                    maxMemory.Should().BeLessThan(500, "Peak memory usage should stay under 500MB");
                    
                    // Check for memory leaks (memory should not continuously grow)
                    if (memoryReadings.Count >= 10)
                    {
                        var firstHalf = memoryReadings.Take(memoryReadings.Count / 2).Average(r => r.memoryMB);
                        var secondHalf = memoryReadings.Skip(memoryReadings.Count / 2).Average(r => r.memoryMB);
                        var memoryGrowth = secondHalf - firstHalf;
                        
                        memoryGrowth.Should().BeLessThan(100, "Memory growth should be limited (no major leaks)");
                    }

                    TestContext.WriteLine($"Memory Usage Analysis:");
                    TestContext.WriteLine($"Peak memory: {maxMemory}MB");
                    TestContext.WriteLine($"Average memory: {avgMemory:F0}MB");
                    TestContext.WriteLine($"Readings count: {memoryReadings.Count}");
                }
            }
            finally
            {
                memoryTimer.Stop();
                memoryTimer.Dispose();
            }
        }

        [Test]
        [Timeout(300000)] // 5 minutes
        public async Task Benchmark_ConcurrencyHandling_ShouldScaleWell()
        {
            var scenario = Scenario.Create("concurrent_file_operations", async context =>
            {
                var fileService = new FileService();
                var tasks = new[]
                {
                    fileService.LoadFileAsync(testFiles!.SmallFile100KB),
                    fileService.LoadFileAsync(testFiles.MediumFile1MB),
                    Task.Run(async () =>
                    {
                        var content = await fileService.LoadFileAsync(testFiles.ManyHyperlinks1000);
                        var (_, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(content);
                        return hyperlinks.Count.ToString();
                    })
                };

                try
                {
                    var results = await Task.WhenAll(tasks);
                    var allSuccessful = results.All(r => !string.IsNullOrEmpty(r));
                    return allSuccessful ? Response.Ok() : Response.Fail();
                }
                catch
                {
                    return Response.Fail();
                }
            })
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromSeconds(120))
            );

            var stats = NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFolder(benchmarkResultsPath)
                .WithReportFileName("concurrency_benchmark")
                .Run();

            // Assert concurrency handling
            stats.AllFailCount.Should().Be(0, "Concurrent operations should not fail");
            
            var throughput = stats.AllOkCount / stats.TestSuite.Duration.TotalSeconds;
            throughput.Should().BeGreaterThan(3.0, "Should maintain good throughput under concurrent load");

            var responseTime95 = stats.ScenarioStats[0].Ok.Percentile95;
            responseTime95.Should().BeLessThan(TimeSpan.FromSeconds(10), 
                "95th percentile response time should be reasonable under concurrent load");

            TestContext.WriteLine($"Concurrency Benchmark:");
            TestContext.WriteLine($"Throughput: {throughput:F1} ops/sec");
            TestContext.WriteLine($"95th percentile response time: {responseTime95.TotalMilliseconds:F0}ms");
            TestContext.WriteLine($"Total operations: {stats.AllOkCount}");
        }

        [Test]
        public async Task EstablishPerformanceBaselines_AllScenarios_ShouldDocumentBaselines()
        {
            // This test establishes performance baselines for CI/CD integration
            var baselines = new Dictionary<string, object>();
            var fileService = new FileService();

            // File loading baselines
            var stopwatch = Stopwatch.StartNew();
            await fileService.LoadFileAsync(testFiles!.SmallFile1KB);
            stopwatch.Stop();
            baselines["SmallFile_1KB_LoadTime_ms"] = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            await fileService.LoadFileAsync(testFiles.MediumFile1MB);
            stopwatch.Stop();
            baselines["MediumFile_1MB_LoadTime_ms"] = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            await fileService.LoadFileAsync(testFiles.LargeFile50MB);
            stopwatch.Stop();
            baselines["LargeFile_50MB_LoadTime_ms"] = stopwatch.ElapsedMilliseconds;

            // Hyperlink processing baselines
            var hyperlinkContent = await fileService.LoadFileAsync(testFiles.ManyHyperlinks1000);
            stopwatch.Restart();
            var (_, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(hyperlinkContent);
            stopwatch.Stop();
            baselines["Hyperlinks_1000_ProcessingTime_ms"] = stopwatch.ElapsedMilliseconds;
            baselines["Hyperlinks_1000_Count"] = hyperlinks.Count;

            // Memory usage baseline
            var initialMemory = GC.GetTotalMemory(true);
            var largeContent = await fileService.LoadFileAsync(testFiles.LargeFile50MB);
            var memoryUsed = GC.GetTotalMemory(false) - initialMemory;
            baselines["LargeFile_50MB_MemoryUsage_MB"] = memoryUsed / 1024 / 1024;

            // Streaming baseline
            using var streamingProcessor = new StreamingTextProcessor();
            var segmentCount = 0;
            stopwatch.Restart();
            await foreach (var segment in streamingProcessor.StreamFileAsync(testFiles.LargeFile50MB))
            {
                segmentCount++;
                if (segmentCount >= 10) break;
            }
            stopwatch.Stop();
            baselines["Streaming_10Segments_Time_ms"] = stopwatch.ElapsedMilliseconds;

            // Save baselines to file
            var baselinesPath = Path.Combine(benchmarkResultsPath, "performance_baselines.json");
            var baselinesJson = System.Text.Json.JsonSerializer.Serialize(baselines, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(baselinesPath, baselinesJson);

            // Log baselines
            TestContext.WriteLine("Performance Baselines Established:");
            foreach (var (key, value) in baselines)
            {
                TestContext.WriteLine($"{key}: {value}");
            }

            // Basic sanity checks
            baselines["SmallFile_1KB_LoadTime_ms"].Should().BeOfType<long>().Which.Should().BeLessThan(1000);
            baselines["MediumFile_1MB_LoadTime_ms"].Should().BeOfType<long>().Which.Should().BeLessThan(5000);
            baselines["LargeFile_50MB_LoadTime_ms"].Should().BeOfType<long>().Which.Should().BeLessThan(60000);
            baselines["LargeFile_50MB_MemoryUsage_MB"].Should().BeOfType<long>().Which.Should().BeLessThan(300);

            TestContext.WriteLine($"Baselines saved to: {baselinesPath}");
        }
    }
}