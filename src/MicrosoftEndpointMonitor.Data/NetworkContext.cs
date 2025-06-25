using Microsoft.EntityFrameworkCore;
using MicrosoftEndpointMonitor.Shared.Models;

namespace MicrosoftEndpointMonitor.Data;

/// <summary>
/// Entity Framework DbContext for the network monitoring database
/// </summary>
public class NetworkContext : DbContext
{
    public NetworkContext(DbContextOptions<NetworkContext> options) : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<NetworkConnection> Connections { get; set; }
    public DbSet<MicrosoftEndpoint> MicrosoftEndpoints { get; set; }
    public DbSet<ConnectionMetric> ConnectionMetrics { get; set; }
    public DbSet<ProcessInfo> Processes { get; set; }
    public DbSet<MonitoringSession> MonitoringSessions { get; set; }
    public DbSet<NetworkInterface> NetworkInterfaces { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<ConfigurationSetting> Configuration { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure NetworkConnection entity
        modelBuilder.Entity<NetworkConnection>(entity =>
        {
            entity.ToTable("connections");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Pid).HasColumnName("pid").IsRequired();
            entity.Property(e => e.ProcessName).HasColumnName("process_name").IsRequired().HasMaxLength(255);
            entity.Property(e => e.ProcessPath).HasColumnName("process_path").HasMaxLength(500);
            entity.Property(e => e.ProcessCommandLine).HasColumnName("process_command_line").HasMaxLength(1000);
            entity.Property(e => e.LocalIp).HasColumnName("local_ip").IsRequired().HasMaxLength(45);
            entity.Property(e => e.LocalPort).HasColumnName("local_port").IsRequired();
            entity.Property(e => e.RemoteIp).HasColumnName("remote_ip").IsRequired().HasMaxLength(45);
            entity.Property(e => e.RemotePort).HasColumnName("remote_port").IsRequired();
            entity.Property(e => e.RemoteHost).HasColumnName("remote_host").HasMaxLength(255);
            entity.Property(e => e.MicrosoftService).HasColumnName("microsoft_service").HasMaxLength(100);
            entity.Property(e => e.ServiceCategory).HasColumnName("service_category").HasMaxLength(100);
            entity.Property(e => e.ConnectionState).HasColumnName("connection_state").IsRequired().HasMaxLength(50);
            entity.Property(e => e.Protocol).HasColumnName("protocol").HasMaxLength(10).HasDefaultValue("TCP");
            entity.Property(e => e.BytesSent).HasColumnName("bytes_sent").HasDefaultValue(0);
            entity.Property(e => e.BytesReceived).HasColumnName("bytes_received").HasDefaultValue(0);
            entity.Property(e => e.PacketsSent).HasColumnName("packets_sent").HasDefaultValue(0);
            entity.Property(e => e.PacketsReceived).HasColumnName("packets_received").HasDefaultValue(0);
            entity.Property(e => e.EstablishedTime).HasColumnName("established_time").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastActivityTime).HasColumnName("last_activity_time").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ClosedTime).HasColumnName("closed_time");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            entity.HasIndex(e => e.Pid).HasDatabaseName("idx_connections_pid");
            entity.HasIndex(e => e.RemoteIp).HasDatabaseName("idx_connections_remote_ip");
            entity.HasIndex(e => e.MicrosoftService).HasDatabaseName("idx_connections_service");
            entity.HasIndex(e => e.EstablishedTime).HasDatabaseName("idx_connections_established_time");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_connections_active");
            entity.HasIndex(e => e.ConnectionState).HasDatabaseName("idx_connections_state");
        });

        // Configure MicrosoftEndpoint entity
        modelBuilder.Entity<MicrosoftEndpoint>(entity =>
        {
            entity.ToTable("microsoft_endpoints");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IpRange).HasColumnName("ip_range").HasMaxLength(50);
            entity.Property(e => e.DomainPattern).HasColumnName("domain_pattern").HasMaxLength(255);
            entity.Property(e => e.ServiceName).HasColumnName("service_name").IsRequired().HasMaxLength(100);
            entity.Property(e => e.ServiceCategory).HasColumnName("service_category").HasMaxLength(100);
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.Priority).HasColumnName("priority").HasDefaultValue(0);
            entity.Property(e => e.LastUpdated).HasColumnName("last_updated").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            entity.HasIndex(e => e.ServiceName).HasDatabaseName("idx_endpoints_service");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_endpoints_active");
        });

        // Configure ConnectionMetric entity
        modelBuilder.Entity<ConnectionMetric>(entity =>
        {
            entity.ToTable("connection_metrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConnectionId).HasColumnName("connection_id");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.BytesPerSecondIn).HasColumnName("bytes_per_second_in").HasDefaultValue(0);
            entity.Property(e => e.BytesPerSecondOut).HasColumnName("bytes_per_second_out").HasDefaultValue(0);
            entity.Property(e => e.PacketsPerSecondIn).HasColumnName("packets_per_second_in").HasDefaultValue(0);
            entity.Property(e => e.PacketsPerSecondOut).HasColumnName("packets_per_second_out").HasDefaultValue(0);
            entity.Property(e => e.LatencyMs).HasColumnName("latency_ms");
            entity.Property(e => e.PacketLossRate).HasColumnName("packet_loss_rate").HasDefaultValue(0.0);
            entity.Property(e => e.JitterMs).HasColumnName("jitter_ms");
            entity.Property(e => e.ConnectionQuality).HasColumnName("connection_quality").HasMaxLength(20);

            // Foreign key
            entity.HasOne<NetworkConnection>()
                  .WithMany()
                  .HasForeignKey(e => e.ConnectionId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.ConnectionId).HasDatabaseName("idx_metrics_connection_id");
            entity.HasIndex(e => e.Timestamp).HasDatabaseName("idx_metrics_timestamp");
        });

        // Configure ProcessInfo entity
        modelBuilder.Entity<ProcessInfo>(entity =>
        {
            entity.ToTable("processes");
            entity.HasKey(e => e.Pid);
            entity.Property(e => e.Pid).HasColumnName("pid");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
            entity.Property(e => e.ExecutablePath).HasColumnName("executable_path").HasMaxLength(500);
            entity.Property(e => e.CommandLine).HasColumnName("command_line").HasMaxLength(1000);
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.UserName).HasColumnName("user_name").HasMaxLength(100);
            entity.Property(e => e.IsMicrosoftApp).HasColumnName("is_microsoft_app").HasDefaultValue(false);
            entity.Property(e => e.AppVersion).HasColumnName("app_version").HasMaxLength(50);
            entity.Property(e => e.AppDescription).HasColumnName("app_description").HasMaxLength(500);
            entity.Property(e => e.LastSeen).HasColumnName("last_seen").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            entity.HasIndex(e => e.Name).HasDatabaseName("idx_processes_name");
            entity.HasIndex(e => e.IsMicrosoftApp).HasDatabaseName("idx_processes_microsoft");
        });

        // Configure MonitoringSession entity
        modelBuilder.Entity<MonitoringSession>(entity =>
        {
            entity.ToTable("monitoring_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SessionStart).HasColumnName("session_start").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.SessionEnd).HasColumnName("session_end");
            entity.Property(e => e.ComputerName).HasColumnName("computer_name").HasMaxLength(100);
            entity.Property(e => e.WindowsVersion).HasColumnName("windows_version").HasMaxLength(100);
            entity.Property(e => e.ServiceVersion).HasColumnName("service_version").HasMaxLength(50);
            entity.Property(e => e.TotalConnectionsTracked).HasColumnName("total_connections_tracked").HasDefaultValue(0);
            entity.Property(e => e.TotalMicrosoftConnections).HasColumnName("total_microsoft_connections").HasDefaultValue(0);
            entity.Property(e => e.TotalBytesTracked).HasColumnName("total_bytes_tracked").HasDefaultValue(0);
            entity.Property(e => e.SessionNotes).HasColumnName("session_notes").HasMaxLength(1000);
        });

        // Configure NetworkInterface entity
        modelBuilder.Entity<NetworkInterface>(entity =>
        {
            entity.ToTable("network_interfaces");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.InterfaceName).HasColumnName("interface_name").IsRequired().HasMaxLength(255);
            entity.Property(e => e.InterfaceDescription).HasColumnName("interface_description").HasMaxLength(500);
            entity.Property(e => e.MacAddress).HasColumnName("mac_address").HasMaxLength(18);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.SubnetMask).HasColumnName("subnet_mask").HasMaxLength(45);
            entity.Property(e => e.Gateway).HasColumnName("gateway").HasMaxLength(45);
            entity.Property(e => e.DnsServers).HasColumnName("dns_servers").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.InterfaceType).HasColumnName("interface_type").HasMaxLength(50);
            entity.Property(e => e.LastSeen).HasColumnName("last_seen").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Configure Alert entity
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("alerts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AlertType).HasColumnName("alert_type").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Severity).HasColumnName("severity").IsRequired().HasMaxLength(20);
            entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(255);
            entity.Property(e => e.Message).HasColumnName("message").IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ConnectionId).HasColumnName("connection_id");
            entity.Property(e => e.ProcessId).HasColumnName("process_id");
            entity.Property(e => e.Data).HasColumnName("data").HasColumnType("TEXT");
            entity.Property(e => e.IsAcknowledged).HasColumnName("is_acknowledged").HasDefaultValue(false);
            entity.Property(e => e.AcknowledgedAt).HasColumnName("acknowledged_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Foreign key
            entity.HasOne<NetworkConnection>()
                  .WithMany()
                  .HasForeignKey(e => e.ConnectionId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => e.AlertType).HasDatabaseName("idx_alerts_type");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_alerts_created");
            entity.HasIndex(e => e.IsAcknowledged).HasDatabaseName("idx_alerts_acknowledged");
        });

        // Configure ConfigurationSetting entity
        modelBuilder.Entity<ConfigurationSetting>(entity =>
        {
            entity.ToTable("configuration");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConfigKey).HasColumnName("config_key").IsRequired().HasMaxLength(255);
            entity.Property(e => e.ConfigValue).HasColumnName("config_value").HasMaxLength(1000);
            entity.Property(e => e.ConfigType).HasColumnName("config_type").HasMaxLength(20).HasDefaultValue("string");
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.IsUserConfigurable).HasColumnName("is_user_configurable").HasDefaultValue(true);
            entity.Property(e => e.LastModified).HasColumnName("last_modified").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique constraint on config_key
            entity.HasIndex(e => e.ConfigKey).IsUnique();
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Default SQLite connection string
            optionsBuilder.UseSqlite("Data Source=../database/network_monitor.db");
        }
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is NetworkConnection connection)
            {
                connection.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
