using Microsoft.AspNetCore.SignalR;
using MicrosoftEndpointMonitor.Shared.Models;

namespace MicrosoftEndpointMonitor.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time network monitoring updates
/// </summary>
public class NetworkMonitorHub : Hub
{
    private readonly ILogger<NetworkMonitorHub> _logger;

    public NetworkMonitorHub(ILogger<NetworkMonitorHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client connects to start receiving monitoring updates
    /// </summary>
    public async Task JoinMonitoring()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "MonitoringClients");
        _logger.LogDebug("Client {ConnectionId} joined monitoring group", Context.ConnectionId);
    }

    /// <summary>
    /// Client disconnects from monitoring updates
    /// </summary>
    public async Task LeaveMonitoring()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "MonitoringClients");
        _logger.LogDebug("Client {ConnectionId} left monitoring group", Context.ConnectionId);
    }

    /// <summary>
    /// Client subscribes to specific service updates
    /// </summary>
    public async Task SubscribeToService(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return;
        }

        var groupName = $"Service_{serviceName}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} subscribed to service {ServiceName}", Context.ConnectionId, serviceName);
    }

    /// <summary>
    /// Client unsubscribes from specific service updates
    /// </summary>
    public async Task UnsubscribeFromService(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return;
        }

        var groupName = $"Service_{serviceName}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("Client {ConnectionId} unsubscribed from service {ServiceName}", Context.ConnectionId, serviceName);
    }

    /// <summary>
    /// Client requests current dashboard data
    /// </summary>
    public async Task RequestDashboardData()
    {
        try
        {
            // This would be injected from a service in a real implementation
            var dashboardData = new DashboardData
            {
                ActiveConnections = 0,
                MicrosoftConnections = 0,
                CurrentBandwidthBytesPerSecond = 0,
                TopServices = new List<ServiceStatistics>(),
                RecentConnections = new List<NetworkConnection>(),
                RecentAlerts = new List<Alert>(),
                LastUpdated = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("DashboardData", dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send dashboard data to client {ConnectionId}", Context.ConnectionId);
        }
    }

    /// <summary>
    /// Client acknowledges an alert
    /// </summary>
    public async Task AcknowledgeAlert(int alertId)
    {
        try
        {
            // This would update the alert in the database
            _logger.LogDebug("Client {ConnectionId} acknowledged alert {AlertId}", Context.ConnectionId, alertId);
            
            // Broadcast the acknowledgment to other clients
            await Clients.Others.SendAsync("AlertAcknowledged", alertId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge alert {AlertId} for client {ConnectionId}", alertId, Context.ConnectionId);
        }
    }

    /// <summary>
    /// Client requests historical data for a specific time range
    /// </summary>
    public async Task RequestHistoricalData(DateTime startTime, DateTime endTime, string? serviceName = null)
    {
        try
        {
            if (endTime <= startTime || (endTime - startTime).TotalDays > 7)
            {
                await Clients.Caller.SendAsync("Error", "Invalid time range. Maximum 7 days allowed.");
                return;
            }

            // This would fetch historical data from the database
            var historicalData = new
            {
                StartTime = startTime,
                EndTime = endTime,
                ServiceName = serviceName,
                Connections = new List<NetworkConnection>(),
                Metrics = new List<ConnectionMetric>()
            };

            await Clients.Caller.SendAsync("HistoricalData", historicalData);
            _logger.LogDebug("Sent historical data to client {ConnectionId} for period {StartTime} to {EndTime}",
                Context.ConnectionId, startTime, endTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send historical data to client {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", "Failed to retrieve historical data");
        }
    }

    /// <summary>
    /// Client connection established
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId} from {UserAgent}",
            Context.ConnectionId,
            Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString() ?? "Unknown");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Client connection terminated
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Extension methods for strongly-typed SignalR client calls
/// </summary>
public static class NetworkMonitorHubExtensions
{
    /// <summary>
    /// Broadcast connection update to all monitoring clients
    /// </summary>
    public static async Task BroadcastConnectionUpdate(this IHubContext<NetworkMonitorHub> hubContext, ConnectionEvent connectionEvent)
    {
        await hubContext.Clients.Group("MonitoringClients").SendAsync("ConnectionUpdate", connectionEvent);
        
        // Also send to service-specific subscribers
        if (!string.IsNullOrEmpty(connectionEvent.Connection.MicrosoftService))
        {
            var serviceGroup = $"Service_{connectionEvent.Connection.MicrosoftService}";
            await hubContext.Clients.Group(serviceGroup).SendAsync("ServiceConnectionUpdate", connectionEvent);
        }
    }

    /// <summary>
    /// Broadcast new alert to all monitoring clients
    /// </summary>
    public static async Task BroadcastAlert(this IHubContext<NetworkMonitorHub> hubContext, Alert alert)
    {
        await hubContext.Clients.Group("MonitoringClients").SendAsync("NewAlert", alert);
    }

    /// <summary>
    /// Broadcast dashboard update to all monitoring clients
    /// </summary>
    public static async Task BroadcastDashboardUpdate(this IHubContext<NetworkMonitorHub> hubContext, DashboardData dashboardData)
    {
        await hubContext.Clients.Group("MonitoringClients").SendAsync("DashboardUpdate", dashboardData);
    }

    /// <summary>
    /// Broadcast service statistics update
    /// </summary>
    public static async Task BroadcastServiceStatistics(this IHubContext<NetworkMonitorHub> hubContext, List<ServiceStatistics> statistics)
    {
        await hubContext.Clients.Group("MonitoringClients").SendAsync("ServiceStatisticsUpdate", statistics);
    }

    /// <summary>
    /// Broadcast system status update
    /// </summary>
    public static async Task BroadcastSystemStatus(this IHubContext<NetworkMonitorHub> hubContext, object statusData)
    {
        await hubContext.Clients.Group("MonitoringClients").SendAsync("SystemStatusUpdate", statusData);
    }
}
