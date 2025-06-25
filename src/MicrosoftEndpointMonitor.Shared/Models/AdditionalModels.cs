using System.ComponentModel.DataAnnotations;

namespace MicrosoftEndpointMonitor.Shared.Models
{
    public class MicrosoftEndpoint
    {
        public int Id { get; set; }
        
        [Required]
        public string ServiceName { get; set; } = string.Empty;
        
        [Required]
        public string IpRange { get; set; } = string.Empty;
        
        public string? DomainPattern { get; set; }
        
        public string Category { get; set; } = "Microsoft Service";
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastUpdated { get; set; }
    }

    public class MonitoringSession
    {
        public int Id { get; set; }
        
        [Required]
        public string ComputerName { get; set; } = string.Empty;
        
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        
        public DateTime? EndTime { get; set; }
        
        public int TotalConnections { get; set; }
        
        public int MicrosoftConnections { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public ICollection<NetworkConnection> Connections { get; set; } = new List<NetworkConnection>();
    }

    public class NetworkInterface
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string MacAddress { get; set; } = string.Empty;
        
        public string? IpAddress { get; set; }
        
        public string? SubnetMask { get; set; }
        
        public string? Gateway { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public long BytesSent { get; set; }
        
        public long BytesReceived { get; set; }
        
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }

    public class Alert
    {
        public int Id { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
        
        public AlertType Type { get; set; } = AlertType.NetworkConnection;
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ReadAt { get; set; }
        
        // Optional connection reference
        public int? ConnectionId { get; set; }
        public NetworkConnection? Connection { get; set; }
    }

    public class ConfigurationSetting
    {
        public int Id { get; set; }
        
        [Required]
        public string Key { get; set; } = string.Empty;
        
        [Required]
        public string Value { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
    }

    public enum AlertSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    public enum AlertType
    {
        NetworkConnection = 0,
        LatencyThreshold = 1,
        ConnectionLost = 2,
        NewEndpoint = 3,
        SystemError = 4
    }
}
