using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using MicrosoftEndpointMonitor.Service.Collectors;
using MicrosoftEndpointMonitor.Service.Services;
using MicrosoftEndpointMonitor.Shared.Models;
using MicrosoftEndpointMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace MicrosoftEndpointMonitor.Service;

/// <summary>
/// Main background service for monitoring network connections to Microsoft endpoints
/// </summary>
public class NetworkMonitorService : BackgroundService
{
    private readonly ILogger<NetworkMonitorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly NetworkContext _context;
    private readonly TcpConnectionEnumerator _connectionEnumerator;
    private readonly MicrosoftEndpointDetector _endpointDetector;
    private readonly IHubContext<NetworkMonitorHub> _hubContext;

    // Configuration
    private readonly int _pollingIntervalMs;
    private readonly bool _microsoftOnly;
    private readonly bool _enableEtw;
    private readonly int _maxHistoryDays;

    // State tracking
    private readonly Dictionary<string, NetworkConnection> _activeConnections = new();
    private readonly Dictionary<string, DateTime> _lastSeen = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private int _totalConnectionsProcessed = 0;
    private int _microsoftConnectionsFound = 0;

    public NetworkMonitorService(
        ILogger<NetworkMonitorService> logger,
        IConfiguration configuration,
        NetworkContext context,
        TcpConnectionEnumerator connectionEnumerator,
        MicrosoftEndpointDetector endpointDetector,
        IHubContext<NetworkMonitorHub> hubContext)
    {
        _logger = logger;
        _configuration = configuration;
        _context = context;
        _connectionEnumerator = connectionEnumerator;
        _endpointDetector = endpointDetector;
        _hubContext = hubContext;

        // Load configuration
        _pollingIntervalMs = _configuration.GetValue<int>("Monitoring:PollingIntervalMs", 5000);
        _microsoftOnly = _configuration.GetValue<bool>("Monitoring:MicrosoftOnly", true);
        _enableEtw = _configuration.GetValue<bool>("Monitoring:EnableEtw", true);
        _maxHistoryDays = _configuration.GetValue<int>("Monitoring:MaxHistoryDays", 30);

        _logger.LogInformation("Network Monitor Service initialized with polling interval: {IntervalMs}ms", _pollingIntervalMs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Network Monitor Service starting...");

        try
        {
            // Initialize database
            await InitializeDatabaseAsync();

            // Start monitoring session
            await StartMonitoringSessionAsync();

            // Main monitoring loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNetworkConnectionsAsync();
                    await PerformMaintenanceAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in monitoring loop");
                }

                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Network Monitor Service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error in Network Monitor Service");
            throw;
        }
        finally
        {
            await EndMonitoringSessionAsync();
            _logger.LogInformation("Network Monitor Service stopped");
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _context.Database.EnsureCreatedAsync();
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    private async Task StartMonitoringSessionAsync()
    {
        try
        {
            var session = new MonitoringSession
            {
                SessionStart = DateTime.UtcNow,
                ComputerName = Environment.MachineName,
                WindowsVersion = Environment.OSVersion.ToString(),
                ServiceVersion = "1.0.0", // TODO: Get from assembly
                SessionNotes = "Monitoring session started"
            };

            _context.MonitoringSessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Monitoring session started for computer: {ComputerName}", Environment.MachineName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start monitoring session");
        }
    }

    private async Task ProcessNetworkConnectionsAsync()
    {
        try
        {
            // Get current connections
            var currentConnections = await _connectionEnumerator.GetActiveConnectionsAsync();
            _totalConnectionsProcessed += currentConnections.Count;

            var newConnections = new List<NetworkConnection>();
            var updatedConnections = new List<NetworkConnection>();
            var connectionKeys = new HashSet<string>();

            foreach (var connection in currentConnections)
            {
                // Skip if we're only monitoring Microsoft endpoints and this isn't one
                if (_microsoftOnly)
                {
                    var (serviceName, category) = await _endpointDetector.DetectMicrosoftServiceAsync(connection);
                    if (string.IsNullOrEmpty(serviceName))
                    {
                        continue;
                    }

                    connection.MicrosoftService = serviceName;
                    connection.ServiceCategory = category;
                    _microsoftConnectionsFound++;
                }

                var connectionKey = GetConnectionKey(connection);
                connectionKeys.Add(connectionKey);
                _lastSeen[connectionKey] = DateTime.UtcNow;

                if (_activeConnections.TryGetValue(connectionKey, out var existingConnection))
                {
                    // Update existing connection
                    await UpdateExistingConnectionAsync(existingConnection, connection);
                    updatedConnections.Add(existingConnection);
                }
                else
                {
                    // New connection
                    _activeConnections[connectionKey] = connection;
                    newConnections.Add(connection);
                }
            }

            // Process new connections
            if (newConnections.Any())
            {
                await ProcessNewConnectionsAsync(newConnections);
            }

            // Process updated connections
            if (updatedConnections.Any())
            {
                await ProcessUpdatedConnectionsAsync(updatedConnections);
            }

            // Mark connections as closed if they're no longer active
            await ProcessClosedConnectionsAsync(connectionKeys);

            _logger.LogDebug("Processed {Total} connections, {Microsoft} Microsoft connections, {New} new, {Updated} updated",
                currentConnections.Count, _microsoftConnectionsFound, newConnections.Count, updatedConnections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process network connections");
        }
    }

    private async Task ProcessNewConnectionsAsync(List<NetworkConnection> newConnections)
    {
        try
        {
            // Save to database
            _context.Connections.AddRange(newConnections);
            await _context.SaveChangesAsync();

            // Broadcast to clients
            foreach (var connection in newConnections)
            {
                var connectionEvent = new ConnectionEvent
                {
                    EventType = "CONNECTED",
                    Connection = connection,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.All.SendAsync("ConnectionUpdate", connectionEvent);

                // Generate alert if configured
                if (connection.IsMicrosoftEndpoint)
                {
                    await GenerateConnectionAlertAsync(connection, "NEW_MICROSOFT_CONNECTION");
                }
            }

            _logger.LogDebug("Processed {Count} new connections", newConnections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process new connections");
        }
    }

    private async Task ProcessUpdatedConnectionsAsync(List<NetworkConnection> updatedConnections)
    {
        try
        {
            // Update in database
            _context.Connections.UpdateRange(updatedConnections);
            await _context.SaveChangesAsync();

            // Broadcast updates to clients
            foreach (var connection in updatedConnections)
            {
                var connectionEvent = new ConnectionEvent
                {
                    EventType = "DATA_TRANSFER",
                    Connection = connection,
                    Timestamp = DateTime.UtcNow,
                    BytesTransferred = connection.TotalBytes
                };

                await _hubContext.Clients.All.SendAsync("ConnectionUpdate", connectionEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process updated connections");
        }
    }

    private async Task ProcessClosedConnectionsAsync(HashSet<string> activeConnectionKeys)
    {
        try
        {
            var closedConnections = new List<NetworkConnection>();
            var connectionsToRemove = new List<string>();

            foreach (var kvp in _activeConnections)
            {
                if (!activeConnectionKeys.Contains(kvp.Key) && 
                    DateTime.UtcNow - _lastSeen.GetValueOrDefault(kvp.Key, DateTime.MinValue) > TimeSpan.FromMinutes(2))
                {
                    var connection = kvp.Value;
                    connection.IsActive = false;
                    connection.ClosedTime = DateTime.UtcNow;
                    connection.DurationMs = (int)(connection.ClosedTime.Value - connection.EstablishedTime).TotalMilliseconds;

                    closedConnections.Add(connection);
                    connectionsToRemove.Add(kvp.Key);
                }
            }

            if (closedConnections.Any())
            {
                // Update database
                _context.Connections.UpdateRange(closedConnections);
                await _context.SaveChangesAsync();

                // Remove from active tracking
                foreach (var key in connectionsToRemove)
                {
                    _activeConnections.Remove(key);
                    _lastSeen.Remove(key);
                }

                // Broadcast to clients
                foreach (var connection in closedConnections)
                {
                    var connectionEvent = new ConnectionEvent
                    {
                        EventType = "DISCONNECTED",
                        Connection = connection,
                        Timestamp = DateTime.UtcNow
                    };

                    await _hubContext.Clients.All.SendAsync("ConnectionUpdate", connectionEvent);
                }

                _logger.LogDebug("Processed {Count} closed connections", closedConnections.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process closed connections");
        }
    }

    private async Task UpdateExistingConnectionAsync(NetworkConnection existing, NetworkConnection current)
    {
        existing.BytesSent = current.BytesSent;
        existing.BytesReceived = current.BytesReceived;
        existing.PacketsSent = current.PacketsSent;
        existing.PacketsReceived = current.PacketsReceived;
        existing.LastActivityTime = DateTime.UtcNow;
        existing.ConnectionState = current.ConnectionState;
        existing.UpdatedAt = DateTime.UtcNow;

        // Store metrics if there's significant change
        var bytesDelta = current.TotalBytes - existing.TotalBytes;
        if (bytesDelta > 1024) // 1KB threshold
        {
            await StoreConnectionMetricAsync(existing, bytesDelta);
        }
    }

    private async Task StoreConnectionMetricAsync(NetworkConnection connection, long bytesDelta)
    {
        try
        {
            var metric = new ConnectionMetric
            {
                ConnectionId = connection.Id,
                Timestamp = DateTime.UtcNow,
                BytesPerSecondIn = (int)(bytesDelta / _pollingIntervalMs * 1000),
                BytesPerSecondOut = 0, // TODO: Calculate actual in/out
                PacketsPerSecondIn = 0,
                PacketsPerSecondOut = 0,
                ConnectionQuality = DetermineConnectionQuality(bytesDelta)
            };

            _context.ConnectionMetrics.Add(metric);
            // Note: Save will happen in batch with other updates
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store connection metric for connection {Id}", connection.Id);
        }
    }

    private async Task GenerateConnectionAlertAsync(NetworkConnection connection, string alertType)
    {
        try
        {
            var alert = new Alert
            {
                AlertType = alertType,
                Severity = "INFO",
                Title = $"New Microsoft Connection: {connection.MicrosoftService}",
                Message = $"Process {connection.ProcessName} (PID: {connection.Pid}) connected to {connection.MicrosoftService} at {connection.RemoteHost ?? connection.RemoteIp}",
                ConnectionId = connection.Id,
                ProcessId = connection.Pid,
                CreatedAt = DateTime.UtcNow
            };

            _context.Alerts.Add(alert);
            // Note: Save will happen in batch with other updates

            // Broadcast alert to clients
            await _hubContext.Clients.All.SendAsync("NewAlert", alert);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate alert for connection {Id}", connection.Id);
        }
    }

    private async Task PerformMaintenanceAsync()
    {
        if (DateTime.UtcNow - _lastCleanup < _cleanupInterval)
        {
            return;
        }

        try
        {
            await CleanupOldDataAsync();
            await UpdateSessionStatisticsAsync();
            
            _connectionEnumerator.ClearProcessCache();
            _endpointDetector.ClearDnsCache();
            
            _lastCleanup = DateTime.UtcNow;
            _logger.LogDebug("Maintenance completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform maintenance");
        }
    }

    private async Task CleanupOldDataAsync()
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_maxHistoryDays);

            // Clean up old connections
            var oldConnections = await _context.Connections
                .Where(c => c.EstablishedTime < cutoffDate && !c.IsActive)
                .CountAsync();

            if (oldConnections > 0)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM connections WHERE established_time < {0} AND is_active = 0", cutoffDate);
                
                _logger.LogInformation("Cleaned up {Count} old connection records", oldConnections);
            }

            // Clean up old metrics
            var oldMetrics = await _context.ConnectionMetrics
                .Where(m => m.Timestamp < cutoffDate)
                .CountAsync();

            if (oldMetrics > 0)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM connection_metrics WHERE timestamp < {0}", cutoffDate);
                
                _logger.LogInformation("Cleaned up {Count} old metric records", oldMetrics);
            }

            // Clean up old alerts
            var oldAlerts = await _context.Alerts
                .Where(a => a.CreatedAt < cutoffDate && a.IsAcknowledged)
                .CountAsync();

            if (oldAlerts > 0)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM alerts WHERE created_at < {0} AND is_acknowledged = 1", cutoffDate);
                
                _logger.LogInformation("Cleaned up {Count} old alert records", oldAlerts);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old data");
        }
    }

    private async Task UpdateSessionStatisticsAsync()
    {
        try
        {
            var currentSession = await _context.MonitoringSessions
                .Where(s => s.SessionEnd == null)
                .OrderByDescending(s => s.SessionStart)
                .FirstOrDefaultAsync();

            if (currentSession != null)
            {
                currentSession.TotalConnectionsTracked = _totalConnectionsProcessed;
                currentSession.TotalMicrosoftConnections = _microsoftConnectionsFound;
                
                var totalBytes = await _context.Connections
                    .Where(c => c.EstablishedTime >= currentSession.SessionStart)
                    .SumAsync(c => c.BytesSent + c.BytesReceived);
                
                currentSession.TotalBytesTracked = totalBytes;
                
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session statistics");
        }
    }

    private async Task EndMonitoringSessionAsync()
    {
        try
        {
            var currentSession = await _context.MonitoringSessions
                .Where(s => s.SessionEnd == null)
                .OrderByDescending(s => s.SessionStart)
                .FirstOrDefaultAsync();

            if (currentSession != null)
            {
                currentSession.SessionEnd = DateTime.UtcNow;
                currentSession.SessionNotes += " - Session ended normally";
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to end monitoring session");
        }
    }

    private static string GetConnectionKey(NetworkConnection connection)
    {
        return $"{connection.Pid}:{connection.LocalIp}:{connection.LocalPort}:{connection.RemoteIp}:{connection.RemotePort}:{connection.Protocol}";
    }

    private static string DetermineConnectionQuality(long bytesTransferred)
    {
        return bytesTransferred switch
        {
            > 10_000_000 => "EXCELLENT", // > 10MB
            > 1_000_000 => "GOOD",       // > 1MB
            > 100_000 => "FAIR",         // > 100KB
            _ => "POOR"
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Network Monitor Service stop requested");
        await base.StopAsync(cancellationToken);
    }
}
