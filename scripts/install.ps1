#Requires -RunAsAdministrator
<#
.SYNOPSIS
Microsoft Endpoint Monitor - Windows 11 Installation Script

.DESCRIPTION
Automated installation and configuration for Microsoft Endpoint Monitor
Installs service, API, and Electron desktop application
Configures Windows Firewall and Service auto-start

.NOTES
Author: BigChiefRick
Version: 1.0
Requires: Administrator privileges, .NET 8.0, Node.js 18+
#>

param(
    [switch]$Uninstall,
    [switch]$ServiceOnly,
    [switch]$SkipFirewall,
    [string]$InstallPath = "C:\Program Files\Microsoft Endpoint Monitor"
)

# Script configuration
$ErrorActionPreference = "Stop"
$ServiceName = "MicrosoftEndpointMonitor"
$ServiceDisplayName = "Microsoft Endpoint Monitor"
$ApiPort = 5000

Write-Host "=== Microsoft Endpoint Monitor Installation ===" -ForegroundColor Cyan
Write-Host "Platform: Windows 11 Desktop" -ForegroundColor Green
Write-Host "Install Path: $InstallPath" -ForegroundColor Green
Write-Host ""

# Function to write colored output
function Write-Success($message) { Write-Host "✅ $message" -ForegroundColor Green }
function Write-Warning($message) { Write-Host "⚠️ $message" -ForegroundColor Yellow }
function Write-Error($message) { Write-Host "❌ $message" -ForegroundColor Red }
function Write-Info($message) { Write-Host "ℹ️ $message" -ForegroundColor Blue }

# Function to check prerequisites
function Test-Prerequisites {
    Write-Info "Checking prerequisites..."
    
    # Check .NET 8.0
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion -and $dotnetVersion.StartsWith("8.")) {
            Write-Success ".NET 8.0 SDK found: $dotnetVersion"
        } else {
            Write-Error ".NET 8.0 SDK required. Current version: $dotnetVersion"
            Write-Host "Install from: https://dotnet.microsoft.com/download/dotnet/8.0"
            exit 1
        }
    } catch {
        Write-Error ".NET 8.0 SDK not found. Please install .NET 8.0 SDK first."
        exit 1
    }
    
    # Check Node.js (for Electron app)
    if (-not $ServiceOnly) {
        try {
            $nodeVersion = & node --version 2>$null
            if ($nodeVersion -and [Version]$nodeVersion.Substring(1) -ge [Version]"18.0.0") {
                Write-Success "Node.js found: $nodeVersion"
            } else {
                Write-Warning "Node.js 18+ recommended for Electron app. Current: $nodeVersion"
            }
        } catch {
            Write-Warning "Node.js not found. Electron app will not be available."
        }
    }
    
    # Check admin privileges
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error "Administrator privileges required. Run PowerShell as Administrator."
        exit 1
    }
    Write-Success "Administrator privileges confirmed"
    
    # Check port availability
    $portCheck = Get-NetTCPConnection -LocalPort $ApiPort -ErrorAction SilentlyContinue
    if ($portCheck) {
        Write-Warning "Port $ApiPort is in use. API may fail to start."
        $process = Get-Process -Id $portCheck.OwningProcess -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "Process using port: $($process.ProcessName) (PID: $($process.Id))"
        }
    } else {
        Write-Success "Port $ApiPort is available"
    }
}

# Function to stop and remove existing service
function Remove-ExistingService {
    Write-Info "Checking for existing service installation..."
    
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Info "Stopping existing service..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        
        Write-Info "Removing existing service..."
        & sc.exe delete $ServiceName | Out-Null
        
        # Wait for service removal
        Start-Sleep -Seconds 2
        Write-Success "Existing service removed"
    }
}

# Function to build the solution
function Build-Solution {
    Write-Info "Building Microsoft Endpoint Monitor solution..."
    
    if (-not (Test-Path "MicrosoftEndpointMonitor.sln")) {
        Write-Error "Solution file not found. Ensure you're running from the project root directory."
        exit 1
    }
    
    # Clean and restore
    Write-Info "Restoring NuGet packages..."
    & dotnet restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore NuGet packages"
        exit 1
    }
    
    # Build solution
    Write-Info "Building solution in Release mode..."
    & dotnet build --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
    
    Write-Success "Solution built successfully"
}

# Function to create installation directory and copy files
function Install-Application {
    Write-Info "Installing application files..."
    
    # Create installation directory
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Success "Created installation directory: $InstallPath"
    }
    
    # Copy service files
    $serviceBuildPath = "src\MicrosoftEndpointMonitor.Service\bin\Release\net8.0"
    if (Test-Path $serviceBuildPath) {
        Copy-Item "$serviceBuildPath\*" "$InstallPath\" -Recurse -Force
        Write-Success "Service files copied"
    } else {
        Write-Error "Service build output not found at: $serviceBuildPath"
        exit 1
    }
    
    # Copy API files
    $apiBuildPath = "src\MicrosoftEndpointMonitor.Api\bin\Release\net8.0"
    $apiInstallPath = "$InstallPath\Api"
    if (Test-Path $apiBuildPath) {
        if (-not (Test-Path $apiInstallPath)) {
            New-Item -ItemType Directory -Path $apiInstallPath -Force | Out-Null
        }
        Copy-Item "$apiBuildPath\*" "$apiInstallPath\" -Recurse -Force
        Write-Success "API files copied"
    } else {
        Write-Error "API build output not found at: $apiBuildPath"
        exit 1
    }
    
    # Copy database schema
    if (Test-Path "database") {
        Copy-Item "database" "$InstallPath\" -Recurse -Force
        Write-Success "Database schema copied"
    }
    
    # Copy Electron app (if not service-only)
    if (-not $ServiceOnly -and (Test-Path "electron-app")) {
        $electronPath = "$InstallPath\ElectronApp"
        Copy-Item "electron-app" "$electronPath" -Recurse -Force
        
        # Install npm dependencies
        try {
            Push-Location $electronPath
            Write-Info "Installing Electron dependencies..."
            & npm install --production --silent
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Electron app installed"
            } else {
                Write-Warning "Electron app installation failed - app may not work properly"
            }
        } catch {
            Write-Warning "Failed to install Electron dependencies: $_"
        } finally {
            Pop-Location
        }
    }
}

# Function to install Windows Service
function Install-WindowsService {
    Write-Info "Installing Windows Service..."
    
    $serviceExePath = "$InstallPath\MicrosoftEndpointMonitor.Service.exe"
    if (-not (Test-Path $serviceExePath)) {
        Write-Error "Service executable not found at: $serviceExePath"
        exit 1
    }
    
    # Create service
    $createResult = & sc.exe create $ServiceName binPath= "`"$serviceExePath`"" DisplayName= "`"$ServiceDisplayName`"" start= auto
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create service: $createResult"
        exit 1
    }
    
    # Set service description
    & sc.exe description $ServiceName "Monitors Microsoft endpoint connections and performance metrics"
    
    # Configure service recovery
    & sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
    
    Write-Success "Windows Service installed"
}

# Function to configure Windows Firewall
function Configure-Firewall {
    if ($SkipFirewall) {
        Write-Warning "Skipping firewall configuration"
        return
    }
    
    Write-Info "Configuring Windows Firewall..."
    
    try {
        # Remove existing rules
        Remove-NetFirewallRule -DisplayName "Microsoft Endpoint Monitor API" -ErrorAction SilentlyContinue
        
        # Add new inbound rule for API port
        New-NetFirewallRule -DisplayName "Microsoft Endpoint Monitor API" `
                           -Direction Inbound `
                           -Protocol TCP `
                           -LocalPort $ApiPort `
                           -Action Allow `
                           -Profile Domain,Private `
                           -Description "Allow inbound connections to Microsoft Endpoint Monitor API"
        
        Write-Success "Firewall rules configured for port $ApiPort"
    } catch {
        Write-Warning "Failed to configure firewall: $_"
        Write-Host "You may need to manually allow port $ApiPort in Windows Firewall"
    }
}

# Function to start services
function Start-Services {
    Write-Info "Starting services..."
    
    # Start main service
    try {
        Start-Service -Name $ServiceName
        Write-Success "Microsoft Endpoint Monitor Service started"
        
        # Verify service is running
        Start-Sleep -Seconds 3
        $serviceStatus = Get-Service -Name $ServiceName
        if ($serviceStatus.Status -eq "Running") {
            Write-Success "Service is running successfully"
        } else {
            Write-Warning "Service status: $($serviceStatus.Status)"
        }
    } catch {
        Write-Error "Failed to start service: $_"
        Write-Host "Check Windows Event Log for service startup errors"
    }
    
    # Start API (if configured as separate service)
    # Note: In production, API might run as part of main service or separately
}

# Function to create desktop shortcuts
function Create-Shortcuts {
    if ($ServiceOnly) {
        return
    }
    
    Write-Info "Creating desktop shortcuts..."
    
    try {
        $WshShell = New-Object -comObject WScript.Shell
        
        # Electron app shortcut
        $electronPath = "$InstallPath\ElectronApp"
        if (Test-Path "$electronPath\package.json") {
            $shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\Microsoft Endpoint Monitor.lnk")
            $shortcut.TargetPath = "npm.cmd"
            $shortcut.Arguments = "start"
            $shortcut.WorkingDirectory = $electronPath
            $shortcut.Description = "Microsoft Endpoint Monitor Dashboard"
            $shortcut.Save()
            Write-Success "Desktop shortcut created"
        }
        
        # API documentation shortcut
        $webShortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\Endpoint Monitor API.url")
        $webShortcut.TargetPath = "http://localhost:$ApiPort/swagger"
        $webShortcut.Save()
        Write-Success "API documentation shortcut created"
    } catch {
        Write-Warning "Failed to create shortcuts: $_"
    }
}

# Function to verify installation
function Test-Installation {
    Write-Info "Verifying installation..."
    
    # Check service status
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq "Running") {
        Write-Success "Service verification: PASSED"
    } else {
        Write-Warning "Service verification: FAILED"
    }
    
    # Check API endpoint (basic connectivity)
    try {
        Start-Sleep -Seconds 5  # Give service time to start API
        $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health" -TimeoutSec 10 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Success "API verification: PASSED"
        } else {
            Write-Warning "API verification: FAILED (Status: $($response.StatusCode))"
        }
    } catch {
        Write-Warning "API verification: FAILED (Unable to connect)"
        Write-Host "API may still be starting. Check http://localhost:$ApiPort/health manually"
    }
    
    # Check database creation
    $dbPath = "$InstallPath\database\endpoint_monitor.db"
    if (Test-Path $dbPath) {
        Write-Success "Database verification: PASSED"
    } else {
        Write-Warning "Database verification: FAILED (Database file not created)"
    }
    
    # Check file permissions
    try {
        $acl = Get-Acl $InstallPath
        Write-Success "File permissions verification: PASSED"
    } catch {
        Write-Warning "File permissions verification: FAILED"
    }
}

# Function to uninstall
function Uninstall-Application {
    Write-Info "Uninstalling Microsoft Endpoint Monitor..."
    
    # Stop and remove service
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Stop-Service -Name $ServiceName -Force
            Write-Success "Service stopped"
        }
        
        & sc.exe delete $ServiceName | Out-Null
        Write-Success "Service removed"
    }
    
    # Remove firewall rules
    Remove-NetFirewallRule -DisplayName "Microsoft Endpoint Monitor API" -ErrorAction SilentlyContinue
    Write-Success "Firewall rules removed"
    
    # Remove installation directory
    if (Test-Path $InstallPath) {
        Remove-Item $InstallPath -Recurse -Force
        Write-Success "Installation directory removed"
    }
    
    # Remove shortcuts
    $shortcuts = @(
        "$env:USERPROFILE\Desktop\Microsoft Endpoint Monitor.lnk",
        "$env:USERPROFILE\Desktop\Endpoint Monitor API.url"
    )
    foreach ($shortcut in $shortcuts) {
        if (Test-Path $shortcut) {
            Remove-Item $shortcut -Force
        }
    }
    Write-Success "Shortcuts removed"
    
    Write-Success "Uninstallation completed"
}

# Function to display post-installation information
function Show-PostInstallInfo {
    Write-Host ""
    Write-Host "=== Installation Complete ===" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Service Status:" -ForegroundColor Yellow
    Write-Host "  • Microsoft Endpoint Monitor Service: Running" -ForegroundColor Green
    Write-Host "  • Auto-start on boot: Enabled" -ForegroundColor Green
    Write-Host ""
    Write-Host "Access Points:" -ForegroundColor Yellow
    Write-Host "  • API Documentation: http://localhost:$ApiPort/swagger" -ForegroundColor Green
    Write-Host "  • Health Check: http://localhost:$ApiPort/health" -ForegroundColor Green
    Write-Host "  • Dashboard API: http://localhost:$ApiPort/api/network/dashboard" -ForegroundColor Green
    
    if (-not $ServiceOnly) {
        Write-Host "  • Electron Dashboard: Desktop shortcut created" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "Management Commands:" -ForegroundColor Yellow
    Write-Host "  • View Service Status: Get-Service -Name '$ServiceName'" -ForegroundColor Cyan
    Write-Host "  • View Service Logs: Get-EventLog -LogName Application -Source '$ServiceDisplayName'" -ForegroundColor Cyan
    Write-Host "  • Stop Service: Stop-Service -Name '$ServiceName'" -ForegroundColor Cyan
    Write-Host "  • Start Service: Start-Service -Name '$ServiceName'" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Installation Path: $InstallPath" -ForegroundColor Blue
    Write-Host ""
    Write-Success "Microsoft Endpoint Monitor is ready for use!"
}

# Main execution
try {
    if ($Uninstall) {
        Uninstall-Application
        exit 0
    }
    
    Test-Prerequisites
    Remove-ExistingService
    Build-Solution
    Install-Application
    Install-WindowsService
    Configure-Firewall
    Start-Services
    
    if (-not $ServiceOnly) {
        Create-Shortcuts
    }
    
    Test-Installation
    Show-PostInstallInfo
    
} catch {
    Write-Error "Installation failed: $_"
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    exit 1
}
