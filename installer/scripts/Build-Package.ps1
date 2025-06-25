# Package Builder for Distribution
# Creates a complete deployment package

param(
    [string]$OutputPath = ".\installer\build\MSMonitor-Setup.zip"
)

Write-Host "📦 Building Microsoft Endpoint Monitor deployment package..." -ForegroundColor Green

# Ensure we're in the right directory
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Build all .NET projects in Release mode
Write-Host "🏗️ Building .NET projects..." -ForegroundColor Cyan
& dotnet clean --configuration Release --verbosity quiet
& dotnet restore --verbosity quiet
& dotnet build --configuration Release --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Build Electron app for distribution
Write-Host "📱 Preparing Electron app..." -ForegroundColor Cyan
Set-Location "electron-app"
& npm install --silent --no-audit --no-fund
Set-Location $repoRoot

# Create temporary package directory
$tempDir = "$env:TEMP\MSMonitor-Package"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

# Copy all necessary files
Write-Host "📁 Copying files..." -ForegroundColor Cyan
Copy-Item "src" "$tempDir\src" -Recurse -Force
Copy-Item "electron-app" "$tempDir\electron-app" -Recurse -Force
Copy-Item "database" "$tempDir\database" -Recurse -Force
Copy-Item "installer" "$tempDir\installer" -Recurse -Force
Copy-Item "README.md" "$tempDir\" -Force
Copy-Item "LICENSE" "$tempDir\" -Force

# Remove unnecessary files from package
Remove-Item "$tempDir\src\**\bin\Debug" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$tempDir\src\**\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$tempDir\electron-app\node_modules\.cache" -Recurse -Force -ErrorAction SilentlyContinue

# Create the package
Write-Host "🗜️ Creating deployment package..." -ForegroundColor Cyan
$packageDir = Split-Path -Parent $OutputPath
if (!(Test-Path $packageDir)) {
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
}

# Create ZIP package
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $OutputPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

# Clean up
Remove-Item $tempDir -Recurse -Force

$packageSize = [Math]::Round((Get-Item $OutputPath).Length / 1MB, 2)
Write-Host "✅ Package created: $OutputPath ($packageSize MB)" -ForegroundColor Green

Write-Host ""
Write-Host "📋 Distribution Instructions:" -ForegroundColor Yellow
Write-Host "1. Extract the ZIP file on target Windows 11 machine" -ForegroundColor White
Write-Host "2. Right-click 'Install-MSMonitor.bat' and run as administrator" -ForegroundColor White
Write-Host "3. Follow the installation prompts" -ForegroundColor White
Write-Host "4. Launch from desktop shortcut when complete" -ForegroundColor White
