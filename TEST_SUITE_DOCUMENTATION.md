# ModernTextViewer Comprehensive Test Suite Documentation

## Overview

This document describes the comprehensive test suite for ModernTextViewer, designed to validate all performance optimizations and prevent regressions. The test suite covers performance, stability, integration, and UI functionality.

## Test Architecture

The test suite is organized into multiple categories with different execution strategies:

### Test Categories

| Category | Purpose | Duration | CI Integration |
|----------|---------|----------|----------------|
| **Unit** | Individual component testing | < 2 minutes | Always |
| **Integration** | Feature interaction testing | < 5 minutes | Always |
| **Performance** | Performance validation | < 15 minutes | Pull requests |
| **Stability** | Long-running stability tests | < 60 minutes | Nightly |
| **Browser** | UI interaction testing | < 30 minutes | Comprehensive |
| **Benchmark** | Performance benchmarking | < 45 minutes | Weekly |

### Test Suites

#### 1. Quick Test Suite (< 5 minutes)
- **Purpose**: Fast feedback for development and CI/CD
- **Includes**: Unit tests, core integration tests, basic performance tests
- **Trigger**: Every push, pull request
- **Expected**: 100% pass rate

#### 2. Comprehensive Test Suite (< 60 minutes)
- **Purpose**: Full validation before release
- **Includes**: All test categories except stress tests
- **Trigger**: Nightly builds, manual execution
- **Expected**: >95% pass rate

#### 3. Stress Test Suite (< 120 minutes)
- **Purpose**: Extended load and stability validation
- **Includes**: Long-running tests, memory leak detection, extreme load scenarios
- **Trigger**: Weekly, before major releases
- **Expected**: No system failures

## Performance Requirements & Baselines

### File Loading Performance
| File Size | Target Load Time | Memory Limit | Throughput |
|-----------|------------------|--------------|------------|
| 1KB | < 100ms | < 5MB | > 10 MB/s |
| 1MB | < 1s | < 20MB | > 5 MB/s |
| 10MB | < 5s | < 50MB | > 2 MB/s |
| 50MB | < 30s | < 200MB | > 1 MB/s |
| 100MB | < 60s | < 300MB | > 0.5 MB/s |
| 500MB | Should not crash | < 1GB | Streaming mode |

### Hyperlink Processing Performance
| Hyperlink Count | Target Processing Time | Memory Per Link |
|----------------|------------------------|-----------------|
| 100 | < 100ms | < 1KB |
| 1,000 | < 1s | < 1KB |
| 10,000 | < 10s | < 1KB |

### UI Responsiveness
| Operation | Target Response Time | UI Thread Blocking |
|-----------|---------------------|-------------------|
| Theme switching | < 500ms | < 100ms |
| Font size change | < 100ms | < 50ms |
| Menu navigation | < 200ms | < 50ms |
| File drag-drop | Loading feedback immediate | < 200ms |

### Startup Performance
- **Cold start**: < 2 seconds
- **Warm start**: < 1 second
- **With file load (1MB)**: < 3 seconds

## Test Implementation Details

### Performance Tests (`PerformanceTests/`)

#### FileLoadingPerformanceTests.cs
```csharp
[Test]
[TestCase("SmallFile1KB", 1024, 100)] // Should load in <100ms
public async Task FileLoading_SmallFiles_ShouldLoadWithinPerformanceThresholds
```

Key validations:
- Load time thresholds
- Memory usage limits
- Throughput calculations
- UI responsiveness during loading

#### HyperlinkPerformanceTests.cs
```csharp
[Test]
[TestCase("ManyHyperlinks1000", 1000, 5000)] // Should process in <5s
public async Task HyperlinkProcessing_HighDensity_ShouldProcessWithinThresholds
```

Key validations:
- Processing speed vs hyperlink density
- Memory efficiency
- Streaming mode performance
- Caching effectiveness

#### PerformanceBenchmarkSuite.cs
Uses NBomber for load testing:
- Throughput benchmarks
- Concurrency testing
- Memory usage under load
- Performance regression detection

### Stability Tests (`StabilityTests/`)

#### LargeFileStabilityTests.cs
```csharp
[Test]
[Timeout(600000)] // 10 minutes timeout
public async Task VeryLargeFile_500MB_ShouldLoadWithoutCrashing
```

Key validations:
- No crashes with large files (up to 500MB)
- Memory leak detection over extended usage
- Error recovery from corrupted files
- Concurrent operation handling
- Long-running stability

### Integration Tests (`IntegrationTests/`)

#### FeatureIntegrationTests.cs
```csharp
[Test]
public async Task StreamingModeSwitch_LargeToSmallFile_ShouldSwitchModesCorrectly
```

Key validations:
- Streaming vs regular mode switching
- Progress dialog functionality
- Auto-save with large files
- Theme switching responsiveness
- Error recovery coordination

### Browser Tests (`BrowserTests/`)

#### UIInteractionTests.cs
Uses Playwright for Windows Forms automation:
```csharp
[Test]
[Timeout(60000)]
public async Task FileDragDrop_LargeFile_ShouldLoadWithoutFreezing
```

Key validations:
- File drag-and-drop functionality
- Hyperlink clicking and navigation
- Keyboard shortcuts responsiveness
- Menu interactions
- Visual regression testing

### Unit Tests (`UnitTests/`)

#### ServiceTests.cs
Individual service component testing:
- FileService: Load/save operations, encoding handling
- HyperlinkService: URL extraction, validation, formatting
- StreamingTextProcessor: Large file handling, segmentation
- PerformanceMonitor: Metrics collection, alerting
- ErrorManager: Error handling, recovery mechanisms

## Test Data Generation

### TestFileGenerator.cs
Programmatically generates test files:
- Various sizes (1KB to 500MB)
- Different hyperlink densities (0% to 50%)
- Special character sets
- Corrupted files for error testing
- Consistent test data with fixed random seeds

Example usage:
```csharp
var testFiles = await TestFileGenerator.GenerateTestFileSuiteAsync();
// Generates: SmallFile1KB, MediumFile1MB, LargeFile50MB, 
//           ManyHyperlinks1000, CorruptedFile, etc.
```

## Execution Methods

### 1. PowerShell Script (RunTests.ps1)
```powershell
# Quick tests for development
./RunTests.ps1 -TestSuite Quick

# Comprehensive tests for CI/CD
./RunTests.ps1 -TestSuite Comprehensive -GenerateReport

# Stress tests for stability validation
./RunTests.ps1 -TestSuite Stress -ContinuousIntegration
```

Features:
- Prerequisite checking (.NET SDK, project files)
- Automated build and test execution
- HTML report generation
- Timeout handling
- Result aggregation

### 2. GitHub Actions (.github/workflows/test-suite.yml)
Automated CI/CD integration:
- **Quick tests**: Every push/PR
- **Comprehensive tests**: Nightly builds
- **Stress tests**: Manual trigger
- **Performance regression**: Baseline comparison

### 3. Manual Execution
```bash
# Run specific test category
dotnet test --filter "Category=Performance"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "TestMethod=FileLoading_LargeFiles_ShouldLoadWithinPerformanceThresholds"
```

## Test Results & Reporting

### Test Reports
1. **TRX files**: Standard .NET test results
2. **HTML reports**: Human-readable summaries
3. **JSON reports**: Machine-readable metrics
4. **Coverage reports**: Code coverage analysis
5. **Benchmark reports**: Performance metrics over time

### Performance Baselines
Stored in `performance_baselines.json`:
```json
{
  "SmallFile_1KB_LoadTime_ms": 45,
  "MediumFile_1MB_LoadTime_ms": 523,
  "LargeFile_50MB_LoadTime_ms": 12847,
  "Hyperlinks_1000_ProcessingTime_ms": 234,
  "LargeFile_50MB_MemoryUsage_MB": 89,
  "Streaming_10Segments_Time_ms": 156
}
```

### Failure Analysis
Common failure categories:
1. **Performance regressions** (>20% slower than baseline)
2. **Memory leaks** (continuously growing memory usage)
3. **UI blocking** (>200ms UI thread blocks)
4. **Crashes** (unhandled exceptions, out of memory)
5. **Functional failures** (incorrect behavior)

## Continuous Integration Strategy

### GitHub Actions Workflow
```yaml
on:
  push: [Quick Tests]
  pull_request: [Quick Tests + Performance Regression]
  schedule: [Comprehensive Tests - Nightly at 2 AM]
  workflow_dispatch: [Manual trigger with suite selection]
```

### Performance Regression Detection
1. **Baseline establishment**: Store performance metrics
2. **Comparison**: Compare current vs previous runs
3. **Threshold alerting**: Flag >20% performance degradation
4. **PR comments**: Automated performance reports

### Test Result Integration
- **Test status badges**: README integration
- **PR status checks**: Block merge on test failures
- **Slack/Teams notifications**: Alert on nightly test failures
- **Coverage reports**: Track test coverage trends

## Adding New Tests

### Performance Test Checklist
- [ ] Define clear performance requirements
- [ ] Include memory usage validation
- [ ] Add timeout protection
- [ ] Test both success and failure scenarios
- [ ] Include performance regression detection

### Test Implementation Pattern
```csharp
[Test]
[Category("Performance")]
[Timeout(30000)] // 30 seconds
public async Task NewFeature_PerformanceScenario_ShouldMeetRequirements()
{
    // Arrange - Setup test data and monitoring
    var stopwatch = Stopwatch.StartNew();
    var initialMemory = GC.GetTotalMemory(true);
    
    // Act - Execute the feature
    var result = await ExecuteFeature();
    stopwatch.Stop();
    var finalMemory = GC.GetTotalMemory(false);
    
    // Assert - Validate performance requirements
    result.Should().NotBeNull();
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedTimeMs);
    (finalMemory - initialMemory).Should().BeLessThan(expectedMemoryBytes);
    
    // Log results for baseline tracking
    TestContext.WriteLine($"Performance: {stopwatch.ElapsedMilliseconds}ms, Memory: {(finalMemory - initialMemory) / 1024 / 1024}MB");
}
```

## Troubleshooting Common Issues

### Test Environment Setup
```bash
# Install required tools
dotnet tool install --global dotnet-reportgenerator-globaltool

# Install Playwright browsers
pwsh bin/Debug/net8.0/playwright.ps1 install --with-deps

# Clean test artifacts
dotnet clean && Remove-Item -Recurse -Force TestResults/
```

### Performance Test Failures
1. **Check system resources**: CPU, memory, disk space
2. **Verify test data**: Ensure test files are generated correctly
3. **Review baselines**: Check if performance requirements are realistic
4. **Isolate tests**: Run individual tests to identify specific issues

### CI/CD Issues
1. **Timeout configuration**: Adjust workflow timeout settings
2. **Resource limits**: GitHub Actions has compute limitations
3. **Artifact retention**: Manage storage for test results
4. **Secrets management**: Ensure proper access to external resources

## Future Enhancements

### Planned Improvements
- [ ] Visual regression testing with image comparison
- [ ] Automated performance trend analysis
- [ ] Load testing with realistic user scenarios  
- [ ] Cross-platform compatibility testing
- [ ] Accessibility testing integration
- [ ] Security vulnerability scanning

### Test Infrastructure Roadmap
- [ ] Test data versioning and management
- [ ] Distributed test execution
- [ ] Real-time performance monitoring
- [ ] Machine learning for anomaly detection
- [ ] Integration with APM tools

## Contact & Support

For questions about the test suite:
- **Documentation**: This file and inline code comments
- **Issues**: GitHub Issues with "testing" label
- **Performance concerns**: Tag issues with "performance" label

The test suite is designed to be comprehensive yet maintainable, providing confidence in the application's performance and stability while enabling rapid development cycles.