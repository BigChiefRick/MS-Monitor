# Microsoft Endpoint Monitor - Installation Guide

## 🚀 Installation Options

### Option 1: Simple Installer (Recommended)
1. **Right-click** on `Install-MSMonitor.bat`
2. Select **"Run as administrator"**
3. Follow the on-screen prompts
4. Launch from desktop shortcut when complete

### Option 2: PowerShell Installer
```powershell
# Run PowerShell as Administrator
Set-ExecutionPolicy Bypass -Scope Process -Force
.\installer\scripts\Install-MSMonitor.ps1
```

### Option 3: Silent Installation (For IT Deployment)
```batch
REM Run from elevated command prompt
installer\Install-MSMonitor-Silent.bat
```

## 🔧 Prerequisites

### Required Software
- **Windows 11** (Build 22000 or higher)
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Node.js 18+** - [Download](https://nodejs.org/)
- **Administrator privileges** (for service installation)

### Network Requirements
- **Port 5000** must be available for the API
- **ICMP outbound** access for latency measurements
- **Internet connectivity** for Microsoft endpoint monitoring

## 📦 What Gets Installed

### Application Components
- **Windows Service**: `MicrosoftEndpointMonitor` (auto-start)
- **API Server**: ASP.NET Core API with SignalR hub
- **Desktop Dashboard**: Electron application
- **Database**: SQLite with pre-configured Microsoft endpoints

### File Locations
- **Installation Directory**: `C:\Program Files\Microsoft Endpoint Monitor\`
- **Desktop Shortcuts**: Public desktop
- **Start Menu**: `Microsoft Endpoint Monitor` folder
- **Service Logs**: Windows Event Log + application logs

### Windows Integration
- **Windows Service** registered and auto-starting
- **Firewall Rules** for API port and ICMP
- **Programs and Features** entry for easy uninstallation
- **Start Menu** entries and desktop shortcuts

## 🎯 Post-Installation

### Automatic Services
- **Monitoring Service** starts automatically and begins detecting Microsoft endpoints
- **API Service** available at `http://localhost:5000`
- **Dashboard** accessible via desktop shortcut

### Manual Verification
1. **Check Service**: Open `services.msc` and verify `MicrosoftEndpointMonitor` is running
2. **Test API**: Browse to `http://localhost:5000/api/network/health`
3. **Launch Dashboard**: Double-click desktop shortcut

### Expected Results
- **Real-time monitoring** of Microsoft endpoint connections
- **Latency measurements** for external Microsoft services
- **Process correlation** showing Teams, Outlook, OneDrive, etc.
- **Live dashboard** with dark/light mode toggle

## 🛠️ Troubleshooting

### Installation Issues

#### "Administrator privileges required"
- Right-click installer and select "Run as administrator"
- Ensure you're logged in as a user with admin rights

#### ".NET 8.0 SDK not found"
- Download and install from: https://dotnet.microsoft.com/download
- Restart command prompt after installation

#### "Node.js 18+ required"
- Download and install from: https://nodejs.org/
- Choose LTS version for stability

#### "Port 5000 already in use"
```bash
# Check what's using port 5000
netstat -ano | findstr :5000

# Kill the process if needed
taskkill /PID [ProcessID] /F
```

### Runtime Issues

#### Service won't start
1. Open Event Viewer → Windows Logs → Application
2. Look for `MicrosoftEndpointMonitor` errors
3. Check that .NET 8.0 runtime is installed
4. Verify database directory permissions

#### Dashboard shows "Disconnected"
1. Verify API service is running: `http://localhost:5000/api/network/health`
2. Check Windows Firewall isn't blocking port 5000
3. Restart the monitoring service

#### No latency data appearing
1. Ensure ICMP is allowed through Windows Firewall
2. Check that Microsoft applications (Teams, Outlook, etc.) are running
3. Verify internet connectivity to Microsoft services

## 🗑️ Uninstallation

### Option 1: Programs and Features
1. Open **Settings** → **Apps** → **Apps & features**
2. Search for **"Microsoft Endpoint Monitor"**
3. Click **Uninstall**

### Option 2: PowerShell Uninstaller
```powershell
# Run PowerShell as Administrator
.\installer\scripts\Install-MSMonitor.ps1 -Uninstall
```

### Option 3: Start Menu Uninstaller
1. Open **Start Menu**
2. Navigate to **Microsoft Endpoint Monitor** folder
3. Click **"Uninstall MS-Monitor"**

## 🔧 Advanced Configuration

### Service Configuration
Edit `appsettings.json` in the service directory to modify:
- **Monitoring interval** (default: 5 seconds)
- **Latency timeout** (default: 3 seconds)
- **Cache duration** (default: 30 seconds)

### Firewall Configuration
The installer automatically configures:
- **Inbound rule**: TCP port 5000 for API access
- **Outbound rule**: ICMP for latency measurements

### Database Configuration
- **Provider**: SQLite (no additional setup required)
- **Location**: `database\network_monitor.db`
- **Backup**: Copy the database file for backup

## 📊 Monitoring Features

### Real-time Detection
- **Microsoft Teams**: teams.exe connections
- **Microsoft Outlook**: outlook.exe connections  
- **Microsoft OneDrive**: onedrive.exe connections
- **Microsoft Edge**: msedge.exe and msedgewebview2.exe
- **Office Apps**: Excel, Word, PowerPoint connections

### Latency Tracking
- **ICMP ping** measurements to Microsoft endpoints
- **Color-coded indicators**: Green (<30ms), Yellow (30-100ms), Red (>100ms)
- **Trend charts** showing latency over time
- **Service-specific averages** grouped by application

### Dashboard Features
- **Live connection counts** and statistics
- **Dark/Light mode** toggle with saved preferences
- **Interactive charts** using Chart.js
- **Real-time updates** via SignalR WebSocket
- **Export functionality** for connection data

## 💡 Tips for Best Results

### For Maximum Visibility
1. **Use Microsoft 365 apps** while monitoring (Teams, Outlook, OneDrive)
2. **Browse SharePoint sites** to generate additional connections
3. **Keep apps running** in background for continuous monitoring

### Performance Optimization
1. **Monitor during peak usage** for best data collection
2. **Use SSD storage** for optimal database performance
3. **Close unnecessary applications** to reduce noise in monitoring

### Network Considerations
1. **Corporate networks** may require firewall adjustments
2. **VPN connections** can affect latency measurements
3. **Proxy servers** may impact endpoint detection

---

**For support and updates, visit: https://github.com/BigChiefRick/MS-Monitor**
