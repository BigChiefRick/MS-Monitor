#Requires -RunAsAdministrator
<#
.SYNOPSIS
MS-Monitor - Fixed Build & Test Validation Script

.DESCRIPTION
Simplified and corrected testing script for immediate execution
Tests all components with proper PowerShell syntax

.NOTES
Author: BigChiefRick
Version: 1.1 - Fixed
Purpose: Build & Test Phase Validation
#>

param(
    [switch]$SkipBuild,
    [switch]$ServiceOnly,
    [switch]$GenerateReport,
    [int]$TestDuration = 30
)

# Test configuration
$ErrorActionPreference = "Continue"
$TestResults = @()
$ApiPort = 5000
$TestStartTime = Get-Date

# Function to add test result
function Add-TestResult($TestName, $Status, $Details = "", $Duration = 0) {
    $global:TestResults += [PSCustomObject]@{
        TestName = $TestName
        Status = $Status
        Details = $Details
        Duration = $Duration
        Timestamp = Get-Date
    }
    
    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        default { "Yellow" }
    }
    
    $icon = switch ($Status) {
        "PASS" { "✅" }
        "FAIL" { "❌" }
        default { "⚠️" }
    }
    
    Write-Host "$icon $TestName : $Status" -ForegroundColor $color
    if ($Details) {
        Write-Host "   $Details" -ForegroundColor Gray
    }
}

# Function to measure test execution time
function Measure-TestExecution($ScriptBlock, $TestName) {
    $startTime = Get-Date
    try {
        & $ScriptBlock
        $duration = ((Get-Date) - $startTime).TotalSeconds
        return @{ Success = $true; Duration = $duration }
    } catch {
        $duration = ((Get-Date) - $startTime).TotalSeconds
        Add-TestResult $TestName "FAIL" $_.Exception.Message $duration
        return @{ Success = $false; Duration = $duration }
    }
}

Write-Host "=== MS-Monitor - Build and Test Validation ===" -ForegroundColor Cyan
Write-Host "Test Session Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Blue
Write-Host "Platform: Windows 11 Desktop" -ForegroundColor Blue
Write-Host "Repository: MS-Monitor" -ForegroundColor Blue
Write-Host ""

# Test 1: Prerequisites Verification
Write-Host "Phase 1: Prerequisites Verification" -ForegroundColor Yellow
Write-Host "=" * 50

# Test .NET 8.0 SDK
try {
    $dotnetVersion = & dotnet --version 2>$null
    if ($dotnetVersion -and $dotnetVersion.StartsWith("8.")) {
        Add-TestResult "Prerequisites - .NET 8.0 SDK" "PASS" "Version: $dotnetVersion" 0
    } else {
        Add-TestResult "Prerequisites - .NET 8.0 SDK" "FAIL" "Wrong version or not found: $dotnetVersion" 0
    }
} catch {
    Add-TestResult "Prerequisites - .NET 8.0 SDK" "FAIL" ".NET 8.0 SDK not found" 0
}

# Test Node.js (if not service-only)
if (-not $ServiceOnly) {
    try {
        $nodeVersion = & node --version 2>$null
        if ($nodeVersion) {
            $versionNumber = [Version]$nodeVersion.Substring(1)
            if ($versionNumber -ge [Version]"18.0.0") {
                Add-TestResult "Prerequisites - Node.js" "PASS" "Version: $nodeVersion" 0
            } else {
                Add-TestResult "Prerequisites - Node.js" "FAIL" "Version too old: $nodeVersion" 0
            }
        } else {
            Add-TestResult "Prerequisites - Node.js" "FAIL" "Node.js not found" 0
        }
    } catch {
        Add-TestResult "Prerequisites - Node.js" "FAIL" "Node.js not available" 0
    }
}

# Test Administrator privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if ($currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Add-TestResult "Prerequisites - Admin Privileges" "PASS" "Running as Administrator" 0
} else {
    Add-TestResult "Prerequisites - Admin Privileges" "FAIL" "Administrator privileges required" 0
}

# Test solution file exists
if (Test-Path "..\MicrosoftEndpointMonitor.sln") {
    Add-TestResult "Prerequisites - Solution File" "PASS" "Solution file found" 0
} elseif (Test-Path "MicrosoftEndpointMonitor.sln") {
    Add-TestResult "Prerequisites - Solution File" "PASS" "Solution file found in current directory" 0
} else {
    Add-TestResult "Prerequisites - Solution File" "FAIL" "MicrosoftEndpointMonitor.sln not found" 0
    # Try to find it
    $solutionFiles = Get-ChildItem -Name "*.sln" -Recurse -ErrorAction SilentlyContinue
    if ($solutionFiles) {
        Write-Host "   Found solution files: $($solutionFiles -join ', ')" -ForegroundColor Gray
    }
}

Write-Host ""

# Test 2: Build Process
if (-not $SkipBuild) {
    Write-Host "Phase 2: Build Process Validation" -ForegroundColor Yellow
    Write-Host "=" * 50
    
    # Change to parent directory if solution is there
    $originalLocation = Get-Location
    if (Test-Path "..\MicrosoftEndpointMonitor.sln") {
        Set-Location ".."
    }
    
    # Test NuGet restore
    try {
        Write-Host "   Restoring NuGet packages..." -ForegroundColor Gray
        $output = & dotnet restore --verbosity minimal 2>&1
        if ($LASTEXITCODE -eq 0) {
            Add-TestResult "Build - NuGet Restore" "PASS" "All packages restored successfully" 0
        } else {
            Add-TestResult "Build - NuGet Restore" "FAIL" "NuGet restore failed: $output" 0
        }
    } catch {
        Add-TestResult "Build - NuGet Restore" "FAIL" "Error during restore: $($_.Exception.Message)" 0
    }
    
    # Test solution build
    try {
        Write-Host "   Building solution..." -ForegroundColor Gray
        $output = & dotnet build --configuration Debug --no-restore --verbosity minimal 2>&1
        if ($LASTEXITCODE -eq 0) {
            Add-TestResult "Build - Solution Compilation" "PASS" "All projects built successfully" 0
        } else {
            Add-TestResult "Build - Solution Compilation" "FAIL" "Build failed: $output" 0
        }
    } catch {
        Add-TestResult "Build - Solution Compilation" "FAIL" "Error during build: $($_.Exception.Message)" 0
    }
    
    # Verify build outputs
    $projects = @(
        "src\MicrosoftEndpointMonitor.Shared\bin\Debug\net8.0\MicrosoftEndpointMonitor.Shared.dll",
        "src\MicrosoftEndpointMonitor.Data\bin\Debug\net8.0\MicrosoftEndpointMonitor.Data.dll",
        "src\MicrosoftEndpointMonitor.Service\bin\Debug\net8.0\MicrosoftEndpointMonitor.Service.exe",
        "src\MicrosoftEndpointMonitor.Api\bin\Debug\net8.0\MicrosoftEndpointMonitor.Api.dll"
    )
    
    $buildOutputsExist = $true
    $missingFiles = @()
    foreach ($project in $projects) {
        if (-not (Test-Path $project)) {
            $buildOutputsExist = $false
            $missingFiles += $project
        }
    }
    
    if ($buildOutputsExist) {
        Add-TestResult "Build - Output Verification" "PASS" "All build outputs found" 0
    } else {
        Add-TestResult "Build - Output Verification" "FAIL" "Missing files: $($missingFiles -join ', ')" 0
    }
    
    # Return to original location
    Set-Location $originalLocation
    
    Write-Host ""
}

# Test 3: Service Testing
Write-Host "Phase 3: Service Component Testing" -ForegroundColor Yellow
Write-Host "=" * 50

# Test service executable
$serviceExePath = "..\src\MicrosoftEndpointMonitor.Service\bin\Debug\net8.0\MicrosoftEndpointMonitor.Service.exe"
if (-not (Test-Path $serviceExePath)) {
    $serviceExePath = "src\MicrosoftEndpointMonitor.Service\bin\Debug\net8.0\MicrosoftEndpointMonitor.Service.exe"
}

if (Test-Path $serviceExePath) {
    Add-TestResult "Service - Executable Exists" "PASS" "Service executable found" 0
    
    # Test service can start (brief test)
    try {
        Write-Host "   Testing service startup (5 seconds)..." -ForegroundColor Gray
        $process = Start-Process -FilePath $serviceExePath -PassThru -NoNewWindow -RedirectStandardOutput "$env:TEMP\service_test.log" -RedirectStandardError "$env:TEMP\service_error.log"
        Start-Sleep -Seconds 5
        
        if (-not $process.HasExited) {
            $process.Kill()
            Add-TestResult "Service - Startup Test" "PASS" "Service started successfully" 0
        } else {
            $errorContent = "Service exited immediately"
            if (Test-Path "$env:TEMP\service_error.log") { 
                $errorLog = Get-Content "$env:TEMP\service_error.log" -Raw
                if ($errorLog) {
                    $errorContent = $errorLog
                }
            }
            Add-TestResult "Service - Startup Test" "FAIL" $errorContent 0
        }
    } catch {
        Add-TestResult "Service - Startup Test" "FAIL" "Error starting service: $($_.Exception.Message)" 0
    }
} else {
    Add-TestResult "Service - Executable Exists" "FAIL" "Service executable not found" 0
}

# Test database schema
$schemaPath = "..\database\schema.sql"
if (-not (Test-Path $schemaPath)) {
    $schemaPath = "database\schema.sql"
}

if (Test-Path $schemaPath) {
    Add-TestResult "Service - Database Schema" "PASS" "Database schema file found" 0
    
    # Validate schema content
    try {
        $schemaContent = Get-Content $schemaPath -Raw
        $expectedTables = @("connections", "microsoft_endpoints", "monitoring_sessions", "processes")
        $missingTables = @()
        
        foreach ($table in $expectedTables) {
            if ($schemaContent -notmatch "CREATE TABLE.*$table") {
                $missingTables += $table
            }
        }
        
        if ($missingTables.Count -eq 0) {
            Add-TestResult "Service - Database Schema Validation" "PASS" "All required tables defined" 0
        } else {
            Add-TestResult "Service - Database Schema Validation" "FAIL" "Missing tables: $($missingTables -join ', ')" 0
        }
    } catch {
        Add-TestResult "Service - Database Schema Validation" "FAIL" "Error reading schema: $($_.Exception.Message)" 0
    }
} else {
    Add-TestResult "Service - Database Schema" "FAIL" "Database schema file not found" 0
}

Write-Host ""

# Test 4: API Testing
Write-Host "Phase 4: API Component Testing" -ForegroundColor Yellow
Write-Host "=" * 50

$apiExePath = "..\src\MicrosoftEndpointMonitor.Api\bin\Debug\net8.0\MicrosoftEndpointMonitor.Api.exe"
if (-not (Test-Path $apiExePath)) {
    $apiExePath = "src\MicrosoftEndpointMonitor.Api\bin\Debug\net8.0\MicrosoftEndpointMonitor.Api.exe"
}

if (Test-Path $apiExePath) {
    Add-TestResult "API - Executable Exists" "PASS" "API executable found" 0
    
    # Start API for testing
    Write-Host "   Starting API for integration testing (10 seconds)..." -ForegroundColor Gray
    $apiProcess = $null
    try {
        $apiProjectPath = Split-Path $apiExePath -Parent
        $apiProjectPath = Join-Path (Split-Path $apiProjectPath -Parent) "MicrosoftEndpointMonitor.Api.csproj"
        
        if (Test-Path $apiProjectPath) {
            $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $apiProjectPath, "--configuration", "Debug" -PassThru -NoNewWindow
        } else {
            $apiProcess = Start-Process -FilePath $apiExePath -PassThru -NoNewWindow
        }
        
        Start-Sleep -Seconds 10
        
        # Test health endpoint
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health" -TimeoutSec 10
            if ($response.StatusCode -eq 200) {
                Add-TestResult "API - Health Endpoint" "PASS" "Health check successful" 0
            } else {
                Add-TestResult "API - Health Endpoint" "FAIL" "Health endpoint returned status: $($response.StatusCode)" 0
            }
        } catch {
            Add-TestResult "API - Health Endpoint" "FAIL" "Health endpoint not accessible: $($_.Exception.Message)" 0
        }
        
        # Test Swagger documentation
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/swagger" -TimeoutSec 10
            if ($response.StatusCode -eq 200) {
                Add-TestResult "API - Swagger Documentation" "PASS" "API documentation accessible" 0
            } else {
                Add-TestResult "API - Swagger Documentation" "FAIL" "Swagger returned status: $($response.StatusCode)" 0
            }
        } catch {
            Add-TestResult "API - Swagger Documentation" "FAIL" "Swagger not accessible: $($_.Exception.Message)" 0
        }
        
        # Test dashboard endpoint
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/api/network/dashboard" -TimeoutSec 10
            if ($response.StatusCode -eq 200) {
                # Validate JSON response
                try {
                    $jsonContent = $response.Content | ConvertFrom-Json
                    Add-TestResult "API - Dashboard Endpoint" "PASS" "Dashboard data available" 0
                } catch {
                    Add-TestResult "API - Dashboard Endpoint" "FAIL" "Dashboard returned invalid JSON" 0
                }
            } else {
                Add-TestResult "API - Dashboard Endpoint" "FAIL" "Dashboard endpoint returned status: $($response.StatusCode)" 0
            }
        } catch {
            Add-TestResult "API - Dashboard Endpoint" "FAIL" "Dashboard endpoint not accessible: $($_.Exception.Message)" 0
        }
        
    } catch {
        Add-TestResult "API - Integration Test" "FAIL" "Error during API testing: $($_.Exception.Message)" 0
    } finally {
        if ($apiProcess -and -not $apiProcess.HasExited) {
            $apiProcess.Kill()
            Write-Host "   API process stopped" -ForegroundColor Gray
        }
    }
} else {
    Add-TestResult "API - Executable Exists" "FAIL" "API executable not found" 0
}

Write-Host ""

# Test 5: Electron App Testing
if (-not $ServiceOnly) {
    Write-Host "Phase 5: Electron App Testing" -ForegroundColor Yellow
    Write-Host "=" * 50
    
    $electronPath = "..\electron-app\package.json"
    if (-not (Test-Path $electronPath)) {
        $electronPath = "electron-app\package.json"
    }
    
    if (Test-Path $electronPath) {
        Add-TestResult "Electron - Package Configuration" "PASS" "package.json found" 0
        
        # Validate package.json content
        try {
            $packageContent = Get-Content $electronPath -Raw | ConvertFrom-Json
            
            if ($packageContent.main -eq "main.js") {
                Add-TestResult "Electron - Main Entry Point" "PASS" "main.js configured" 0
            } else {
                Add-TestResult "Electron - Main Entry Point" "FAIL" "main.js not configured properly" 0
            }
            
            if ($packageContent.dependencies -and $packageContent.dependencies.electron) {
                Add-TestResult "Electron - Dependencies" "PASS" "Electron dependency found" 0
            } else {
                Add-TestResult "Electron - Dependencies" "FAIL" "Electron dependency missing" 0
            }
        } catch {
            Add-TestResult "Electron - Package Validation" "FAIL" "Invalid package.json: $($_.Exception.Message)" 0
        }
        
        # Test essential files
        $electronDir = Split-Path $electronPath -Parent
        $electronFiles = @(
            "main.js",
            "renderer.js", 
            "index.html",
            "styles\main.css"
        )
        
        $missingElectronFiles = @()
        foreach ($file in $electronFiles) {
            $fullPath = Join-Path $electronDir $file
            if (-not (Test-Path $fullPath)) {
                $missingElectronFiles += $file
            }
        }
        
        if ($missingElectronFiles.Count -eq 0) {
            Add-TestResult "Electron - Essential Files" "PASS" "All essential files present" 0
        } else {
            Add-TestResult "Electron - Essential Files" "FAIL" "Missing files: $($missingElectronFiles -join ', ')" 0
        }
    } else {
        Add-TestResult "Electron - Package Configuration" "FAIL" "package.json not found" 0
    }
    
    Write-Host ""
}

# Test 6: Integration Testing
Write-Host "Phase 6: Integration Testing" -ForegroundColor Yellow
Write-Host "=" * 50

# Test Windows API dependencies
try {
    # Test network connection enumeration
    $connections = Get-NetTCPConnection -State Established -ErrorAction Stop
    if ($connections.Count -gt 0) {
        Add-TestResult "Integration - Network Connection Enumeration" "PASS" "$($connections.Count) active connections found" 0
    } else {
        Add-TestResult "Integration - Network Connection Enumeration" "FAIL" "No network connections found" 0
    }
} catch {
    Add-TestResult "Integration - Network Connection Enumeration" "FAIL" "Error enumerating connections: $($_.Exception.Message)" 0
}

# Test Microsoft endpoint detection (sample)
try {
    $msConnections = Get-NetTCPConnection -State Established | Where-Object { 
        $_.RemoteAddress -match "^(52\.|40\.|13\.|20\.|51\.|104\.)" 
    }
    Add-TestResult "Integration - Microsoft Endpoint Detection" "PASS" "$($msConnections.Count) potential MS endpoints detected" 0
} catch {
    Add-TestResult "Integration - Microsoft Endpoint Detection" "FAIL" "Error detecting MS endpoints: $($_.Exception.Message)" 0
}

Write-Host ""

# Generate Test Report
Write-Host "Phase 7: Test Results Summary" -ForegroundColor Yellow
Write-Host "=" * 50

$totalTests = $TestResults.Count
$passedTests = ($TestResults | Where-Object { $_.Status -eq "PASS" }).Count
$failedTests = ($TestResults | Where-Object { $_.Status -eq "FAIL" }).Count
$skippedTests = ($TestResults | Where-Object { $_.Status -eq "SKIP" }).Count
$infoTests = ($TestResults | Where-Object { $_.Status -eq "INFO" }).Count
$totalDuration = ((Get-Date) - $TestStartTime).TotalSeconds

Write-Host ""
Write-Host "=== TEST RESULTS SUMMARY ===" -ForegroundColor Cyan
Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Passed: $passedTests" -ForegroundColor Green
Write-Host "Failed: $failedTests" -ForegroundColor Red
Write-Host "Skipped: $skippedTests" -ForegroundColor Yellow
Write-Host "Info: $infoTests" -ForegroundColor Blue
$successRate = if (($totalTests - $infoTests - $skippedTests) -gt 0) { 
    [math]::Round(($passedTests / ($totalTests - $infoTests - $skippedTests)) * 100, 1) 
} else { 
    0 
}
Write-Host "Success Rate: $successRate%" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Yellow" })
Write-Host "Total Duration: $([math]::Round($totalDuration, 1)) seconds" -ForegroundColor Blue
Write-Host ""

# Show failed tests
if ($failedTests -gt 0) {
    Write-Host "Failed Tests:" -ForegroundColor Red
    $TestResults | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  ❌ $($_.TestName): $($_.Details)" -ForegroundColor Red
    }
    Write-Host ""
}

# Overall assessment
if ($failedTests -eq 0) {
    Write-Host "🎉 ALL TESTS PASSED - Build is ready for deployment!" -ForegroundColor Green
} elseif ($failedTests -le 2) {
    Write-Host "⚠️ Minor issues detected - Build mostly successful" -ForegroundColor Yellow
} else {
    Write-Host "❌ Multiple failures detected - Build needs attention" -ForegroundColor Red
}

# Generate report file if requested
if ($GenerateReport) {
    $reportPath = "test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $reportData = @{
        TestSession = @{
            StartTime = $TestStartTime
            EndTime = Get-Date
            Duration = $totalDuration
            Platform = "Windows 11"
            TestType = "Build & Validation"
        }
        Summary = @{
            TotalTests = $totalTests
            Passed = $passedTests
            Failed = $failedTests
            Skipped = $skippedTests
            Info = $infoTests
            SuccessRate = $successRate
        }
        Results = $TestResults
    }
    
    try {
        $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8
        Write-Host "📊 Test report saved to: $reportPath" -ForegroundColor Blue
    } catch {
        Write-Host "⚠️ Could not save test report: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Build and Test Validation Complete ===" -ForegroundColor Cyan

# Exit with appropriate code
exit $(if ($failedTests -eq 0) { 0 } else { 1 })
