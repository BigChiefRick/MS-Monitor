param([switch]$GenerateReport)

$results = @()
$start = Get-Date

function Test-Item($name, $test) {
    try {
        $result = & $test
        if ($result) {
            Write-Host "✅ $name" -ForegroundColor Green
            $global:results += @{Name=$name; Status="PASS"}
        } else {
            Write-Host "❌ $name" -ForegroundColor Red  
            $global:results += @{Name=$name; Status="FAIL"}
        }
    } catch {
        Write-Host "❌ $name" -ForegroundColor Red
        $global:results += @{Name=$name; Status="FAIL"}
    }
}

Write-Host "MS-Monitor Build Test" -ForegroundColor Cyan

Test-Item "DotNet 8.0" { (dotnet --version).StartsWith("8.") }
Test-Item "Node.js" { node --version }
Test-Item "Admin Rights" { ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }

if (Test-Path "..\MicrosoftEndpointMonitor.sln") { cd .. }
Test-Item "Solution File" { Test-Path "MicrosoftEndpointMonitor.sln" }

Write-Host "Building..." -ForegroundColor Yellow
dotnet restore | Out-Null
dotnet build --configuration Debug | Out-Null

Test-Item "Service Built" { Test-Path "src\MicrosoftEndpointMonitor.Service\bin\Debug\net8.0\MicrosoftEndpointMonitor.Service.exe" }
Test-Item "API Built" { Test-Path "src\MicrosoftEndpointMonitor.Api\bin\Debug\net8.0\MicrosoftEndpointMonitor.Api.dll" }

Write-Host "Testing API..." -ForegroundColor Yellow
$api = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src\MicrosoftEndpointMonitor.Api" -PassThru -NoNewWindow
Start-Sleep -Seconds 15

Test-Item "API Health" { 
    try { 
        (Invoke-WebRequest -Uri "http://localhost:5000/health" -TimeoutSec 5).StatusCode -eq 200 
    } catch { 
        $false 
    } 
}

if ($api -and -not $api.HasExited) { $api.Kill() }

$passed = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$duration = ((Get-Date) - $start).TotalSeconds

Write-Host ""
Write-Host "Results: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "Duration: $([math]::Round($duration, 1)) seconds" -ForegroundColor Blue

if ($GenerateReport) {
    @{
        Passed = $passed
        Failed = $failed  
        Duration = $duration
        Results = $results
    } | ConvertTo-Json | Out-File "test-results.json"
    Write-Host "Report: test-results.json" -ForegroundColor Blue
}

exit $failed