# Quick installer that runs from repository root
# This makes it easier to find the source files

$repoRoot = $PSScriptRoot
$installerScript = Join-Path $repoRoot "installer\scripts\Install-MSMonitor.ps1"

if (Test-Path $installerScript) {
    Write-Host "🚀 Launching Microsoft Endpoint Monitor installer..." -ForegroundColor Green
    & powershell.exe -ExecutionPolicy Bypass -File $installerScript @args
} else {
    Write-Host "❌ Installer script not found at: $installerScript" -ForegroundColor Red
    Write-Host "Please run this from the repository root directory." -ForegroundColor Yellow
    pause
}
