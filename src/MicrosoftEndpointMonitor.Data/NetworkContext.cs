using Microsoft.EntityFrameworkCore;
using MicrosoftEndpointMonitor.Shared.Models;

namespace MicrosoftEndpointMonitor.Data
{
    public class NetworkContext : DbContext
    {
        public NetworkContext(DbContextOptions<NetworkContext> options) : base(options)
        {
        }

        // Primary tables
        public DbSet<NetworkConnection> Connections { get; set; }
        public DbSet<ConnectionMetric> ConnectionMetrics { get; set; }
        public DbSet<ProcessInfo> Processes { get; set; }
        public DbSet<MicrosoftEndpoint> MicrosoftEndpoints { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<MonitoringSession> MonitoringSessions { get; set; }
        public DbSet<NetworkInterface> NetworkInterfaces { get; set; }
        public DbSet<ConfigurationSetting> ConfigurationSettings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("Data Source=../database/network_monitor.db");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure NetworkConnection
            modelBuilder.Entity<NetworkConnection>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LocalAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.RemoteAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.ProcessName).HasMaxLength(255);
                entity.Property(e => e.ServiceName).HasMaxLength(255);
                entity.Property(e => e.State).HasMaxLength(50);
                entity.Property(e => e.Protocol).HasMaxLength(10);
                entity.HasIndex(e => e.RemoteAddress);
                entity.HasIndex(e => e.ProcessName);
                entity.HasIndex(e => e.Timestamp);
            });

            // Configure ConnectionMetric
            modelBuilder.Entity<ConnectionMetric>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Connection)
                      .WithMany()
                      .HasForeignKey(e => e.ConnectionId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.Timestamp);
            });

            // Configure MicrosoftEndpoint
            modelBuilder.Entity<MicrosoftEndpoint>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.IpRange).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DomainPattern).HasMaxLength(255);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.HasIndex(e => e.ServiceName);
            });

            // Configure ProcessInfo
            modelBuilder.Entity<ProcessInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ExecutablePath).HasMaxLength(500);
                entity.HasIndex(e => e.ProcessId);
                entity.HasIndex(e => e.ProcessName);
            });

            // Configure Alert
            modelBuilder.Entity<Alert>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Severity).HasConversion<int>();
                entity.Property(e => e.Type).HasConversion<int>();
                entity.HasOne(e => e.Connection)
                      .WithMany()
                      .HasForeignKey(e => e.ConnectionId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Severity);
            });

            // Configure MonitoringSession
            modelBuilder.Entity<MonitoringSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ComputerName).IsRequired().HasMaxLength(255);
                entity.HasMany(e => e.Connections)
                      .WithOne()
                      .HasForeignKey("MonitoringSessionId")
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.ComputerName);
            });

            // Configure NetworkInterface
            modelBuilder.Entity<NetworkInterface>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.MacAddress).IsRequired().HasMaxLength(17);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.SubnetMask).HasMaxLength(45);
                entity.Property(e => e.Gateway).HasMaxLength(45);
                entity.HasIndex(e => e.MacAddress).IsUnique();
            });

            // Configure ConfigurationSetting
            modelBuilder.Entity<ConfigurationSetting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasIndex(e => e.Key).IsUnique();
            });

            // Seed Microsoft endpoints
            SeedMicrosoftEndpoints(modelBuilder);
        }

        private void SeedMicrosoftEndpoints(ModelBuilder modelBuilder)
        {
            var endpoints = new List<MicrosoftEndpoint>
            {
                new() { Id = 1, ServiceName = "Microsoft Teams", IpRange = "52.108.0.0/14", DomainPattern = "*.teams.microsoft.com", Category = "Communication" },
                new() { Id = 2, ServiceName = "Microsoft Office 365", IpRange = "52.96.0.0/11", DomainPattern = "*.office365.com", Category = "Productivity" },
                new() { Id = 3, ServiceName = "Microsoft Azure", IpRange = "20.0.0.0/8", DomainPattern = "*.azure.com", Category = "Cloud Services" },
                new() { Id = 4, ServiceName = "Microsoft OneDrive", IpRange = "52.121.0.0/16", DomainPattern = "*.onedrive.com", Category = "Storage" },
                new() { Id = 5, ServiceName = "Microsoft Exchange Online", IpRange = "40.92.0.0/15", DomainPattern = "*.outlook.com", Category = "Email" },
                new() { Id = 6, ServiceName = "Microsoft SharePoint", IpRange = "52.244.0.0/16", DomainPattern = "*.sharepoint.com", Category = "Collaboration" },
                new() { Id = 7, ServiceName = "Microsoft Graph API", IpRange = "40.126.0.0/16", DomainPattern = "graph.microsoft.com", Category = "API" },
                new() { Id = 8, ServiceName = "Microsoft Authentication", IpRange = "40.124.0.0/16", DomainPattern = "login.microsoftonline.com", Category = "Authentication" },
                new() { Id = 9, ServiceName = "Skype for Business", IpRange = "52.114.0.0/16", DomainPattern = "*.lync.com", Category = "Communication" },
                new() { Id = 10, ServiceName = "Microsoft Edge Update", IpRange = "13.107.42.0/24", DomainPattern = "*.msedge.net", Category = "Browser" }
            };

            modelBuilder.Entity<MicrosoftEndpoint>().HasData(endpoints);
        }
    }
}
