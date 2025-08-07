# ModernTextViewer Automated Test Execution Script
# This script orchestrates the complete test suite execution with reporting

param(
    [ValidateSet("Quick", "Comprehensive", "Stress", "All")]
    [string]$TestSuite = "Quick",
    
    [switch]$GenerateReport,
    
    [switch]$ContinuousIntegration,
    
    [string]$OutputPath = "",
    
    [switch]$Verbose,
    
    [int]$TimeoutMinutes = 60
)

# Script configuration
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = $ScriptDir
$TestProject = Join-Path $ProjectDir "ModernTextViewer.Tests\ModernTextViewer.Tests.csproj"
$MainProject = Join-Path $ProjectDir "ModernTextViewer.csproj"
$ResultsDir = if ($OutputPath) { $OutputPath } else { Join-Path $env:TEMP "ModernTextViewerTestResults" }

# Ensure results directory exists
if (-not (Test-Path $ResultsDir)) {
    New-Item -ItemType Directory -Path $ResultsDir -Force | Out-Null
}

Write-Host "=== ModernTextViewer Test Execution ===" -ForegroundColor Green
Write-Host "Test Suite: $TestSuite" -ForegroundColor Yellow
Write-Host "Results Directory: $ResultsDir" -ForegroundColor Yellow
Write-Host "Continuous Integration: $ContinuousIntegration" -ForegroundColor Yellow
Write-Host ""

# Function to log messages
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch($Level) {
        "INFO" { "White" }
        "WARN" { "Yellow" }
        "ERROR" { "Red" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# Function to check prerequisites
function Test-Prerequisites {
    Write-Log "Checking prerequisites..."
    
    # Check .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Log ".NET SDK Version: $dotnetVersion" "SUCCESS"
    }
    catch {
        Write-Log "❌ .NET SDK not found. Please install .NET 8.0 SDK." "ERROR"
        exit 1
    }
    
    # Check project files
    if (-not (Test-Path $MainProject)) {
        Write-Log "❌ Main project not found: $MainProject" "ERROR"
        exit 1
    }
    
    if (-not (Test-Path $TestProject)) {
        Write-Log "❌ Test project not found: $TestProject" "ERROR"
        exit 1
    }
    
    Write-Log "✅ Prerequisites check passed" "SUCCESS"
}

# Function to build projects
function Build-Projects {
    Write-Log "Building projects..."
    
    try {
        Write-Log "Building main project..."
        dotnet build $MainProject -c Release --no-restore -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Main project build failed" }
        
        Write-Log "Building test project..."
        dotnet build $TestProject -c Release --no-restore -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Test project build failed" }
        
        Write-Log "✅ Build completed successfully" "SUCCESS"
    }
    catch {
        Write-Log "❌ Build failed: $_" "ERROR"
        exit 1
    }
}

# Function to restore packages
function Restore-Packages {
    Write-Log "Restoring NuGet packages..."
    
    try {
        dotnet restore $ProjectDir -v minimal
        if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
        
        Write-Log "✅ Package restore completed" "SUCCESS"
    }
    catch {
        Write-Log "❌ Package restore failed: $_" "ERROR"
        exit 1
    }
}

# Function to install Playwright browsers
function Install-PlaywrightBrowsers {
    if ($TestSuite -eq "Comprehensive" -or $TestSuite -eq "All") {
        Write-Log "Installing Playwright browsers..."
        
        try {
            # Install Playwright browsers
            dotnet run --project $TestProject --verbosity quiet -- install
            Write-Log "✅ Playwright browsers installed" "SUCCESS"
        }
        catch {
            Write-Log "⚠️ Playwright browser installation failed (browser tests may be skipped): $_" "WARN"
        }
    }
}

# Function to run specific test categories
function Run-TestCategory {
    param(
        [string]$Category,
        [int]$TimeoutMinutes = 30,
        [string]$AdditionalArgs = ""
    )
    
    Write-Log "Running $Category tests..." "INFO"
    
    $testFilter = "Category=$Category"
    $logFile = Join-Path $ResultsDir "$Category-test-results.trx"
    $coverageFile = Join-Path $ResultsDir "$Category-coverage.xml"
    
    $dotnetArgs = @(
        "test"
        $TestProject
        "--configuration", "Release"
        "--filter", $testFilter
        "--logger", "trx;LogFileName=$logFile"
        "--collect:`"XPlat Code Coverage`""
        "--results-directory", $ResultsDir
        "--verbosity", $(if ($Verbose) { "normal" } else { "minimal" })
        "--no-build"
        "--no-restore"
    )
    
    if ($AdditionalArgs) {
        $dotnetArgs += $AdditionalArgs.Split(' ')
    }
    
    try {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        # Set timeout
        $timeout = $TimeoutMinutes * 60 * 1000 # Convert to milliseconds
        
        $process = Start-Process -FilePath "dotnet" -ArgumentList $dotnetArgs -NoNewWindow -PassThru -RedirectStandardOutput -RedirectStandardError
        
        if (-not $process.WaitForExit($timeout)) {
            $process.Kill()
            throw "Test execution timed out after $TimeoutMinutes minutes"
        }
        
        $stopwatch.Stop()
        $exitCode = $process.ExitCode
        
        if ($exitCode -eq 0) {
            Write-Log "✅ $Category tests completed successfully in $($stopwatch.Elapsed)" "SUCCESS"
            return $true
        } else {
            Write-Log "❌ $Category tests failed (exit code: $exitCode)" "ERROR"
            return $false
        }
    }
    catch {
        Write-Log "❌ Error running $Category tests: $_" "ERROR"
        return $false
    }
}

# Function to generate test report
function Generate-TestReport {
    Write-Log "Generating test report..."
    
    try {
        $reportFile = Join-Path $ResultsDir "test-report-summary.html"
        $trxFiles = Get-ChildItem -Path $ResultsDir -Filter "*.trx"
        
        # Simple HTML report generation
        $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>ModernTextViewer Test Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background-color: #f0f0f0; padding: 10px; border-radius: 5px; }
        .success { color: green; } .error { color: red; } .warning { color: orange; }
        table { border-collapse: collapse; width: 100%; margin-top: 20px; }
        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }
        th { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <div class="header">
        <h1>ModernTextViewer Test Report</h1>
        <p>Generated: $(Get-Date)</p>
        <p>Test Suite: $TestSuite</p>
        <p>Machine: $env:COMPUTERNAME</p>
    </div>
    
    <h2>Test Results Summary</h2>
    <table>
        <tr><th>Test Category</th><th>Status</th><th>Results File</th></tr>
"@
        
        foreach ($trxFile in $trxFiles) {
            $category = [System.IO.Path]::GetFileNameWithoutExtension($trxFile.Name) -replace '-test-results$', ''
            $status = if ($trxFile.Length -gt 0) { "✅ Completed" } else { "❌ Failed" }
            $html += "<tr><td>$category</td><td>$status</td><td>$($trxFile.Name)</td></tr>`n"
        }
        
        $html += @"
    </table>
    
    <h2>System Information</h2>
    <ul>
        <li>OS: $($env:OS) - $(Get-WmiObject -Class Win32_OperatingSystem | Select-Object -ExpandProperty Caption)</li>
        <li>Processor: $(Get-WmiObject -Class Win32_Processor | Select-Object -First 1 -ExpandProperty Name)</li>
        <li>Total Memory: $([math]::Round((Get-WmiObject -Class Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)) GB</li>
        <li>.NET Version: $(dotnet --version)</li>
        <li>PowerShell Version: $($PSVersionTable.PSVersion)</li>
    </ul>
    
    <h2>Test Files</h2>
    <ul>
"@
        
        Get-ChildItem -Path $ResultsDir -File | ForEach-Object {
            $html += "<li><a href=`"$($_.Name)`">$($_.Name)</a> ($([math]::Round($_.Length / 1KB, 2)) KB)</li>`n"
        }
        
        $html += @"
    </ul>
    
    <p><em>Report generated by ModernTextViewer automated test script</em></p>
</body>
</html>
"@
        
        $html | Out-File -FilePath $reportFile -Encoding UTF8
        Write-Log "✅ Test report generated: $reportFile" "SUCCESS"
        
        # Open report if not in CI mode
        if (-not $ContinuousIntegration) {
            Start-Process $reportFile
        }
    }
    catch {
        Write-Log "⚠️ Failed to generate test report: $_" "WARN"
    }
}

# Function to clean up test artifacts
function Clean-TestArtifacts {
    Write-Log "Cleaning up test artifacts..."
    
    try {
        # Clean bin and obj directories
        Get-ChildItem -Path $ProjectDir -Recurse -Directory -Name "bin", "obj" | ForEach-Object {
            $fullPath = Join-Path $ProjectDir $_
            if (Test-Path $fullPath) {
                Remove-Item -Path $fullPath -Recurse -Force
            }
        }
        
        # Clean temporary test files
        $tempTestDir = Join-Path $env:TEMP "ModernTextViewerTests"
        if (Test-Path $tempTestDir) {
            Remove-Item -Path $tempTestDir -Recurse -Force
        }
        
        Write-Log "✅ Test artifacts cleaned up" "SUCCESS"
    }
    catch {
        Write-Log "⚠️ Failed to clean up some test artifacts: $_" "WARN"
    }
}

# Main execution flow
try {
    $scriptStartTime = Get-Date
    
    # Check prerequisites
    Test-Prerequisites
    
    # Clean previous artifacts
    Clean-TestArtifacts
    
    # Restore packages
    Restore-Packages
    
    # Build projects
    Build-Projects
    
    # Install Playwright browsers if needed
    Install-PlaywrightBrowsers
    
    # Run test suites based on selection
    $testResults = @{}
    
    switch ($TestSuite) {
        "Quick" {
            Write-Log "=== RUNNING QUICK TEST SUITE ===" "INFO"
            $testResults["Unit"] = Run-TestCategory "Unit" 10
            $testResults["Integration"] = Run-TestCategory "Integration" 15
            $testResults["Performance"] = Run-TestCategory "Performance" 20 "--filter `"Category=Performance&TestCategory!=Benchmark`""
        }
        
        "Comprehensive" {
            Write-Log "=== RUNNING COMPREHENSIVE TEST SUITE ===" "INFO"
            $testResults["Unit"] = Run-TestCategory "Unit" 15
            $testResults["Integration"] = Run-TestCategory "Integration" 20
            $testResults["Performance"] = Run-TestCategory "Performance" 30
            $testResults["Stability"] = Run-TestCategory "Stability" 45
            if (-not $ContinuousIntegration) {
                $testResults["Browser"] = Run-TestCategory "Browser" 30
            }
        }
        
        "Stress" {
            Write-Log "=== RUNNING STRESS TEST SUITE ===" "INFO"
            $testResults["Stability"] = Run-TestCategory "Stability" 60
            $testResults["LongRunning"] = Run-TestCategory "LongRunning" 120
            $testResults["Benchmark"] = Run-TestCategory "Benchmark" 60
        }
        
        "All" {
            Write-Log "=== RUNNING ALL TEST SUITES ===" "INFO"
            $testResults["Unit"] = Run-TestCategory "Unit" 20
            $testResults["Integration"] = Run-TestCategory "Integration" 30
            $testResults["Performance"] = Run-TestCategory "Performance" 45
            $testResults["Stability"] = Run-TestCategory "Stability" 60
            $testResults["LongRunning"] = Run-TestCategory "LongRunning" 120
            $testResults["Benchmark"] = Run-TestCategory "Benchmark" 60
            if (-not $ContinuousIntegration) {
                $testResults["Browser"] = Run-TestCategory "Browser" 45
            }
        }
    }
    
    # Generate report if requested
    if ($GenerateReport) {
        Generate-TestReport
    }
    
    # Calculate overall results
    $totalCategories = $testResults.Count
    $passedCategories = ($testResults.Values | Where-Object { $_ }).Count
    $failedCategories = $totalCategories - $passedCategories
    
    $scriptEndTime = Get-Date
    $totalDuration = $scriptEndTime - $scriptStartTime
    
    Write-Log "" "INFO"
    Write-Log "=== TEST EXECUTION SUMMARY ===" "INFO"
    Write-Log "Total Duration: $totalDuration" "INFO"
    Write-Log "Test Categories: $totalCategories" "INFO"
    Write-Log "Passed Categories: $passedCategories" "SUCCESS"
    Write-Log "Failed Categories: $failedCategories" $(if ($failedCategories -gt 0) { "ERROR" } else { "INFO" })
    Write-Log "Results Directory: $ResultsDir" "INFO"
    
    if ($failedCategories -eq 0) {
        Write-Log "🎉 ALL TESTS PASSED! 🎉" "SUCCESS"
        exit 0
    } else {
        Write-Log "❌ Some test categories failed. Check individual test results." "ERROR"
        
        # List failed categories
        $failedCategoryNames = $testResults.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object { $_.Key }
        Write-Log "Failed categories: $($failedCategoryNames -join ', ')" "ERROR"
        
        exit 1
    }
}
catch {
    Write-Log "❌ Script execution failed: $_" "ERROR"
    exit 1
}
finally {
    # Final cleanup
    if (-not $ContinuousIntegration) {
        Write-Log "Test execution completed. Results saved to: $ResultsDir" "INFO"
    }
}