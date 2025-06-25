#Requires -RunAsAdministrator

# Microsoft Endpoint Monitor - Professional Installer (with fallback build)
# Version: 1.0.0
# Requires: Windows 11, .NET 8.0, Node.js 18+

param(
    [switch]$Uninstall,
    [switch]$Silent,
    [string]$InstallPath = "$env:ProgramFiles\Microsoft Endpoint Monitor"
)

$ErrorActionPreference = "Stop"
$global:LogFile = "$env:TEMP\MSMonitor_Install.log"

function Write-Log {
    param($Message, $Type = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Type] $Message"
    Add-Content -Path $global:LogFile -Value $logEntry
    
    switch ($Type) {
        "ERROR" { Write-Host $Message -ForegroundColor Red }
        "WARN" { Write-Host $Message -ForegroundColor Yellow }
        "SUCCESS" { Write-Host $Message -ForegroundColor Green }
        default { Write-Host $Message -ForegroundColor White }
    }
}

function Get-RepositoryRoot {
    $currentPath = $PSScriptRoot
    
    if ($currentPath -like "*\installer\scripts") {
        return Split-Path (Split-Path $currentPath -Parent) -Parent
    }
    if ($currentPath -like "*\installer") {
        return Split-Path $currentPath -Parent
    }
    return $currentPath
}

function Test-Prerequisites {
    Write-Log "Checking prerequisites..." "INFO"
    
    # Check Windows version
    $os = Get-WmiObject -Class Win32_OperatingSystem
    $version = [Version]$os.Version
    if ($version.Major -lt 10 -or ($version.Major -eq 10 -and $version.Build -lt 22000)) {
        throw "Windows 11 (build 22000+) is required. Current: $($os.Caption) Build $($version.Build)"
    }
    Write-Log "✓ Windows 11 detected" "SUCCESS"
    
    # Check .NET 8.0
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($dotnetVersion -notmatch "^8\.") {
            throw ".NET 8.0 SDK not found"
        }
        Write-Log "✓ .NET 8.0 SDK found: $dotnetVersion" "SUCCESS"
    }
    catch {
        Write-Log "❌ .NET 8.0 SDK required. Download from: https://dotnet.microsoft.com/download" "ERROR"
        throw
    }
    
    # Check Node.js
    try {
        $nodeVersion = & node --version 2>$null
        $nodeVersionNum = [Version]($nodeVersion -replace 'v', '')
        if ($nodeVersionNum.Major -lt 18) {
            throw "Node.js 18+ required, found: $nodeVersion"
        }
        Write-Log "✓ Node.js found: $nodeVersion" "SUCCESS"
    }
    catch {
        Write-Log "❌ Node.js 18+ required. Download from: https://nodejs.org/" "ERROR"
        throw
    }
    
    # Check admin privileges
    if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
        throw "Administrator privileges required for installation"
    }
    Write-Log "✓ Administrator privileges confirmed" "SUCCESS"
}

function Build-DotNetProjects {
    param($InstallPath)
    
    Write-Log "Building .NET projects..." "INFO"
    $originalLocation = Get-Location
    Set-Location "$InstallPath"
    
    try {
        # Try solution-based build first
        $solutionFile = "MicrosoftEndpointMonitor.sln"
        if (Test-Path $solutionFile) {
            Write-Log "Attempting solution-based build..." "INFO"
            
            # Restore packages for the solution
            & dotnet restore $solutionFile --verbosity quiet 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Log "✓ NuGet packages restored (solution)" "SUCCESS"
                
                # Build the solution
                & dotnet build $solutionFile --configuration Release --verbosity quiet --no-restore 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-Log "✅ Solution built successfully" "SUCCESS"
                    return $true
                }
            }
            Write-Log "⚠ Solution build failed, trying individual projects..." "WARN"
        }
        
        # Fallback: Build individual projects
        Write-Log "Building projects individually..." "INFO"
        $projects = @(
            "src\MicrosoftEndpointMonitor.Shared\MicrosoftEndpointMonitor.Shared.csproj",
            "src\MicrosoftEndpointMonitor.Data\MicrosoftEndpointMonitor.Data.csproj",
            "src\MicrosoftEndpointMonitor.Service\MicrosoftEndpointMonitor.Service.csproj",
            "src\MicrosoftEndpointMonitor.Api\MicrosoftEndpointMonitor.Api.csproj"
        )
        
        foreach ($project in $projects) {
            if (Test-Path $project) {
                Write-Log "Building: $project" "INFO"
                
                # Restore packages
                & dotnet restore $project --verbosity quiet
                if ($LASTEXITCODE -ne 0) {
                    Write-Log "Failed to restore: $project" "ERROR"
                    return $false
                }
                
                # Build project
                & dotnet build $project --configuration Release --verbosity quiet --no-restore
                if ($LASTEXITCODE -ne 0) {
                    Write-Log "Failed to build: $project" "ERROR"
                    return $false
                }
                
                Write-Log "✓ Built: $project" "SUCCESS"
            } else {
                Write-Log "⚠ Project not found: $project" "WARN"
            }
        }
        
        Write-Log "✅ All projects built successfully" "SUCCESS"
        return $true
        
    } catch {
        Write-Log "❌ Build failed: $($_.Exception.Message)" "ERROR"
        return $false
    } finally {
        Set-Location $originalLocation
    }
}

function Install-Application {
    Write-Log "Starting Microsoft Endpoint Monitor installation..." "INFO"
    
    try {
        # Get repository root directory
        $repoRoot = Get-RepositoryRoot
        Write-Log "Repository root: $repoRoot" "INFO"
        
        # Verify source directories exist
        $srcDir = Join-Path $repoRoot "src"
        $electronDir = Join-Path $repoRoot "electron-app"
        $databaseDir = Join-Path $repoRoot "database"
        
        if (!(Test-Path $srcDir)) {
            throw "Source directory not found: $srcDir"
        }
        if (!(Test-Path $electronDir)) {
            throw "Electron app directory not found: $electronDir"
        }
        if (!(Test-Path $databaseDir)) {
            throw "Database directory not found: $databaseDir"
        }
        
        Write-Log "✓ Source directories verified" "SUCCESS"
        
        # Create installation directory
        if (Test-Path $InstallPath) {
            Write-Log "Removing existing installation..." "WARN"
            Remove-Item $InstallPath -Recurse -Force
        }
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Log "Created installation directory: $InstallPath" "SUCCESS"
        
        # Copy application files
        Write-Log "Copying application files..." "INFO"
        
        Copy-Item $srcDir "$InstallPath\src" -Recurse -Force
        Write-Log "✓ .NET source files copied" "SUCCESS"
        
        Copy-Item $databaseDir "$InstallPath\database" -Recurse -Force
        Write-Log "✓ Database files copied" "SUCCESS"
        
        Copy-Item $electronDir "$InstallPath\electron-app" -Recurse -Force
        Write-Log "✓ Electron app files copied" "SUCCESS"
        
        # Copy solution file if it exists
        $solutionFile = Join-Path $repoRoot "MicrosoftEndpointMonitor.sln"
        if (Test-Path $solutionFile) {
            Copy-Item $solutionFile "$InstallPath\" -Force
            Write-Log "✓ Solution file copied" "SUCCESS"
        }
        
        # Copy documentation
        $readmePath = Join-Path $repoRoot "README.md"
        $licensePath = Join-Path $repoRoot "LICENSE"
        
        if (Test-Path $readmePath) {
            Copy-Item $readmePath "$InstallPath\" -Force
        }
        if (Test-Path $licensePath) {
            Copy-Item $licensePath "$InstallPath\" -Force
        }
        
        Write-Log "✓ Application files copied successfully" "SUCCESS"
        
        # Build .NET projects
        $buildSuccess = Build-DotNetProjects -InstallPath $InstallPath
        if (-not $buildSuccess) {
            throw "Failed to build .NET projects"
        }
        
        # Verify service executable exists
        $serviceExe = "$InstallPath\src\MicrosoftEndpointMonitor.Service\bin\Release\net8.0\MicrosoftEndpointMonitor.Service.exe"
        if (!(Test-Path $serviceExe)) {
            throw "Service executable not found after build: $serviceExe"
        }
        Write-Log "✓ Service executable verified: $(Split-Path $serviceExe -Leaf)" "SUCCESS"
        
        # Verify API assembly exists
        $apiDll = "$InstallPath\src\MicrosoftEndpointMonitor.Api\bin\Release\net8.0\MicrosoftEndpointMonitor.Api.dll"
        if (!(Test-Path $apiDll)) {
            throw "API assembly not found after build: $apiDll"
        }
        Write-Log "✓ API assembly verified: $(Split-Path $apiDll -Leaf)" "SUCCESS"
        
        # Install Electron dependencies
        Write-Log "Installing Electron dependencies..." "INFO"
        $originalLocation = Get-Location
        Set-Location "$InstallPath\electron-app"
        
        & npm install --silent --no-audit --no-fund 2>$null
        if ($LASTEXITCODE -ne 0) { 
            Write-Log "Retrying npm install with verbose output..." "WARN"
            & npm install --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) {
                throw "npm install failed with exit code $LASTEXITCODE"
            }
        }
        Write-Log "✓ Electron dependencies installed" "SUCCESS"
        
        Set-Location $originalLocation
        
        # Create Windows Service
        Write-Log "Installing Windows Service..." "INFO"
        $serviceName = "MicrosoftEndpointMonitor"
        $serviceDisplayName = "Microsoft Endpoint Monitor Service"
        $serviceDescription = "Real-time monitoring service for Microsoft endpoint connections and latency tracking"
        $servicePath = $serviceExe
        
        # Remove existing service if it exists
        $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($existingService) {
            Write-Log "Stopping existing service..." "INFO"
            Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            & sc.exe delete $serviceName | Out-Null
            Start-Sleep -Seconds 2
        }
        
        # Create new service
        $scResult = & sc.exe create $serviceName binPath= "`"$servicePath`"" DisplayName= "`"$serviceDisplayName`"" start= auto
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create Windows service: $scResult"
        }
        
        & sc.exe description $serviceName "`"$serviceDescription`"" | Out-Null
        Write-Log "✓ Windows Service installed: $serviceName" "SUCCESS"
        
        # Create desktop shortcuts
        Write-Log "Creating desktop shortcuts..." "INFO"
        $WshShell = New-Object -comObject WScript.Shell
        
        # Dashboard shortcut
        $dashboardShortcut = $WshShell.CreateShortcut("$env:PUBLIC\Desktop\MS-Monitor Dashboard.lnk")
        $dashboardShortcut.TargetPath = "cmd.exe"
        $dashboardShortcut.Arguments = "/c `"cd /d `"$InstallPath\electron-app`" && npm start`""
        $dashboardShortcut.WorkingDirectory = "$InstallPath\electron-app"
        $dashboardShortcut.Description = "Microsoft Endpoint Monitor Dashboard"
        $dashboardShortcut.Save()
        
        # API shortcut
        $apiShortcut = $WshShell.CreateShortcut("$env:PUBLIC\Desktop\MS-Monitor API Test.lnk")
        $apiShortcut.TargetPath = "cmd.exe"
        $apiShortcut.Arguments = "/c `"cd /d `"$InstallPath\src\MicrosoftEndpointMonitor.Api`" && dotnet run --configuration Release && pause`""
        $apiShortcut.WorkingDirectory = "$InstallPath\src\MicrosoftEndpointMonitor.Api"
        $apiShortcut.Description = "Test Microsoft Endpoint Monitor API"
        $apiShortcut.Save()
        
        # Service shortcut
        $serviceShortcut = $WshShell.CreateShortcut("$env:PUBLIC\Desktop\MS-Monitor Service.lnk")
        $serviceShortcut.TargetPath = "services.msc"
        $serviceShortcut.Description = "Manage Microsoft Endpoint Monitor Service"
        $serviceShortcut.Save()
        
        Write-Log "✓ Desktop shortcuts created" "SUCCESS"
        
        # Create Start Menu entries
        $startMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Endpoint Monitor"
        New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
        
        Copy-Item "$env:PUBLIC\Desktop\MS-Monitor Dashboard.lnk" "$startMenuPath\" -Force
        Copy-Item "$env:PUBLIC\Desktop\MS-Monitor API Test.lnk" "$startMenuPath\" -Force
        Copy-Item "$env:PUBLIC\Desktop\MS-Monitor Service.lnk" "$startMenuPath\" -Force
        
        Write-Log "✓ Start Menu entries created" "SUCCESS"
        
        # Configure Windows Firewall
        Write-Log "Configuring Windows Firewall..." "INFO"
        & netsh advfirewall firewall delete rule name="MS-Monitor API" 2>$null
        & netsh advfirewall firewall add rule name="MS-Monitor API" dir=in action=allow protocol=TCP localport=5000 | Out-Null
        & netsh advfirewall firewall add rule name="MS-Monitor ICMP" dir=out action=allow protocol=icmpv4:8,any | Out-Null
        Write-Log "✓ Windows Firewall configured" "SUCCESS"
        
        # Add to Programs and Features
        Write-Log "Registering with Windows Programs and Features..." "INFO"
        $uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MicrosoftEndpointMonitor"
        New-Item -Path $uninstallKey -Force | Out-Null
        Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "Microsoft Endpoint Monitor"
        Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.0"
        Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "BigChiefRick"
        Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallPath
        Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$InstallPath\installer\scripts\Install-MSMonitor.ps1`" -Uninstall"
        Set-ItemProperty -Path $uninstallKey -Name "EstimatedSize" -Value 150000 -Type DWord
        Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -Type DWord
        Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -Type DWord
        Write-Log "✓ Registered with Programs and Features" "SUCCESS"
        
        # Start the service
        Write-Log "Starting Microsoft Endpoint Monitor Service..." "INFO"
        try {
            Start-Service -Name $serviceName
            
            $timeout = 30
            $timer = 0
            do {
                Start-Sleep -Seconds 1
                $timer++
                $service = Get-Service -Name $serviceName
            } while ($service.Status -ne "Running" -and $timer -lt $timeout)
            
            if ($service.Status -eq "Running") {
                Write-Log "✓ Service started successfully" "SUCCESS"
            } else {
                Write-Log "⚠ Service status: $($service.Status) - may need manual start" "WARN"
            }
        }
        catch {
            Write-Log "⚠ Service start failed: $($_.Exception.Message)" "WARN"
            Write-Log "⚠ You can start the service manually from Services console" "WARN"
        }
        
        Write-Log "🎉 Microsoft Endpoint Monitor installed successfully!" "SUCCESS"
        Write-Log "" "INFO"
        Write-Log "🚀 Next steps:" "INFO"
        Write-Log "1. Start the monitoring service (if not already running)" "INFO"
        Write-Log "2. Launch 'MS-Monitor API Test' to start the API server" "INFO"
        Write-Log "3. Launch 'MS-Monitor Dashboard' to view the real-time dashboard" "INFO"
        Write-Log "4. View API health at: http://localhost:5000/api/network/health" "INFO"
        Write-Log "" "INFO"
        Write-Log "📁 Installation completed in: $InstallPath" "SUCCESS"
        
    }
    catch {
        Write-Log "❌ Installation failed: $($_.Exception.Message)" "ERROR"
        throw
    }
}

function Uninstall-Application {
    Write-Log "Starting Microsoft Endpoint Monitor uninstallation..." "INFO"
    
    try {
        $serviceName = "MicrosoftEndpointMonitor"
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($service) {
            Write-Log "Stopping and removing Windows Service..." "INFO"
            Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            & sc.exe delete $serviceName | Out-Null
            Write-Log "✓ Windows Service removed" "SUCCESS"
        }
        
        Write-Log "Removing Windows Firewall rules..." "INFO"
        & netsh advfirewall firewall delete rule name="MS-Monitor API" 2>$null
        & netsh advfirewall firewall delete rule name="MS-Monitor ICMP" 2>$null
        Write-Log "✓ Firewall rules removed" "SUCCESS"
        
        $uninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MicrosoftEndpointMonitor"
        if (Test-Path $uninstallKey) {
            Remove-Item -Path $uninstallKey -Force
            Write-Log "✓ Removed from Programs and Features" "SUCCESS"
        }
        
        Write-Log "Removing shortcuts and Start Menu entries..." "INFO"
        Remove-Item "$env:PUBLIC\Desktop\MS-Monitor Dashboard.lnk" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:PUBLIC\Desktop\MS-Monitor API Test.lnk" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:PUBLIC\Desktop\MS-Monitor Service.lnk" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Endpoint Monitor" -Recurse -Force -ErrorAction SilentlyContinue
        Write-Log "✓ Shortcuts removed" "SUCCESS"
        
        if (Test-Path $InstallPath) {
            Write-Log "Removing installation directory..." "INFO"
            Get-Process | Where-Object { $_.Path -like "$InstallPath*" } | ForEach-Object {
                Write-Log "Stopping process: $($_.ProcessName)" "INFO"
                $_ | Stop-Process -Force -ErrorAction SilentlyContinue
            }
            
            Start-Sleep -Seconds 2
            Remove-Item $InstallPath -Recurse -Force
            Write-Log "✓ Installation directory removed" "SUCCESS"
        }
        
        Write-Log "🎉 Microsoft Endpoint Monitor uninstalled successfully!" "SUCCESS"
        
    }
    catch {
        Write-Log "❌ Uninstallation failed: $($_.Exception.Message)" "ERROR"
        throw
    }
}

function Show-Banner {
    Clear-Host
    Write-Host ""
    Write-Host "╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                                                               ║" -ForegroundColor Cyan
    Write-Host "║          🔍 Microsoft Endpoint Monitor Installer             ║" -ForegroundColor Cyan
    Write-Host "║                                                               ║" -ForegroundColor Cyan
    Write-Host "║     Real-time monitoring for Microsoft services with         ║" -ForegroundColor Cyan
    Write-Host "║        latency tracking and dark mode dashboard              ║" -ForegroundColor Cyan
    Write-Host "║                                                               ║" -ForegroundColor Cyan
    Write-Host "║                    Version 1.0.0                             ║" -ForegroundColor Cyan
    Write-Host "║                  by BigChiefRick                              ║" -ForegroundColor Cyan
    Write-Host "║                                                               ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

# Main execution
try {
    if (-not $Silent) {
        Show-Banner
    }
    
    Write-Log "=== Microsoft Endpoint Monitor Installer Started ===" "INFO"
    Write-Log "Log file: $global:LogFile" "INFO"
    Write-Log "Script location: $PSScriptRoot" "INFO"
    
    if ($Uninstall) {
        Write-Log "Uninstall mode selected" "INFO"
        Uninstall-Application
    } else {
        Test-Prerequisites
        Install-Application
    }
    
    Write-Log "=== Installation Completed Successfully ===" "SUCCESS"
    
    if (-not $Silent) {
        Write-Host ""
        Write-Host "Press any key to exit..." -ForegroundColor Yellow
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
    
    exit 0
}
catch {
    Write-Log "=== Installation Failed ===" "ERROR"
    Write-Log $_.Exception.Message "ERROR"
    Write-Log "Check log file: $global:LogFile" "ERROR"
    
    if (-not $Silent) {
        Write-Host ""
        Write-Host "Press any key to exit..." -ForegroundColor Red
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
    
    exit 1
}
