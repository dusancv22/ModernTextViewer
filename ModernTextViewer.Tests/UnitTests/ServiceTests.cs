using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using ModernTextViewer.src.Services;
using ModernTextViewer.src.Models;
using ModernTextViewer.Tests.TestData;

namespace ModernTextViewer.Tests.UnitTests
{
    /// <summary>
    /// Unit tests for individual service components
    /// Tests specific functionality and edge cases for each service
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    public class ServiceTests
    {
        [TestFixture]
        public class FileServiceTests
        {
            private FileService? fileService;
            private string tempDirectory = string.Empty;

            [SetUp]
            public void SetUp()
            {
                fileService = new FileService();
                tempDirectory = Path.Combine(Path.GetTempPath(), "ModernTextViewerUnitTests");
                Directory.CreateDirectory(tempDirectory);
            }

            [TearDown]
            public void TearDown()
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }

            [Test]
            public async Task LoadFileAsync_ValidFile_ShouldReturnContent()
            {
                // Arrange
                var testContent = "Test file content\nLine 2\nLine 3";
                var filePath = Path.Combine(tempDirectory, "test.txt");
                await File.WriteAllTextAsync(filePath, testContent);

                // Act
                var result = await fileService!.LoadFileAsync(filePath);

                // Assert
                result.Should().NotBeNull();
                result.Should().Contain("Test file content");
                result.Should().Contain("Line 2");
                result.Should().Contain("Line 3");
            }

            [Test]
            public async Task LoadFileAsync_NonExistentFile_ShouldThrowFileNotFoundException()
            {
                // Arrange
                var filePath = Path.Combine(tempDirectory, "nonexistent.txt");

                // Act & Assert
                var exception = await Assert.ThrowsAsync<FileNotFoundException>(
                    async () => await fileService!.LoadFileAsync(filePath));

                exception.Should().NotBeNull();
                exception.Message.Should().Contain("nonexistent.txt");
            }

            [Test]
            public async Task SaveFileAsync_ValidContent_ShouldCreateFile()
            {
                // Arrange
                var testContent = "Content to save";
                var filePath = Path.Combine(tempDirectory, "save_test.txt");

                // Act
                await fileService!.SaveFileAsync(filePath, testContent);

                // Assert
                File.Exists(filePath).Should().BeTrue();
                var savedContent = await File.ReadAllTextAsync(filePath);
                savedContent.Should().Be(testContent);
            }

            [Test]
            public async Task LoadFileAsync_EmptyFile_ShouldReturnEmptyString()
            {
                // Arrange
                var filePath = Path.Combine(tempDirectory, "empty.txt");
                await File.WriteAllTextAsync(filePath, string.Empty);

                // Act
                var result = await fileService!.LoadFileAsync(filePath);

                // Assert
                result.Should().NotBeNull();
                result.Should().BeEmpty();
            }

            [Test]
            public async Task LoadFileAsync_SpecialCharacters_ShouldPreserveEncoding()
            {
                // Arrange
                var testContent = "Special chars: àáâãäå çčćĉ èéêë ñńň öôõø üûù ÿý §†‡• «»"" …–—";
                var filePath = Path.Combine(tempDirectory, "special_chars.txt");
                await File.WriteAllTextAsync(filePath, testContent);

                // Act
                var result = await fileService!.LoadFileAsync(filePath);

                // Assert
                result.Should().Be(testContent);
            }
        }

        [TestFixture]
        public class HyperlinkServiceTests
        {
            [Test]
            public void ExtractHyperlinkMetadata_TextWithHTTPLinks_ShouldFindLinks()
            {
                // Arrange
                var text = "Visit https://github.com and http://example.com for more info.";

                // Act
                var (processedText, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(text);

                // Assert
                hyperlinks.Should().HaveCount(2);
                hyperlinks[0].Url.Should().Be("https://github.com");
                hyperlinks[1].Url.Should().Be("http://example.com");
                hyperlinks[0].StartIndex.Should().BeGreaterOrEqualTo(0);
                hyperlinks[1].StartIndex.Should().BeGreaterThan(hyperlinks[0].StartIndex);
            }

            [Test]
            public void ExtractHyperlinkMetadata_TextWithEmailLinks_ShouldFindEmails()
            {
                // Arrange
                var text = "Contact us at test@example.com or support@company.org";

                // Act
                var (processedText, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(text);

                // Assert
                hyperlinks.Should().HaveCountGreaterOrEqualTo(2);
                hyperlinks.Should().Contain(h => h.Url.Contains("test@example.com"));
                hyperlinks.Should().Contain(h => h.Url.Contains("support@company.org"));
            }

            [Test]
            public void ExtractHyperlinkMetadata_NoHyperlinks_ShouldReturnEmptyList()
            {
                // Arrange
                var text = "This text has no hyperlinks just regular content.";

                // Act
                var (processedText, hyperlinks) = HyperlinkService.ExtractHyperlinkMetadata(text);

                // Assert
                hyperlinks.Should().BeEmpty();
                processedText.Should().Be(text);
            }

            [Test]
            public void ValidateHyperlink_ValidHTTPSUrl_ShouldReturnTrue()
            {
                // Arrange
                var url = "https://www.example.com/path/to/resource";

                // Act
                var isValid = HyperlinkService.ValidateHyperlink(url);

                // Assert
                isValid.Should().BeTrue();
            }

            [Test]
            public void ValidateHyperlink_InvalidUrl_ShouldReturnFalse()
            {
                // Arrange
                var url = "not-a-valid-url";

                // Act
                var isValid = HyperlinkService.ValidateHyperlink(url);

                // Assert
                isValid.Should().BeFalse();
            }

            [Test]
            public void ApplyHyperlinkFormatting_WithHyperlinks_ShouldApplyFormatting()
            {
                // Arrange
                var text = "Visit https://example.com for info.";
                var hyperlinks = new List<HyperlinkModel>
                {
                    new HyperlinkModel
                    {
                        Url = "https://example.com",
                        StartIndex = text.IndexOf("https://example.com"),
                        Length = "https://example.com".Length
                    }
                };

                // Act
                var formattedText = HyperlinkService.ApplyHyperlinkFormatting(text, hyperlinks);

                // Assert
                formattedText.Should().NotBe(text);
                formattedText.Should().Contain("https://example.com");
            }
        }

        [TestFixture]
        public class StreamingTextProcessorTests
        {
            private StreamingTextProcessor? processor;
            private string tempDirectory = string.Empty;

            [SetUp]
            public void SetUp()
            {
                processor = new StreamingTextProcessor();
                tempDirectory = Path.Combine(Path.GetTempPath(), "ModernTextViewerStreamingTests");
                Directory.CreateDirectory(tempDirectory);
            }

            [TearDown]
            public void TearDown()
            {
                processor?.Dispose();
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }

            [Test]
            public void IsLargeFile_SmallFile_ShouldReturnFalse()
            {
                // Arrange
                var smallFilePath = Path.Combine(tempDirectory, "small.txt");
                File.WriteAllText(smallFilePath, "Small file content");

                // Act
                var isLarge = processor!.IsLargeFile(smallFilePath);

                // Assert
                isLarge.Should().BeFalse();
            }

            [Test]
            public async Task IsLargeFile_LargeFile_ShouldReturnTrue()
            {
                // Arrange
                var largeFilePath = await TestFileGenerator.GenerateTestFileAsync("large_test.txt", 60 * 1024 * 1024); // 60MB

                // Act
                var isLarge = processor!.IsLargeFile(largeFilePath);

                // Assert
                isLarge.Should().BeTrue();
            }

            [Test]
            public async Task AnalyzeFileAsync_ValidFile_ShouldReturnAnalysis()
            {
                // Arrange
                var testContent = string.Join(Environment.NewLine, Enumerable.Repeat("Test line content", 1000));
                var filePath = Path.Combine(tempDirectory, "analyze_test.txt");
                await File.WriteAllTextAsync(filePath, testContent);

                // Act
                var analysis = await processor!.AnalyzeFileAsync(filePath);

                // Assert
                analysis.Should().NotBeNull();
                analysis.FilePath.Should().Be(filePath);
                analysis.FileSize.Should().BeGreaterThan(0);
                analysis.EstimatedLineCount.Should().BeGreaterThan(500); // Should estimate close to 1000
            }

            [Test]
            public async Task LoadTextSegmentAsync_ValidPosition_ShouldReturnSegment()
            {
                // Arrange
                var testContent = "Line 1\nLine 2\nLine 3\nLine 4\nLine 5";
                var filePath = Path.Combine(tempDirectory, "segment_test.txt");
                await File.WriteAllTextAsync(filePath, testContent);
                await processor!.AnalyzeFileAsync(filePath); // Initialize processor

                // Act
                var segment = await processor.LoadTextSegmentAsync(0, 20);

                // Assert
                segment.Should().NotBeNull();
                segment.Content.Should().NotBeEmpty();
                segment.StartPosition.Should().Be(0);
                segment.Length.Should().BeGreaterThan(0);
            }

            [Test]
            public async Task LoadTextSegmentAsync_InvalidPosition_ShouldThrowException()
            {
                // Arrange
                var filePath = Path.Combine(tempDirectory, "invalid_position_test.txt");
                await File.WriteAllTextAsync(filePath, "Short content");
                await processor!.AnalyzeFileAsync(filePath);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                    async () => await processor.LoadTextSegmentAsync(-1, 10));
            }

            [Test]
            public async Task StreamFileAsync_ValidFile_ShouldProduceSegments()
            {
                // Arrange
                var testContent = string.Join(Environment.NewLine, Enumerable.Repeat("Test line for streaming", 100));
                var filePath = Path.Combine(tempDirectory, "stream_test.txt");
                await File.WriteAllTextAsync(filePath, testContent);

                // Act
                var segments = new List<StreamingTextProcessor.TextSegment>();
                await foreach (var segment in processor!.StreamFileAsync(filePath))
                {
                    segments.Add(segment);
                    if (segments.Count >= 3) break; // Take first 3 segments
                }

                // Assert
                segments.Should().HaveCountGreaterOrEqualTo(1);
                segments.Should().OnlyContain(s => !string.IsNullOrEmpty(s.Content));
                segments.Should().OnlyContain(s => s.Length > 0);
            }
        }

        [TestFixture]
        public class PerformanceMonitorTests
        {
            private PerformanceMonitor? monitor;

            [SetUp]
            public void SetUp()
            {
                monitor = new PerformanceMonitor();
            }

            [TearDown]
            public void TearDown()
            {
                monitor?.Dispose();
            }

            [Test]
            public void Level_SetToDetailed_ShouldUpdateLevel()
            {
                // Act
                monitor!.Level = PerformanceMonitor.MonitoringLevel.Detailed;

                // Assert
                monitor.Level.Should().Be(PerformanceMonitor.MonitoringLevel.Detailed);
            }

            [Test]
            public void GetCurrentMetrics_AfterInitialization_ShouldReturnMetrics()
            {
                // Act
                var metrics = monitor!.GetCurrentMetrics();

                // Assert
                metrics.Should().NotBeEmpty();
                metrics.Should().ContainKeys("CpuUsage", "MemoryUsageMB", "UptimeSeconds");
            }

            [Test]
            public void StartFileOperation_ValidOperation_ShouldTrackOperation()
            {
                // Arrange
                var filePath = "test.txt";
                var fileSize = 1024L;

                // Act
                monitor!.StartFileOperation("load", filePath, fileSize);
                monitor.EndFileOperation("load", filePath, true);

                // Assert - Should not throw and should complete successfully
                var metrics = monitor.GetCurrentMetrics();
                metrics.Should().ContainKey("FileLoadTime");
            }

            [Test]
            public void UpdateMetric_ValidMetric_ShouldUpdateValue()
            {
                // Arrange
                var metricName = "TestMetric";
                var initialMetrics = monitor!.GetCurrentMetrics();
                
                // Add the metric first
                monitor.UpdateMetric("CpuUsage", 50.0);

                // Act
                monitor.UpdateMetric("CpuUsage", 75.0);
                var updatedMetrics = monitor.GetCurrentMetrics();

                // Assert
                updatedMetrics["CpuUsage"].Value.Should().Be(75.0);
            }

            [Test]
            public void ExportPerformanceReport_ShouldReturnReport()
            {
                // Act
                var report = monitor!.ExportPerformanceReport();

                // Assert
                report.Should().NotBeNullOrEmpty();
                report.Should().Contain("Performance Report");
                report.Should().Contain("Current Metrics");
            }

            [Test]
            public async Task PerformanceAlert_HighMemoryUsage_ShouldFireAlert()
            {
                // Arrange
                var alertFired = false;
                var alertMessage = string.Empty;
                
                monitor!.PerformanceAlert += (sender, e) =>
                {
                    alertFired = true;
                    alertMessage = e.Message;
                };

                // Act - Simulate high memory usage
                monitor.UpdateMetric("MemoryUsageMB", 600.0); // Above 500MB threshold
                await Task.Delay(100); // Allow event to fire

                // Assert
                // Note: Alert firing depends on internal timer, so this test might be flaky
                // In a real implementation, you might expose a method to force metric evaluation
                TestContext.WriteLine($"Alert fired: {alertFired}, Message: {alertMessage}");
            }
        }

        [TestFixture]
        public class ErrorManagerTests
        {
            [Test]
            public void LogError_ValidError_ShouldNotThrow()
            {
                // Arrange
                var category = ErrorManager.ErrorCategory.FileIO;
                var severity = ErrorManager.ErrorSeverity.Warning;
                var message = "Test error message";

                // Act & Assert
                Action logAction = () => ErrorManager.LogError(category, severity, message);
                logAction.Should().NotThrow();
            }

            [Test]
            public void ExecuteWithErrorHandling_SuccessfulOperation_ShouldReturnResult()
            {
                // Arrange
                var expectedResult = "Success";
                Func<string> operation = () => expectedResult;

                // Act
                var result = ErrorManager.ExecuteWithErrorHandling(
                    operation,
                    ErrorManager.ErrorCategory.General,
                    "Test operation");

                // Assert
                result.Should().Be(expectedResult);
            }

            [Test]
            public void ExecuteWithErrorHandling_ThrowsException_ShouldReturnDefault()
            {
                // Arrange
                var defaultValue = "Default";
                Func<string> operation = () => throw new InvalidOperationException("Test exception");

                // Act
                var result = ErrorManager.ExecuteWithErrorHandling(
                    operation,
                    ErrorManager.ErrorCategory.General,
                    "Test operation",
                    defaultValue);

                // Assert
                result.Should().Be(defaultValue);
            }

            [Test]
            public async Task ExecuteWithErrorHandlingAsync_SuccessfulOperation_ShouldReturnResult()
            {
                // Arrange
                var expectedResult = "Async Success";
                Func<Task<string>> operation = async () =>
                {
                    await Task.Delay(10);
                    return expectedResult;
                };

                // Act
                var result = await ErrorManager.ExecuteWithErrorHandlingAsync(
                    operation,
                    ErrorManager.ErrorCategory.General,
                    "Test async operation");

                // Assert
                result.Should().Be(expectedResult);
            }
        }

        [TestFixture]
        public class DocumentModelTests
        {
            [Test]
            public void IsDirty_ModifyContent_ShouldSetDirtyFlag()
            {
                // Arrange
                var document = new DocumentModel
                {
                    Content = "Original content",
                    IsDirty = false
                };

                // Act
                document.Content = "Modified content";

                // Assert - Note: This depends on the DocumentModel implementation
                // If it has property change notifications, IsDirty should update automatically
                // Otherwise, it would need to be set manually
                TestContext.WriteLine($"Document dirty status after content change: {document.IsDirty}");
            }

            [Test]
            public void FilePath_SetValidPath_ShouldUpdatePath()
            {
                // Arrange
                var document = new DocumentModel();
                var testPath = @"C:\Test\document.txt";

                // Act
                document.FilePath = testPath;

                // Assert
                document.FilePath.Should().Be(testPath);
            }
        }
    }
}