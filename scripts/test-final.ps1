param([switch]$GenerateReport)

$results = @()
$start = Get-Date

function Add-Result($name, $status) {
    $global:results += [PSCustomObject]@{Name=$name; Status=$status}
    $icon = if ($status -eq "PASS") { "✅" } else { "❌" }
    $color = if ($status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "$icon $name" -ForegroundColor $color
}

Write-Host "MS-Monitor Build Test" -ForegroundColor Cyan

# Check .NET
try {
    $dotnetPath = Get-Command dotnet -ErrorAction Stop
    $version = & dotnet --version 2>$null
    if ($version -and $version.StartsWith("8.")) {
        Add-Result "DotNet 8.0" "PASS"
    } else {
        Add-Result "DotNet 8.0" "FAIL"
        Write-Host "   Found version: $version (need 8.x)" -ForegroundColor Yellow
    }
} catch {
    Add-Result "DotNet 8.0" "FAIL"
    Write-Host "   .NET not found. Install from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
}

# Check Node.js
try {
    $nodeVersion = & node --version 2>$null
    if ($nodeVersion) {
        Add-Result "Node.js" "PASS"
    } else {
        Add-Result "Node.js" "FAIL"
    }
} catch {
    Add-Result "Node.js" "FAIL"
    Write-Host "   Node.js not found" -ForegroundColor Yellow
}

# Check Admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    Add-Result "Admin Rights" "PASS"
} else {
    Add-Result "Admin Rights" "FAIL"
    Write-Host "   Run PowerShell as Administrator" -ForegroundColor Yellow
}

# Find solution
$solutionFound = $false
if (Test-Path "..\MicrosoftEndpointMonitor.sln") {
    Set-Location ".."
    $solutionFound = $true
} elseif (Test-Path "MicrosoftEndpointMonitor.sln") {
    $solutionFound = $true
} else {
    # Look for any .sln file
    $slnFiles = Get-ChildItem -Name "*.sln" -Recurse -ErrorAction SilentlyContinue
    if ($slnFiles) {
        Write-Host "   Found solution files: $($slnFiles -join ', ')" -ForegroundColor Yellow
        if ($slnFiles[0]) {
            $slnDir = Split-Path $slnFiles[0] -Parent
            if ($slnDir) { Set-Location $slnDir }
            $solutionFound = $true
        }
    }
}

if ($solutionFound) {
    Add-Result "Solution File" "PASS"
} else {
    Add-Result "Solution File" "FAIL"
    Write-Host "   No .sln file found" -ForegroundColor Yellow
}

# Try to build if we have .NET and solution
if ($solutionFound -and (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Building..." -ForegroundColor Yellow
    
    try {
        & dotnet restore 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Add-Result "NuGet Restore" "PASS"
        } else {
            Add-Result "NuGet Restore" "FAIL"
        }
    } catch {
        Add-Result "NuGet Restore" "FAIL"
    }
    
    try {
        & dotnet build --configuration Debug 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Add-Result "Build" "PASS"
        } else {
            Add-Result "Build" "FAIL"
        }
    } catch {
        Add-Result "Build" "FAIL"
    }
} else {
    Add-Result "Build" "SKIP"
    Write-Host "   Skipped - missing .NET or solution" -ForegroundColor Yellow
}

# Check build outputs
$serviceExe = "src\MicrosoftEndpointMonitor.Service\bin\Debug\net8.0\MicrosoftEndpointMonitor.Service.exe"
$apiDll = "src\MicrosoftEndpointMonitor.Api\bin\Debug\net8.0\MicrosoftEndpointMonitor.Api.dll"

if (Test-Path $serviceExe) {
    Add-Result "Service Built" "PASS"
} else {
    Add-Result "Service Built" "FAIL"
}

if (Test-Path $apiDll) {
    Add-Result "API Built" "PASS"
} else {
    Add-Result "API Built" "FAIL"
}

$passed = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$skipped = ($results | Where-Object { $_.Status -eq "SKIP" }).Count
$duration = ((Get-Date) - $start).TotalSeconds

Write-Host ""
Write-Host "RESULTS:" -ForegroundColor Cyan
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor Red
Write-Host "Skipped: $skipped" -ForegroundColor Yellow
Write-Host "Duration: $([math]::Round($duration, 1)) seconds" -ForegroundColor Blue

if ($failed -eq 0 -and $passed -gt 5) {
    Write-Host "🎉 BUILD SUCCESSFUL!" -ForegroundColor Green
} elseif ($failed -gt 0) {
    Write-Host "❌ BUILD ISSUES DETECTED" -ForegroundColor Red
} else {
    Write-Host "⚠️ PARTIAL SUCCESS" -ForegroundColor Yellow
}

if ($GenerateReport) {
    $report = @{
        Summary = @{
            Passed = $passed
            Failed = $failed
            Skipped = $skipped
            Duration = $duration
            Timestamp = (Get-Date).ToString()
        }
        Results = $results
    }
    $report | ConvertTo-Json -Depth 3 | Out-File "test-results.json" -Encoding UTF8
    Write-Host "Report saved: test-results.json" -ForegroundColor Blue
}

Write-Host ""
if ($failed -gt 0) {
    Write-Host "NEXT STEPS:" -ForegroundColor Yellow
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "1. Install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    }
    if (-not $isAdmin) {
        Write-Host "2. Run PowerShell as Administrator" -ForegroundColor Cyan
    }
    if (-not $solutionFound) {
        Write-Host "3. Navigate to the correct project directory" -ForegroundColor Cyan
    }
} else {
    Write-Host "Ready to proceed with installation!" -ForegroundColor Green
    Write-Host "Next: Run .\install.ps1 to deploy as Windows Service" -ForegroundColor Cyan
}

exit $failed