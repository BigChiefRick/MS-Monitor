#Requires -RunAsAdministrator

param(
    [switch]$GenerateReport
)

$ErrorActionPreference = "Continue"
$TestResults = @()
$TestStartTime = Get-Date

function Add-TestResult($TestName, $Status, $Details = "") {
    $global:TestResults += [PSCustomObject]@{
        TestName = $TestName
        Status = $Status
        Details = $Details
        Timestamp = Get-Date
    }
    
    if ($Status -eq "PASS") {
        Write-Host "✅ $TestName" -ForegroundColor Green
    } elseif ($Status -eq "FAIL") {
        Write-Host "❌ $TestName" -ForegroundColor Red
    } else {
        Write-Host "⚠️ $TestName" -ForegroundColor Yellow
    }
    
    if ($Details) {
        Write-Host "   $Details" -ForegroundColor Gray
    }
}

Write-Host "=== MS-Monitor Build and Test Validation ===" -ForegroundColor Cyan
Write-Host "Started: $(Get-Date)" -ForegroundColor Blue
Write-Host ""

# Test 1: Prerequisites
Write-Host "Phase 1: Prerequisites" -ForegroundColor Yellow
Write-Host "=" * 30

try {
    $dotnetVersion = & dotnet --version 2>$null
    if ($dotnetVersion -and $dotnetVersion.StartsWith("8.")) {
        Add-TestResult "DotNet 8.0 SDK" "PASS" "Version: $dotnetVersion"
    } else {
        Add-TestResult "DotNet 8.0 SDK" "FAIL" "Wrong version or missing: $dotnetVersion"
    }
} catch {
    Add-TestResult "DotNet 8.0 SDK" "FAIL" "Not found"
}

try {
    $nodeVersion = & node --version 2>$null
    if ($nodeVersion) {
        Add-TestResult "Node.js" "PASS" "Version: $nodeVersion"
    } else {
        Add-TestResult "Node.js" "FAIL" "Not found"
    }
} catch {
    Add-TestResult "Node.js" "FAIL" "Not available"
}

$currentUser = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if ($currentUser.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Add-TestResult "Admin Rights" "PASS" "Running as Administrator"
} else {
    Add-TestResult "Admin Rights" "FAIL" "Need Administrator privileges"
}

Write-Host ""

# Test 2: Solution Build
Write-Host "Phase 2: Build Process" -ForegroundColor Yellow  
Write-Host "=" * 30

$originalLocation = Get-Location
if (Test-Path "..\MicrosoftEndpointMonitor.sln") {
    Set-Location ".."
    Add-TestResult "Solution File" "PASS" "Found in parent directory"
} elseif (Test-Path "MicrosoftEndpointMonitor.sln") {
    Add-TestResult "Solution File" "PASS" "Found in current directory"
} else {
    Add-TestResult "Solution File" "FAIL" "MicrosoftEndpointMonitor.sln not found"
}

Write-Host "   Restoring packages..." -ForegroundColor Gray
$restoreOutput = & dotnet restore 2>&1
if ($LASTEXITCODE -eq 0) {
    Add-TestResult "NuGet Restore" "PASS" "Packages restored"
} else {
    Add-TestResult "NuGet Restore" "FAIL" "Restore failed"
}

Write-Host "   Building solution..." -ForegroundColor Gray
$buildOutput = & dotnet build --configuration Debug --no-restore 2>&1
if ($LASTEXITCODE -eq 0) {
    Add-TestResult "Solution Build" "PASS" "Build successful"
} else {
    Add-TestResult "Solution Build" "FAIL" "Build failed"
}

$buildFiles = @(
    "src\MicrosoftEndpointMonitor.Service\bin\Debug\net8.0\MicrosoftEndpointMonitor.Service.exe",
    "src\MicrosoftEndpointMonitor.Api\bin\Debug\net8.0\MicrosoftEndpointMonitor.Api.dll"
)

$missingFiles = @()
foreach ($file in $buildFiles) {
    if (-not (Test-Path $file)) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -eq 0) {
    Add-TestResult "Build Outputs" "PASS" "All executables found"
} else {
    Add-TestResult "Build Outputs" "FAIL" "Missing: $($missingFiles -join ', ')"
}

Set-Location $originalLocation

Write-Host ""

# Test 3: API Testing
Write-Host "Phase 3: API Testing" -ForegroundColor Yellow
Write-Host "=" * 30

$apiProject = "..\src\MicrosoftEndpointMonitor.Api\MicrosoftEndpointMonitor.Api.csproj"
if (-not (Test-Path $apiProject)) {
    $apiProject = "src\MicrosoftEndpointMonitor.Api\MicrosoftEndpointMonitor.Api.csproj"
}

if (Test-Path $apiProject) {
    Write-Host "   Starting API (15 seconds)..." -ForegroundColor Gray
    $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $apiProject -PassThru -NoNewWindow
    Start-Sleep -Seconds 15
    
    try {
        $healthResponse = Invoke-WebRequest -Uri "http://localhost:5000/health" -TimeoutSec 10
        if ($healthResponse.StatusCode -eq 200) {
            Add-TestResult "API Health" "PASS" "Status: $($healthResponse.StatusCode)"
        } else {
            Add-TestResult "API Health" "FAIL" "Status: $($healthResponse.StatusCode)"
        }
    } catch {
        Add-TestResult "API Health" "FAIL" "Cannot connect to API"
    }
    
    try {
        $swaggerResponse = Invoke-WebRequest -Uri "http://localhost:5000/swagger" -TimeoutSec 10
        Add-TestResult "Swagger Docs" "PASS" "Documentation accessible"
    } catch {
        Add-TestResult "Swagger Docs" "FAIL" "Cannot access Swagger"
    }
    
    if ($apiProcess -and -not $apiProcess.HasExited) {
        $apiProcess.Kill()
        Write-Host "   API stopped" -ForegroundColor Gray
    }
} else {
    Add-TestResult "API Project" "FAIL" "API project file not found"
}

Write-Host ""

# Test 4: Electron App
Write-Host "Phase 4: Electron App" -ForegroundColor Yellow
Write-Host "=" * 30

$electronPackage = "..\electron-app\package.json"
if (-not (Test-Path $electronPackage)) {
    $electronPackage = "electron-app\package.json"
}

if (Test-Path $electronPackage) {
    Add-TestResult "Electron Package" "PASS" "package.json found"
    
    try {
        $packageData = Get-Content $electronPackage | ConvertFrom-Json
        if ($packageData.main -eq "main.js") {
            Add-TestResult "Electron Config" "PASS" "Properly configured"
        } else {
            Add-TestResult "Electron Config" "FAIL" "main.js not set as entry point"
        }
    } catch {
        Add-TestResult "Electron Config" "FAIL" "Cannot read package.json"
    }
} else {
    Add-TestResult "Electron Package" "FAIL" "package.json not found"
}

Write-Host ""

# Test 5: Network Capabilities
Write-Host "Phase 5: Network Monitoring" -ForegroundColor Yellow
Write-Host "=" * 30

try {
    $connections = Get-NetTCPConnection -State Established
    Add-TestResult "TCP Connections" "PASS" "Found $($connections.Count) connections"
} catch {
    Add-TestResult "TCP Connections" "FAIL" "Cannot enumerate connections"
}

try {
    $processes = Get-Process
    Add-TestResult "Process Access" "PASS" "Found $($processes.Count) processes"
} catch {
    Add-TestResult "Process Access" "FAIL" "Cannot enumerate processes"
}

Write-Host ""

# Results Summary
$totalTests = $TestResults.Count
$passedTests = ($TestResults | Where-Object { $_.Status -eq "PASS" }).Count
$failedTests = ($TestResults | Where-Object { $_.Status -eq "FAIL" }).Count
$totalDuration = ((Get-Date) - $TestStartTime).TotalSeconds

Write-Host "=== TEST SUMMARY ===" -ForegroundColor Cyan
Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Passed: $passedTests" -ForegroundColor Green
Write-Host "Failed: $failedTests" -ForegroundColor Red
Write-Host "Duration: $([math]::Round($totalDuration, 1)) seconds" -ForegroundColor Blue

if ($failedTests -eq 0) {
    Write-Host "🎉 ALL TESTS PASSED!" -ForegroundColor Green
} else {
    Write-Host "❌ $failedTests test(s) failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Failed Tests:" -ForegroundColor Red
    $TestResults | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  • $($_.TestName): $($_.Details)" -ForegroundColor Red
    }
}

if ($GenerateReport) {
    $reportData = @{
        Summary = @{
            TotalTests = $totalTests
            Passed = $passedTests
            Failed = $failedTests
            Duration = $totalDuration
        }
        Results = $TestResults
    }
    
    $reportFile = "test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $reportData | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportFile -Encoding UTF8
    Write-Host "📊 Report saved: $reportFile" -ForegroundColor Blue
}

Write-Host ""
Write-Host "=== Validation Complete ===" -ForegroundColor Cyan

exit $(if ($failedTests -eq 0) { 0 } else { 1 })
