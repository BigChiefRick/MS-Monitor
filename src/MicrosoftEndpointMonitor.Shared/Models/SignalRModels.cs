using System.ComponentModel.DataAnnotations;

namespace MicrosoftEndpointMonitor.Shared.Models
{
    public class ConnectionEvent
    {
        public string EventType { get; set; } = string.Empty;
        public NetworkConnection? Connection { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Description { get; set; }
    }

    public class DashboardData
    {
        public int TotalConnections { get; set; }
        public int MicrosoftConnections { get; set; }
        public int ActiveServices { get; set; }
        public double AverageLatency { get; set; }
        public List<ServiceStatistics> Services { get; set; } = new();
        public List<NetworkConnection> RecentConnections { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class ServiceStatistics
    {
        public string ServiceName { get; set; } = string.Empty;
        public int ConnectionCount { get; set; }
        public double AverageLatency { get; set; }
        public List<string> Processes { get; set; } = new();
        public string Status { get; set; } = "Active";
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }

    public class LatencyMeasurement
    {
        public string EndpointAddress { get; set; } = string.Empty;
        public double Latency { get; set; }
        public string Status { get; set; } = "Success";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class NetworkStatistics
    {
        public long TotalBytesReceived { get; set; }
        public long TotalBytesSent { get; set; }
        public int PacketsReceived { get; set; }
        public int PacketsSent { get; set; }
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    }
}
