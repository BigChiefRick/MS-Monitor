using Microsoft.AspNetCore.SignalR;
using MicrosoftEndpointMonitor.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MicrosoftEndpointMonitor.Api.Hubs
{
    public class NetworkMonitorHub : Hub
    {
        private readonly ILogger<NetworkMonitorHub> _logger;

        public NetworkMonitorHub(ILogger<NetworkMonitorHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogDebug("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
        }
    }

    // Hub extensions for easier usage from other services
    public static class HubExtensions
    {
        public static async Task NotifyConnectionEvent(this IHubContext<NetworkMonitorHub> hubContext, ConnectionEvent connectionEvent)
        {
            await hubContext.Clients.All.SendAsync("ConnectionEvent", connectionEvent);
        }

        public static async Task NotifyDashboardUpdate(this IHubContext<NetworkMonitorHub> hubContext, DashboardData dashboardData)
        {
            await hubContext.Clients.All.SendAsync("DashboardUpdate", dashboardData);
        }

        public static async Task NotifyServiceUpdate(this IHubContext<NetworkMonitorHub> hubContext, string serviceName, ServiceStatistics serviceStats)
        {
            await hubContext.Clients.Group($"Service_{serviceName}").SendAsync("ServiceUpdate", serviceStats);
        }

        public static async Task NotifyAlert(this IHubContext<NetworkMonitorHub> hubContext, Alert alert)
        {
            await hubContext.Clients.All.SendAsync("AlertReceived", alert);
        }
    }
}
