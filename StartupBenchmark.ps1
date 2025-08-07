# Startup Performance Benchmark for ModernTextViewer
# This script measures the startup time of the application

param(
    [int]$Iterations = 5,
    [string]$BuildConfiguration = "Release"
)

Write-Host "=== ModernTextViewer Startup Performance Benchmark ===" -ForegroundColor Cyan
Write-Host "Configuration: $BuildConfiguration" -ForegroundColor Green
Write-Host "Iterations: $Iterations" -ForegroundColor Green
Write-Host ""

# Path to the executable
$exePath = "bin\$BuildConfiguration\net8.0-windows\ModernTextViewer.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Error: Executable not found at $exePath" -ForegroundColor Red
    Write-Host "Please build the project first with: dotnet build -c $BuildConfiguration" -ForegroundColor Yellow
    exit 1
}

$startupTimes = @()

for ($i = 1; $i -le $Iterations; $i++) {
    Write-Host "Run $i/$Iterations..." -NoNewline
    
    # Start the process and measure time to show window
    $startTime = Get-Date
    $process = Start-Process -FilePath $exePath -PassThru -WindowStyle Normal
    
    # Wait for the main window to appear
    $timeout = 10 # seconds
    $elapsed = 0
    $windowFound = $false
    
    while ($elapsed -lt $timeout -and -not $windowFound) {
        Start-Sleep -Milliseconds 50
        $elapsed += 0.05
        
        if ($process.MainWindowHandle -ne [System.IntPtr]::Zero) {
            $windowFound = $true
            $endTime = Get-Date
            $startupTime = ($endTime - $startTime).TotalMilliseconds
        }
    }
    
    # Clean up
    if (-not $process.HasExited) {
        $process.CloseMainWindow()
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) {
            $process.Kill()
        }
    }
    
    if ($windowFound) {
        $startupTimes += $startupTime
        Write-Host " ${startupTime:F0}ms" -ForegroundColor Green
    } else {
        Write-Host " TIMEOUT" -ForegroundColor Red
    }
    
    # Brief pause between runs
    Start-Sleep -Milliseconds 1000
}

# Calculate statistics
if ($startupTimes.Count -gt 0) {
    $avgTime = ($startupTimes | Measure-Object -Average).Average
    $minTime = ($startupTimes | Measure-Object -Minimum).Minimum  
    $maxTime = ($startupTimes | Measure-Object -Maximum).Maximum
    
    Write-Host ""
    Write-Host "=== RESULTS ===" -ForegroundColor Cyan
    Write-Host "Successful runs: $($startupTimes.Count)/$Iterations" -ForegroundColor Green
    Write-Host "Average startup time: ${avgTime:F0}ms" -ForegroundColor $(if ($avgTime -lt 2000) { "Green" } elseif ($avgTime -lt 3000) { "Yellow" } else { "Red" })
    Write-Host "Minimum startup time: ${minTime:F0}ms" -ForegroundColor Green
    Write-Host "Maximum startup time: ${maxTime:F0}ms" -ForegroundColor Green
    Write-Host ""
    
    if ($avgTime -lt 2000) {
        Write-Host "✅ EXCELLENT: Startup time is under 2 seconds!" -ForegroundColor Green
    } elseif ($avgTime -lt 3000) {
        Write-Host "⚠️ GOOD: Startup time is under 3 seconds but could be improved" -ForegroundColor Yellow
    } else {
        Write-Host "❌ NEEDS IMPROVEMENT: Startup time exceeds 3 seconds" -ForegroundColor Red
    }
} else {
    Write-Host "❌ FAILED: No successful startups recorded" -ForegroundColor Red
}

Write-Host ""
Write-Host "Benchmark complete." -ForegroundColor Cyan