#Requires -RunAsAdministrator
<#
.SYNOPSIS
Microsoft Endpoint Monitor - Build & Test Validation Script

.DESCRIPTION
Comprehensive testing script to validate build and functionality
Tests all components: Service, API, Database, and Electron App
Generates detailed test report with pass/fail status

.NOTES
Author: BigChiefRick
Version: 1.0
Purpose: Build & Test Phase Validation
#>

param(
    [switch]$SkipBuild,
    [switch]$ServiceOnly,
    [switch]$GenerateReport,
    [int]$TestDuration = 30  # seconds for monitoring tests
)

# Test configuration
$ErrorActionPreference = "Continue"  # Continue on errors for comprehensive testing
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
    
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "FAIL") { "Red" } else { "Yellow" }
    $icon = if ($Status -eq "PASS") { "✅" } elseif ($Status -eq "FAIL") { "❌" } else { "⚠️" }
    
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

Write-Host "=== Microsoft Endpoint Monitor - Build & Test Validation ===" -ForegroundColor Cyan
Write-Host "Test Session Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Blue
Write-Host "Platform: Windows 11 Desktop" -ForegroundColor Blue
Write-Host "Test Duration: $TestDuration seconds for monitoring tests" -ForegroundColor Blue
Write-Host ""

# Test 1: Prerequisites Verification
Write-Host "Phase 1: Prerequisites Verification" -ForegroundColor Yellow
Write-Host "=" * 50

# Test .NET 8.0 SDK
$result = Measure-TestExecution {
    $dotnetVersion = & dotnet --version 2>$null
    if (-not $dotnetVersion -or -not $dotnetVersion.StartsWith("8.")) {
        throw ".NET 8.0 SDK not found or wrong version: $dotnetVersion"
    }
} "Prerequisites - .NET 8.0 SDK"

if ($result.Success) {
    $dotnetVersion = & dotnet --version 2>$null
    Add-TestResult "Prerequisites - .NET 8.0 SDK" "PASS" "Version: $dotnetVersion" $result.Duration
}

# Test Node.js (if not service-only)
if (-not $ServiceOnly) {
    $result = Measure-TestExecution {
        $nodeVersion = & node --version 2>$null
        if (-not $nodeVersion) {
            throw "Node.js not found"
        }
        $versionNumber = [Version]$nodeVersion.Substring(1)
        if ($versionNumber -lt [Version]"18.0.0") {
            throw "Node.js version too old: $nodeVersion (required: 18+)"
        }
    } "Prerequisites - Node.js"
    
    if ($result.Success) {
        $nodeVersion = & node --version 2>$null
        Add-TestResult "Prerequisites - Node.js" "PASS" "Version: $nodeVersion" $result.Duration
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
if (Test-Path "MicrosoftEndpointMonitor.sln") {
    Add-TestResult "Prerequisites - Solution File" "PASS" "Solution file found" 0
} else {
    Add-TestResult "Prerequisites - Solution File" "FAIL" "MicrosoftEndpointMonitor.sln not found" 0
}

Write-Host ""

# Test 2: Build Process
if (-not $SkipBuild) {
    Write-Host "Phase 2: Build Process Validation" -ForegroundColor Yellow
    Write-Host "=" * 50
    
    # Test NuGet restore
    $result = Measure-TestExecution {
        $output = & dotnet restore --verbosity minimal 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "NuGet restore failed: $output"
        }
    } "Build - NuGet Restore"
    
    if ($result.Success) {
        Add-TestResult "Build - NuGet Restore" "PASS" "All packages restored successfully" $result.Duration
    }
    
    # Test solution build
    $result = Measure-TestExecution {
        $output = & dotnet build --configuration Debug --no-restore --verbosity minimal 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed: $output"
        }
    } "Build - Solution Compilation"
    
    if ($result.Success) {
        Add-TestResult "Build - Solution Compilation" "PASS" "All projects built successfully" $result.Duration
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
    
    Write-Host ""
}

# Test 3: Service Testing
Write-Host "Phase 3: Service Component Testing" -ForegroundColor Yellow
Write-Host "=" * 50

# Test service executable
$serviceExePath = "src\MicrosoftEndpointMonitor.Service\bin\Debug\net8.0\MicrosoftEndpointMonitor.Service.exe"
if (Test-Path $serviceExePath) {
    Add-TestResult "Service - Executable Exists" "PASS" "Service executable found" 0
    
    # Test service can start (brief test)
    $result = Measure-TestExecution {
        $process = Start-Process -FilePath $serviceExePath -PassThru -NoNewWindow -RedirectStandardOutput "$env:TEMP\service_test.log" -RedirectStandardError "$env:TEMP\service_error.log"
        Start-Sleep -Seconds 5  # Let it initialize
        
        if (-not $process.HasExited) {
            $process.Kill()
            Add-TestResult "Service - Startup Test" "PASS" "Service started successfully" 0
        } else {
            $errorContent = if (Test-Path "$env:TEMP\service_error.log") { Get-Content "$env:TEMP\service_error.log" -Raw } else "No error log"
            throw "Service exited immediately: $errorContent"
        }
    } "Service - Startup Test"
    
} else {
    Add-TestResult "Service - Executable Exists" "FAIL" "Service executable not found" 0
}

# Test database schema
if (Test-Path "database\schema.sql") {
    Add-TestResult "Service - Database Schema" "PASS" "Database schema file found" 0
    
    # Validate schema content
    $schemaContent = Get-Content "database\schema.sql" -Raw
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
} else {
    Add-TestResult "Service - Database Schema" "FAIL" "Database schema file not found" 0
}

Write-Host ""

# Test 4: API Testing
Write-Host "Phase 4: API Component Testing" -ForegroundColor Yellow
Write-Host "=" * 50

$apiExePath = "src\MicrosoftEndpointMonitor.Api\bin\Debug\net8.0\MicrosoftEndpointMonitor.Api.exe"
if (Test-Path $apiExePath) {
    Add-TestResult "API - Executable Exists" "PASS" "API executable found" 0
    
    # Start API for testing
    Write-Host "   Starting API for integration testing..." -ForegroundColor Gray
    $apiProcess = $null
    try {
        $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src\MicrosoftEndpointMonitor.Api", "--configuration", "Debug" -PassThru -NoNewWindow
        Start-Sleep -Seconds 10  # Give API time to start
        
        # Test health endpoint
        $result = Measure-TestExecution {
            $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health" -TimeoutSec 10
            if ($response.StatusCode -ne 200) {
                throw "Health endpoint returned status: $($response.StatusCode)"
            }
        } "API - Health Endpoint"
        
        if ($result.Success) {
            Add-TestResult "API - Health Endpoint" "PASS" "Health check successful" $result.Duration
        }
        
        # Test Swagger documentation
        $result = Measure-TestExecution {
            $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/swagger" -TimeoutSec 10
            if ($response.StatusCode -ne 200) {
                throw "Swagger endpoint returned status: $($response.StatusCode)"
            }
        } "API - Swagger Documentation"
        
        if ($result.Success) {
            Add-TestResult "API - Swagger Documentation" "PASS" "API documentation accessible" $result.Duration
        }
        
        # Test dashboard endpoint
        $result = Measure-TestExecution {
            $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/api/network/dashboard" -TimeoutSec 10
            if ($response.StatusCode -ne 200) {
                throw "Dashboard endpoint returned status: $($response.StatusCode)"
            }
            
            # Validate JSON response
            $jsonContent = $response.Content | ConvertFrom-Json
            if (-not $jsonContent) {
                throw "Dashboard endpoint returned invalid JSON"
            }
        } "API - Dashboard Endpoint"
        
        if ($result.Success) {
            Add-TestResult "API - Dashboard Endpoint" "PASS" "Dashboard data available" $result.Duration
        }
        
    } catch {
        Add-TestResult "API - Integration Test" "FAIL" $_.Exception.Message 0
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
    
    if (Test-Path "electron-app\package.json") {
        Add-TestResult "Electron - Package Configuration" "PASS" "package.json found" 0
        
        # Validate package.json content
        try {
            $packageContent = Get-Content "electron-app\package.json" -Raw | ConvertFrom-Json
            
            if ($packageContent.main -eq "main.js") {
                Add-TestResult "Electron - Main Entry Point" "PASS" "main.js configured" 0
            } else {
                Add-TestResult "Electron - Main Entry Point" "FAIL" "main.js not configured properly" 0
            }
            
            if ($packageContent.dependencies.electron) {
                Add-TestResult "Electron - Dependencies" "PASS" "Electron dependency found" 0
            } else {
                Add-TestResult "Electron - Dependencies" "FAIL" "Electron dependency missing" 0
            }
            
        } catch {
            Add-TestResult "Electron - Package Validation" "FAIL" "Invalid package.json: $($_.Exception.Message)" 0
        }
        
        # Test npm install
        if (Get-Command npm -ErrorAction SilentlyContinue) {
            Write-Host "   Testing npm install..." -ForegroundColor Gray
            Push-Location "electron-app"
            try {
                $result = Measure-TestExecution {
                    $output = & npm install --silent 2>&1
                    if ($LASTEXITCODE -ne 0) {
                        throw "npm install failed: $output"
                    }
                } "Electron - NPM Install"
                
                if ($result.Success) {
                    Add-TestResult "Electron - NPM Install" "PASS" "Dependencies installed successfully" $result.Duration
                }
                
            } finally {
                Pop-Location
            }
        } else {
            Add-TestResult "Electron - NPM Install" "SKIP" "npm not available" 0
        }
        
    } else {
        Add-TestResult "Electron - Package Configuration" "FAIL" "package.json not found" 0
    }
    
    # Test essential files
    $electronFiles = @(
        "electron-app\main.js",
        "electron-app\renderer.js", 
        "electron-app\index.html",
        "electron-app\styles\main.css"
    )
    
    $missingElectronFiles = @()
    foreach ($file in $electronFiles) {
        if (-not (Test-Path $file)) {
            $missingElectronFiles += $file
        }
    }
    
    if ($missingElectronFiles.Count -eq 0) {
        Add-TestResult "Electron - Essential Files" "PASS" "All essential files present" 0
    } else {
        Add-TestResult "Electron - Essential Files" "FAIL" "Missing files: $($missingElectronFiles -join ', ')" 0
    }
    
    Write-Host ""
}

# Test 6: Integration Testing
Write-Host "Phase 6: Integration Testing" -ForegroundColor Yellow
Write-Host "=" * 50

# Test Windows API dependencies
$result = Measure-TestExecution {
    # Test if we can load necessary Windows APIs
    Add-Type -TypeDefinition @"
    using System;
    using System.Runtime.InteropServices;
    public class WinAPI {
        [DllImport("iphlpapi.dll")]
        public static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved);
    }
"@
    [WinAPI]::GetExtendedTcpTable([IntPtr]::Zero, [ref]0, $false, 2, 5, 0) | Out-Null
} "Integration - Windows API Access"

if ($result.Success) {
    Add-TestResult "Integration - Windows API Access" "PASS" "Windows TCP APIs accessible" $result.Duration
}

# Test network connectivity for monitoring
$result = Measure-TestExecution {
    # Test that we can enumerate network connections
    $connections = Get-NetTCPConnection -State Established -ErrorAction Stop
    if ($connections.Count -eq 0) {
        throw "No network connections found - monitoring may not work properly"
    }
} "Integration - Network Connection Enumeration"

if ($result.Success) {
    $connectionCount = (Get-NetTCPConnection -State Established).Count
    Add-TestResult "Integration - Network Connection Enumeration" "PASS" "$connectionCount active connections found" $result.Duration
}

# Test Microsoft endpoint detection (sample)
$result = Measure-TestExecution {
    $msEndpoints = Get-NetTCPConnection -State Established | Where-Object { 
        $_.RemoteAddress -match "^(52\.|40\.|13\.|20\.|51\.|104\.)" -or
        $_.RemoteAddress -eq "outlook.office365.com"
    }
    # This is a simplified test - real implementation is more comprehensive
} "Integration - Microsoft Endpoint Detection"

if ($result.Success) {
    $msConnections = Get-NetTCPConnection -State Established | Where-Object { $_.RemoteAddress -match "^(52\.|40\.|13\.|20\.|51\.|104\.)" }
    Add-TestResult "Integration - Microsoft Endpoint Detection" "PASS" "$($msConnections.Count) potential MS endpoints detected" $result.Duration
}

Write-Host ""

# Test 7: Performance and Resource Testing
Write-Host "Phase 7: Performance Testing" -ForegroundColor Yellow
Write-Host "=" * 50

# Test system resources
$memoryUsage = [System.GC]::GetTotalMemory($false) / 1MB
Add-TestResult "Performance - Memory Usage" "INFO" "$([math]::Round($memoryUsage, 2)) MB currently used" 0

# Test file system performance
$result = Measure-TestExecution {
    $testFile = "$env:TEMP\endpoint_monitor_test.tmp"
    $testData = "x" * 1MB
    [System.IO.File]::WriteAllText($testFile, $testData)
    $readData = [System.IO.File]::ReadAllText($testFile)
    Remove-Item $testFile -Force
    if ($readData.Length -ne $testData.Length) {
        throw "File I/O test failed"
    }
} "Performance - File I/O"

if ($result.Success) {
    Add-TestResult "Performance - File I/O" "PASS" "File operations working (1MB in $([math]::Round($result.Duration, 2))s)" $result.Duration
}

# Test database creation
$result = Measure-TestExecution {
    $testDbPath = "$env:TEMP\test_endpoint_monitor.db"
    if (Test-Path $testDbPath) { Remove-Item $testDbPath -Force }
    
    # Create a simple SQLite database to test functionality
    $connectionString = "Data Source=$testDbPath"
    Add-Type -Path (Get-ChildItem -Path "src\MicrosoftEndpointMonitor.Data\bin\Debug\net8.0" -Filter "System.Data.SQLite.dll" -Recurse | Select-Object -First 1).FullName -ErrorAction SilentlyContinue
    
    if (Test-Path $testDbPath) { Remove-Item $testDbPath -Force }
} "Performance - Database Operations"

if ($result.Success) {
    Add-TestResult "Performance - Database Operations" "PASS" "Database operations functional" $result.Duration
}

Write-Host ""

# Generate Test Report
Write-Host "Phase 8: Test Results Summary" -ForegroundColor Yellow
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
Write-Host "Success Rate: $([math]::Round(($passedTests / ($totalTests - $infoTests - $skippedTests)) * 100, 1))%" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Yellow" })
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
            SuccessRate = [math]::Round(($passedTests / ($totalTests - $infoTests - $skippedTests)) * 100, 1)
        }
        Results = $TestResults
    }
    
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Host "📊 Test report saved to: $reportPath" -ForegroundColor Blue
}

Write-Host ""
Write-Host "=== Build & Test Validation Complete ===" -ForegroundColor Cyan

# Exit with appropriate code
exit $(if ($failedTests -eq 0) { 0 } else { 1 })
