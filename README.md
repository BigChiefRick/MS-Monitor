# Microsoft Endpoint Monitor

A real-time network monitoring solution that tracks and visualizes connections to Microsoft services and endpoints.
## Features

- **Real-time Network Monitoring**: Track active connections to Microsoft services
- **Process Correlation**: Identify which applications are making Microsoft connections
- **Historical Data**: Store and analyze connection patterns over time
- **Live Dashboard**: Electron-based GUI with real-time updates
- **Microsoft Service Detection**: Automatically categorize connections by service (Teams, Office 365, Azure, etc.)
- **Bandwidth Monitoring**: Track data usage per Microsoft service
- **Connection Analytics**: Latency monitoring and connection health metrics

## Architecture

- **Backend**: C# .NET Windows Service + ASP.NET Core API
- **Real-time**: Event Tracing for Windows (ETW) + SignalR
- **Database**: SQLite for historical data and configuration
- **Frontend**: Electron application with live graphs
- **Communication**: REST API + WebSocket for real-time updates

## Prerequisites

- Windows 11 (tested platform)
- .NET 8.0 SDK
- Node.js 18+ and npm
- Git
- Visual Studio 2022 or VS Code

## Quick Start

### 1. Clone Repository
```bash
git clone <your-repo-url>
cd microsoft-endpoint-monitor
```

### 2. Setup Backend
```bash
cd src/MicrosoftEndpointMonitor.Service
dotnet restore
dotnet build
```

### 3. Setup Database
```bash
cd ../../database
# Database will be auto-created on first run
```

### 4. Setup Frontend
```bash
cd ../electron-app
npm install
```

### 5. Run Application
```bash
# Terminal 1: Start the monitoring service
cd src/MicrosoftEndpointMonitor.Service
dotnet run

# Terminal 2: Start the API server
cd ../MicrosoftEndpointMonitor.Api
dotnet run

# Terminal 3: Start Electron app
cd ../../electron-app
npm start
```

## Project Structure

```
microsoft-endpoint-monitor/
├── README.md
├── LICENSE
├── .gitignore
├── src/
│   ├── MicrosoftEndpointMonitor.Service/
│   │   ├── Program.cs
│   │   ├── NetworkMonitorService.cs
│   │   ├── Collectors/
│   │   ├── Models/
│   │   └── Services/
│   ├── MicrosoftEndpointMonitor.Api/
│   │   ├── Program.cs
│   │   ├── Controllers/
│   │   ├── Hubs/
│   │   └── Models/
│   ├── MicrosoftEndpointMonitor.Data/
│   │   ├── NetworkContext.cs
│   │   ├── Models/
│   │   └── Repositories/
│   └── MicrosoftEndpointMonitor.Shared/
│       └── Models/
├── electron-app/
│   ├── package.json
│   ├── main.js
│   ├── renderer.js
│   ├── index.html
│   └── assets/
├── database/
│   └── schema.sql
├── docs/
│   └── api.md
└── scripts/
    ├── install.ps1
    └── setup-dev.ps1
```

## Configuration

The application uses `appsettings.json` for configuration:

```json
{
  "Database": {
    "ConnectionString": "Data Source=../database/network_monitor.db"
  },
  "Monitoring": {
    "PollingIntervalMs": 5000,
    "EnableEtw": true,
    "MicrosoftOnly": true
  },
  "SignalR": {
    "HubUrl": "http://localhost:5000/networkhub"
  }
}
```

## Development

### Running in Development Mode

1. **Backend Development**:
   ```bash
   cd src/MicrosoftEndpointMonitor.Api
   dotnet watch run
   ```

2. **Frontend Development**:
   ```bash
   cd electron-app
   npm run dev
   ```

### Building for Production

```bash
# Build all C# projects
dotnet build --configuration Release

# Package Electron app
cd electron-app
npm run build
```

## API Documentation

The REST API provides endpoints for:
- `/api/connections` - Get active connections
- `/api/connections/history` - Get historical data
- `/api/services` - Get Microsoft service statistics
- `/networkhub` - SignalR hub for real-time updates

See [docs/api.md](docs/api.md) for detailed API documentation.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by GlassWire network monitoring
- Uses Microsoft's Event Tracing for Windows (ETW)
- Built with .NET and Electron technologies
