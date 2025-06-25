using System.ComponentModel.DataAnnotations;

namespace MicrosoftEndpointMonitor.Shared.Models
{
    public class NetworkConnection
    {
        public int Id { get; set; }
        
        [Required]
        public string LocalAddress { get; set; } = string.Empty;
        
        public int LocalPort { get; set; }
        
        [Required]
        public string RemoteAddress { get; set; } = string.Empty;
        
        public int RemotePort { get; set; }
        
        public string ProcessName { get; set; } = string.Empty;
        
        public int ProcessId { get; set; }
        
        public string ServiceName { get; set; } = string.Empty;
        
        public bool IsMicrosoftEndpoint { get; set; }
        
        public double? Latency { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public string State { get; set; } = "ESTABLISHED";
        
        public string Protocol { get; set; } = "TCP";
    }
    
    public class ConnectionMetric
    {
        public int Id { get; set; }
        public int ConnectionId { get; set; }
        public double Latency { get; set; }
        public int PacketsReceived { get; set; }
        public int PacketsSent { get; set; }
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public NetworkConnection? Connection { get; set; }
    }
    
    public class ProcessInfo
    {
        public int Id { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public bool IsMicrosoftProcess { get; set; }
        public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }
}
