# 🔍 Microsoft Endpoint Monitor

**Real-time network monitoring for Microsoft services with latency tracking and dark mode**

A ThousandEyes-style monitoring platform specifically designed to monitor Microsoft 365, Teams, OneDrive, SharePoint, Outlook, and other Microsoft service endpoints with real-time latency measurements and process correlation.

![Microsoft Endpoint Monitor](https://img.shields.io/badge/Status-Working-brightgreen)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)
![Electron](https://img.shields.io/badge/Electron-28.0-blue)
![Windows 11](https://img.shields.io/badge/Windows-11-blue)

## 🎯 Features

### 🔥 **Real-time Monitoring**
- **Live TCP connection monitoring** using Windows networking APIs
- **Microsoft endpoint detection** via IP ranges and domain patterns
- **Process correlation** to identify Teams, Outlook, OneDrive, etc.
- **Latency measurements** using ICMP ping with intelligent caching
- **Connection state tracking** (Established, TimeWait, etc.)

### 📊 **Dashboard & Analytics**
- **Real-time dashboard** with live connection counts and metrics
- **Interactive charts** showing latency trends over time
- **Connection distribution** visualizations
- **Service-specific statistics** grouped by Microsoft application
- **Color-coded latency indicators** (Green <30ms, Yellow <100ms, Red >100ms)

### 🎨 **Modern UI Experience**
- **Light/Dark mode toggle** with saved preferences
- **Glassmorphism design** with backdrop blur effects
- **Responsive layout** that works on different screen sizes
- **Real-time status indicators** and connection health
- **Loading animations** and smooth transitions

### 🔧 **Technical Architecture**
- **C# .NET 8.0 Service** for Windows network monitoring
- **ASP.NET Core API** with REST endpoints and SignalR hub
- **SQLite database** with Entity Framework Core for persistence
- **Electron desktop app** with modern HTML5/CSS3/JavaScript UI
- **SignalR WebSocket** for real-time data streaming

## 📸 Screenshots

### Light Mode Dashboard
*Real-time monitoring showing 19 Microsoft endpoints with 39ms average latency*

### Dark Mode Dashboard
*Same functionality with beautiful dark theme and proper contrast*

## 🚀 Quick Start

### Prerequisites
- **Windows 11** (required for network API access)
- **.NET 8.0 SDK** - [Download here](https://dotnet.microsoft.com/download)
- **Node.js 18+** - [Download here](https://nodejs.org/)
- **Administrator privileges** (required for network monitoring)

### 1. Clone the Repository
```bash
git clone https://github.com/BigChiefRick/MS-Monitor.git
cd MS-Monitor
```

### 2. Build the Solution
```bash
# Restore NuGet packages
dotnet restore

# Build all projects
dotnet build --configuration Debug
```

### 3. Install Electron Dependencies
```bash
cd electron-app
npm install
cd ..
```

### 4. Start the Services

#### Terminal 1: Start the Monitoring Service (as Administrator)
```bash
cd src/MicrosoftEndpointMonitor.Service
dotnet run
```
*Expected output:*
```
MS-Monitor Service started - Real network monitoring with latency tracking active
Monitoring Cycle: 115 total, 19 Microsoft endpoints detected
MS Endpoint: Microsoft Edge (msedgewebview2) -> 52.96.222.226:443 | Latency: 27ms | State: Established
```

#### Terminal 2: Start the API Service
```bash
cd src/MicrosoftEndpointMonitor.Api
dotnet run
```
*API will be available at: http://localhost:5000*

#### Terminal 3: Start the Electron Dashboard
```bash
cd electron-app
npm start
```

### 5. View the Dashboard
The Electron app will automatically open showing:
- **Real-time connection counts**
- **Microsoft endpoint detection**
- **Latency measurements**
- **Interactive charts and tables**

## 🏗️ Architecture Overview

```
┌─────────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│  Electron Frontend │◄──►│  ASP.NET Core    │◄──►│ .NET Service    │
│  (Dashboard UI)    │    │  API + SignalR   │    │ (TCP Monitor)   │
└─────────────────────┘    └──────────────────┘    └─────────────────┘
                                      │                       │
                                      ▼                       ▼
                            ┌──────────────────┐    ┌─────────────────┐
                            │ SQLite Database  │    │ Windows TCP API │
                            │ (EF Core)        │    │ (Network Stack) │
                            └──────────────────┘    └─────────────────┘
```

### Components

#### 🔧 **MicrosoftEndpointMonitor.Service**
- **TcpConnectionEnumerator**: Uses Windows `IPGlobalProperties` to enumerate active TCP connections
- **MicrosoftEndpointDetector**: Classifies endpoints using IP ranges and domain patterns
- **NetworkMonitorService**: Background service that runs monitoring cycles every 5 seconds
- **Process Correlation**: Maps network connections to running Microsoft applications

#### 🌐 **MicrosoftEndpointMonitor.Api**
- **NetworkController**: REST API with 12+ endpoints for dashboard data
- **NetworkMonitorHub**: SignalR hub for real-time WebSocket updates
- **CORS Support**: Configured for Electron app communication
- **Health Checks**: `/api/network/health` endpoint for monitoring

#### 📊 **MicrosoftEndpointMonitor.Data**
- **NetworkContext**: Entity Framework Core database context
- **Models**: 11 database tables including connections, metrics, alerts, and configuration
- **Seeded Data**: Pre-configured with 130+ Microsoft IP ranges and service definitions

#### 🖥️ **Electron Dashboard**
- **Real-time Updates**: SignalR client for live data streaming
- **Chart.js Integration**: Interactive latency and distribution charts
- **Dark Mode**: Complete theme system with localStorage persistence
- **Responsive Design**: Mobile-friendly layout with CSS Grid

## 📋 API Endpoints

### Core Endpoints
- `GET /api/network/health` - Service health check
- `GET /api/network/dashboard` - Real-time dashboard data
- `GET /api/network/connections` - All network connections
- `GET /api/network/connections/microsoft` - Microsoft endpoints only
- `GET /api/network/services` - Active Microsoft services statistics
- `GET /api/network/endpoints` - Configured Microsoft endpoint definitions
- `GET /api/network/alerts` - System alerts and notifications
- `POST /api/network/update` - Update dashboard data (used by Service)

### SignalR Events
- `ConnectionUpdate` - Real-time connection events
- `DashboardUpdate` - Live dashboard data updates
- `AlertReceived` - System alerts and notifications
- `ServiceUpdate` - Service-specific statistics

## 🎛️ Configuration

### Service Configuration (`appsettings.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MicrosoftEndpointMonitor": "Debug"
    }
  },
  "MonitoringOptions": {
    "PollIntervalSeconds": 5,
    "LatencyTimeoutMs": 3000,
    "CacheLatencySeconds": 30
  }
}
```

### Database
- **Provider**: SQLite with Entity Framework Core 8.0
- **Location**: `../database/network_monitor.db`
- **Auto-created**: Database and tables are created automatically on first run
- **Migrations**: Schema updates handled automatically

## 🔍 Microsoft Service Detection

### IP Range Detection
The system includes 130+ pre-configured Microsoft IP ranges:
- **Teams**: `52.108.0.0/14`
- **Office 365**: `52.96.0.0/11`
- **Azure**: `20.0.0.0/8`
- **OneDrive**: `52.121.0.0/16`
- **Exchange**: `40.92.0.0/15`
- **SharePoint**: `52.244.0.0/16`

### Domain Pattern Matching
- `*.microsoft.com`, `*.office365.com`, `*.teams.microsoft.com`
- `*.onedrive.com`, `*.sharepoint.com`, `*.outlook.com`
- `*.microsoftonline.com`, `*.graph.microsoft.com`

### Process Correlation
Maps network connections to running processes:
- **teams.exe** → Microsoft Teams
- **outlook.exe** → Microsoft Outlook
- **onedrive.exe** → Microsoft OneDrive
- **msedgewebview2.exe** → Microsoft Edge WebView
- **excel.exe**, **winword.exe**, **powerpnt.exe** → Office Applications

## 📈 Performance & Monitoring

### System Requirements
- **Memory Usage**: ~50-100MB for Service + API
- **CPU Usage**: <5% during normal monitoring
- **Database Growth**: ~1MB per day of typical usage
- **Network**: Minimal impact with intelligent ping caching

### Latency Monitoring
- **ICMP Ping**: 3-second timeout with 30-second caching per endpoint
- **Color Coding**: Green (<30ms), Yellow (30-100ms), Red (>100ms)
- **Trend Charts**: Real-time latency visualization with 20-point history
- **Filtering**: Excludes local/private IP ranges from latency measurement

### Real-time Updates
- **Service Cycle**: 5-second monitoring intervals
- **API Polling**: 5-second dashboard refresh (fallback)
- **SignalR Updates**: Immediate push when available
- **Chart Updates**: Smooth animations with 300ms transitions

## 🛠️ Development

### Building from Source
```bash
# Clone and build
git clone https://github.com/BigChiefRick/MS-Monitor.git
cd MS-Monitor

# Restore packages
dotnet restore

# Build solution
dotnet build --configuration Debug

# Run tests (when available)
dotnet test
```

### Project Structure
```
MS-Monitor/
├── src/
│   ├── MicrosoftEndpointMonitor.Shared/     # Shared models and DTOs
│   ├── MicrosoftEndpointMonitor.Data/       # Entity Framework data layer
│   ├── MicrosoftEndpointMonitor.Service/    # Background monitoring service
│   └── MicrosoftEndpointMonitor.Api/        # REST API and SignalR hub
├── electron-app/                            # Electron desktop application
├── database/                                # SQLite database schema
├── scripts/                                 # PowerShell installation scripts
└── docs/                                    # Additional documentation
```

### Adding New Microsoft Services
1. Update IP ranges in `MicrosoftEndpointDetector.cs`
2. Add domain patterns to service detection
3. Update process correlation mappings
4. Test endpoint classification

## 🐛 Troubleshooting

### Common Issues

#### Service Not Detecting Connections
```bash
# Ensure running as Administrator
# Check Windows Firewall settings
# Verify Microsoft applications are running and connected
```

#### API Connection Failures
```bash
# Check port 5000 availability
netstat -an | findstr :5000

# Verify CORS configuration
# Check Windows Firewall exceptions
```

#### Electron App Shows "Disconnected"
```bash
# Ensure API service is running
# Check SignalR connection in browser dev tools (F12)
# Verify http://localhost:5000/api/network/health responds
```

#### No Latency Data
```bash
# Check ICMP permissions (may require admin)
# Verify external network connectivity
# Check Windows Firewall ICMP rules
```

### Debug Mode
Enable detailed logging in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "MicrosoftEndpointMonitor": "Debug"
    }
  }
}
```

## 🤝 Contributing

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Development Guidelines
- Follow C# coding standards and conventions
- Add unit tests for new functionality
- Update documentation for API changes
- Test on Windows 11 environment
- Ensure Electron app remains responsive

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Microsoft** for comprehensive networking APIs
- **Electron** for cross-platform desktop framework
- **Chart.js** for beautiful data visualizations
- **SignalR** for real-time communication
- **Entity Framework Core** for data persistence

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/BigChiefRick/MS-Monitor/issues)
- **Discussions**: [GitHub Discussions](https://github.com/BigChiefRick/MS-Monitor/discussions)
- **Wiki**: [Project Wiki](https://github.com/BigChiefRick/MS-Monitor/wiki)

---

**Built with ❤️ for monitoring Microsoft services in real-time**

*Last updated: June 25, 2025*
