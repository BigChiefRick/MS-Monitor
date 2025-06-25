using System.ComponentModel.DataAnnotations;

namespace MicrosoftEndpointMonitor.Shared.Models;

/// <summary>
/// Represents a network connection to a Microsoft endpoint
/// </summary>
public class NetworkConnection
{
    public int Id { get; set; }
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? ProcessPath { get; set; }
    public string? ProcessCommandLine { get; set; }
    public string LocalIp { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteIp { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string? RemoteHost { get; set; }
    public string? MicrosoftService { get; set; }
    public string? ServiceCategory { get; set; }
    public string ConnectionState { get; set; } = string.Empty;
    public string Protocol { get; set; } = "TCP";
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public int PacketsSent { get; set; }
    public int PacketsReceived { get; set; }
    public DateTime EstablishedTime { get; set; }
    public DateTime LastActivityTime { get; set; }
    public DateTime? ClosedTime { get; set; }
    public int? DurationMs { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Computed properties
    public long TotalBytes => BytesSent + BytesReceived;
    public bool IsMicrosoftEndpoint => !string.IsNullOrEmpty(MicrosoftService);
    public TimeSpan Duration => ClosedTime?.Subtract(EstablishedTime) ?? DateTime.UtcNow.Subtract(EstablishedTime);
}

/// <summary>
/// Real-time connection event for SignalR broadcasting
/// </summary>
public class ConnectionEvent
{
    public string EventType { get; set; } = string.Empty; // "CONNECTED", "DISCONNECTED", "DATA_TRANSFER"
    public NetworkConnection Connection { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long? BytesTransferred { get; set; }
    public string? AdditionalData { get; set; }
}

/// <summary>
/// Process information associated with network connections
/// </summary>
public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? CommandLine { get; set; }
    public DateTime? StartTime { get; set; }
    public string? UserName { get; set; }
    public bool IsMicrosoftApp { get; set; }
    public string? AppVersion { get; set; }
    public string? AppDescription { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Microsoft endpoint definition for service detection
/// </summary>
public class MicrosoftEndpoint
{
    public int Id { get; set; }
    public string? IpRange { get; set; }
    public string? DomainPattern { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? ServiceCategory { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Connection performance metrics
/// </summary>
public class ConnectionMetric
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public DateTime Timestamp { get; set; }
    public int BytesPerSecondIn { get; set; }
    public int BytesPerSecondOut { get; set; }
    public int PacketsPerSecondIn { get; set; }
    public int PacketsPerSecondOut { get; set; }
    public int? LatencyMs { get; set; }
    public double PacketLossRate { get; set; }
    public int? JitterMs { get; set; }
    public string? ConnectionQuality { get; set; }
}

/// <summary>
/// Alert/notification model
/// </summary>
public class Alert
{
    public int Id { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? ConnectionId { get; set; }
    public int? ProcessId { get; set; }
    public string? Data { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Network interface information
/// </summary>
public class NetworkInterface
{
    public int Id { get; set; }
    public string InterfaceName { get; set; } = string.Empty;
    public string? InterfaceDescription { get; set; }
    public string? MacAddress { get; set; }
    public string? IpAddress { get; set; }
    public string? SubnetMask { get; set; }
    public string? Gateway { get; set; }
    public string? DnsServers { get; set; }
    public bool IsActive { get; set; }
    public string? InterfaceType { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Monitoring session information
/// </summary>
public class MonitoringSession
{
    public int Id { get; set; }
    public DateTime SessionStart { get; set; }
    public DateTime? SessionEnd { get; set; }
    public string? ComputerName { get; set; }
    public string? WindowsVersion { get; set; }
    public string? ServiceVersion { get; set; }
    public int TotalConnectionsTracked { get; set; }
    public int TotalMicrosoftConnections { get; set; }
    public long TotalBytesTracked { get; set; }
    public string? SessionNotes { get; set; }
}

/// <summary>
/// Service statistics summary
/// </summary>
public class ServiceStatistics
{
    public string ServiceName { get; set; } = string.Empty;
    public string? ServiceCategory { get; set; }
    public int ConnectionCount { get; set; }
    public long TotalBytes { get; set; }
    public double AverageDurationMs { get; set; }
    public DateTime FirstConnection { get; set; }
    public DateTime LastActivity { get; set; }
    public List<string> ProcessesUsing { get; set; } = new();
}

/// <summary>
/// Real-time dashboard data
/// </summary>
public class DashboardData
{
    public int ActiveConnections { get; set; }
    public int MicrosoftConnections { get; set; }
    public long CurrentBandwidthBytesPerSecond { get; set; }
    public List<ServiceStatistics> TopServices { get; set; } = new();
    public List<NetworkConnection> RecentConnections { get; set; } = new();
    public List<Alert> RecentAlerts { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Connection filter criteria
/// </summary>
public class ConnectionFilter
{
    public string? ProcessName { get; set; }
    public string? MicrosoftService { get; set; }
    public string? ServiceCategory { get; set; }
    public string? ConnectionState { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool? IsActive { get; set; }
    public int? MinBytes { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 100;
    public string SortBy { get; set; } = "EstablishedTime";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Paged result wrapper
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage => PageNumber * PageSize < TotalCount;
    public bool HasPreviousPage => PageNumber > 1;
}

/// <summary>
/// Configuration setting
/// </summary>
public class ConfigurationSetting
{
    public int Id { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string? ConfigValue { get; set; }
    public string ConfigType { get; set; } = "string";
    public string? Description { get; set; }
    public bool IsUserConfigurable { get; set; } = true;
    public DateTime LastModified { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// TCP connection state enumeration
/// </summary>
public enum TcpConnectionState
{
    Unknown = 0,
    Closed = 1,
    Listen = 2,
    SynSent = 3,
    SynRcvd = 4,
    Established = 5,
    FinWait1 = 6,
    FinWait2 = 7,
    CloseWait = 8,
    Closing = 9,
    LastAck = 10,
    TimeWait = 11,
    DeleteTcb = 12
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Connection quality levels
/// </summary>
public enum ConnectionQuality
{
    Unknown,
    Poor,
    Fair,
    Good,
    Excellent
}
