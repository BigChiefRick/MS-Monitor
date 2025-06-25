using System.Net;
using System.Text.RegularExpressions;
using MicrosoftEndpointMonitor.Shared.Models;
using MicrosoftEndpointMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace MicrosoftEndpointMonitor.Service.Services;

/// <summary>
/// Service for detecting and categorizing Microsoft endpoints
/// </summary>
public class MicrosoftEndpointDetector
{
    private readonly ILogger<MicrosoftEndpointDetector> _logger;
    private readonly NetworkContext _context;
    private List<MicrosoftEndpoint> _endpoints = new();
    private DateTime _lastEndpointUpdate = DateTime.MinValue;
    private readonly TimeSpan _endpointCacheExpiry = TimeSpan.FromHours(1);

    // DNS resolution cache
    private readonly Dictionary<string, string> _dnsCache = new();
    private readonly Dictionary<string, DateTime> _dnsCacheTimestamps = new();
    private readonly TimeSpan _dnsCacheExpiry = TimeSpan.FromMinutes(30);

    public MicrosoftEndpointDetector(ILogger<MicrosoftEndpointDetector> logger, NetworkContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Detects if a connection is to a Microsoft endpoint and categorizes it
    /// </summary>
    public async Task<(string? ServiceName, string? Category)> DetectMicrosoftServiceAsync(NetworkConnection connection)
    {
        try
        {
            await EnsureEndpointsLoadedAsync();

            // Try hostname-based detection first (more accurate)
            var hostname = await ResolveHostnameAsync(connection.RemoteIp);
            if (!string.IsNullOrEmpty(hostname))
            {
                connection.RemoteHost = hostname;
                var (serviceName, category) = DetectByHostname(hostname);
                if (!string.IsNullOrEmpty(serviceName))
                {
                    _logger.LogDebug("Detected Microsoft service {Service} for hostname {Hostname}", serviceName, hostname);
                    return (serviceName, category);
                }
            }

            // Fall back to IP range detection
            var ipResult = DetectByIpRange(connection.RemoteIp);
            if (!string.IsNullOrEmpty(ipResult.ServiceName))
            {
                _logger.LogDebug("Detected Microsoft service {Service} for IP {IP}", ipResult.ServiceName, connection.RemoteIp);
                return ipResult;
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect Microsoft service for {IP}", connection.RemoteIp);
            return (null, null);
        }
    }

    /// <summary>
    /// Detects Microsoft service by hostname/domain
    /// </summary>
    private (string? ServiceName, string? Category) DetectByHostname(string hostname)
    {
        var endpoints = _endpoints
            .Where(e => e.IsActive && !string.IsNullOrEmpty(e.DomainPattern))
            .OrderByDescending(e => e.Priority)
            .ToList();

        foreach (var endpoint in endpoints)
        {
            if (IsHostnameMatch(hostname, endpoint.DomainPattern!))
            {
                return (endpoint.ServiceName, endpoint.ServiceCategory);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Detects Microsoft service by IP range
    /// </summary>
    private (string? ServiceName, string? Category) DetectByIpRange(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            return (null, null);
        }

        var endpoints = _endpoints
            .Where(e => e.IsActive && !string.IsNullOrEmpty(e.IpRange))
            .OrderByDescending(e => e.Priority)
            .ToList();

        foreach (var endpoint in endpoints)
        {
            if (IsIpInRange(ip, endpoint.IpRange!))
            {
                return (endpoint.ServiceName, endpoint.ServiceCategory);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Checks if hostname matches a domain pattern
    /// </summary>
    private bool IsHostnameMatch(string hostname, string pattern)
    {
        try
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(hostname, regexPattern, RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to match hostname {Hostname} against pattern {Pattern}", hostname, pattern);
            return false;
        }
    }

    /// <summary>
    /// Checks if IP address is within a CIDR range
    /// </summary>
    private bool IsIpInRange(IPAddress ip, string cidrRange)
    {
        try
        {
            var parts = cidrRange.Split('/');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0], out var networkAddress) || 
                !int.TryParse(parts[1], out var prefixLength))
            {
                return false;
            }

            // Ensure IP address families match
            if (ip.AddressFamily != networkAddress.AddressFamily)
            {
                return false;
            }

            var ipBytes = ip.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            if (ipBytes.Length != networkBytes.Length)
            {
                return false;
            }

            var bytesToCheck = prefixLength / 8;
            var bitsToCheck = prefixLength % 8;

            // Check full bytes
            for (int i = 0; i < bytesToCheck; i++)
            {
                if (ipBytes[i] != networkBytes[i])
                {
                    return false;
                }
            }

            // Check remaining bits in the last byte
            if (bitsToCheck > 0 && bytesToCheck < ipBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - bitsToCheck));
                if ((ipBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if IP {IP} is in range {Range}", ip, cidrRange);
            return false;
        }
    }

    /// <summary>
    /// Resolves hostname from IP address with caching
    /// </summary>
    private async Task<string?> ResolveHostnameAsync(string ipAddress)
    {
        try
        {
            // Check cache first
            if (_dnsCache.TryGetValue(ipAddress, out var cachedHostname) &&
                _dnsCacheTimestamps.TryGetValue(ipAddress, out var cacheTime) &&
                DateTime.UtcNow - cacheTime < _dnsCacheExpiry)
            {
                return cachedHostname;
            }

            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                return null;
            }

            // Perform DNS lookup
            var hostEntry = await Dns.GetHostEntryAsync(ip);
            var hostname = hostEntry.HostName?.ToLowerInvariant();

            if (!string.IsNullOrEmpty(hostname))
            {
                _dnsCache[ipAddress] = hostname;
                _dnsCacheTimestamps[ipAddress] = DateTime.UtcNow;
            }

            return hostname;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve hostname for IP {IP}", ipAddress);
            return null;
        }
    }

    /// <summary>
    /// Ensures Microsoft endpoints are loaded from database
    /// </summary>
    private async Task EnsureEndpointsLoadedAsync()
    {
        if (DateTime.UtcNow - _lastEndpointUpdate < _endpointCacheExpiry && _endpoints.Any())
        {
            return;
        }

        try
        {
            _endpoints = await _context.MicrosoftEndpoints
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.Priority)
                .ToListAsync();

            _lastEndpointUpdate = DateTime.UtcNow;
            _logger.LogDebug("Loaded {Count} Microsoft endpoints from database", _endpoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Microsoft endpoints from database");
        }
    }

    /// <summary>
    /// Adds or updates a Microsoft endpoint definition
    /// </summary>
    public async Task<bool> AddOrUpdateEndpointAsync(MicrosoftEndpoint endpoint)
    {
        try
        {
            var existing = await _context.MicrosoftEndpoints
                .FirstOrDefaultAsync(e => 
                    e.IpRange == endpoint.IpRange && 
                    e.DomainPattern == endpoint.DomainPattern &&
                    e.ServiceName == endpoint.ServiceName);

            if (existing != null)
            {
                existing.ServiceCategory = endpoint.ServiceCategory;
                existing.Description = endpoint.Description;
                existing.Priority = endpoint.Priority;
                existing.IsActive = endpoint.IsActive;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                endpoint.CreatedAt = DateTime.UtcNow;
                endpoint.LastUpdated = DateTime.UtcNow;
                _context.MicrosoftEndpoints.Add(endpoint);
            }

            await _context.SaveChangesAsync();
            
            // Force reload of endpoints
            _lastEndpointUpdate = DateTime.MinValue;
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add/update Microsoft endpoint");
            return false;
        }
    }

    /// <summary>
    /// Gets service statistics for Microsoft endpoints
    /// </summary>
    public async Task<List<ServiceStatistics>> GetServiceStatisticsAsync(DateTime? startTime = null, DateTime? endTime = null)
    {
        try
        {
            var query = _context.Connections
                .Where(c => !string.IsNullOrEmpty(c.MicrosoftService));

            if (startTime.HasValue)
            {
                query = query.Where(c => c.EstablishedTime >= startTime.Value);
            }

            if (endTime.HasValue)
            {
                query = query.Where(c => c.EstablishedTime <= endTime.Value);
            }

            var statistics = await query
                .GroupBy(c => new { c.MicrosoftService, c.ServiceCategory })
                .Select(g => new ServiceStatistics
                {
                    ServiceName = g.Key.MicrosoftService!,
                    ServiceCategory = g.Key.ServiceCategory,
                    ConnectionCount = g.Count(),
                    TotalBytes = g.Sum(c => c.BytesSent + c.BytesReceived),
                    AverageDurationMs = g.Average(c => c.DurationMs ?? 0),
                    FirstConnection = g.Min(c => c.EstablishedTime),
                    LastActivity = g.Max(c => c.LastActivityTime),
                    ProcessesUsing = g.Select(c => c.ProcessName).Distinct().ToList()
                })
                .OrderByDescending(s => s.TotalBytes)
                .ToListAsync();

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service statistics");
            return new List<ServiceStatistics>();
        }
    }

    /// <summary>
    /// Clears DNS resolution cache
    /// </summary>
    public void ClearDnsCache()
    {
        _dnsCache.Clear();
        _dnsCacheTimestamps.Clear();
        _logger.LogDebug("DNS cache cleared");
    }

    /// <summary>
    /// Gets cache statistics for monitoring
    /// </summary>
    public (int EndpointsCount, int DnsCacheSize, DateTime LastEndpointUpdate) GetCacheStats()
    {
        return (_endpoints.Count, _dnsCache.Count, _lastEndpointUpdate);
    }

    /// <summary>
    /// Checks if a process is likely a Microsoft application
    /// </summary>
    public bool IsMicrosoftProcess(string processName, string? processPath = null)
    {
        if (string.IsNullOrEmpty(processName))
            return false;

        var microsoftProcesses = new[]
        {
            "teams", "outlook", "winword", "excel", "powerpnt", "msedge", "msedgewebview2",
            "onedrive", "skype", "lync", "communicator", "microsoftedge", "iexplore",
            "onenotem", "onenote", "msteams", "ms-teams", "microsoftteams",
            "windowsstore", "calculator", "notepad", "paint", "mspaint",
            "svchost", "dwm", "csrss", "winlogon", "explorer", "conhost"
        };

        var processNameLower = processName.ToLowerInvariant();
        
        // Check against known Microsoft process names
        if (microsoftProcesses.Any(mp => processNameLower.Contains(mp)))
        {
            return true;
        }

        // Check path if available
        if (!string.IsNullOrEmpty(processPath))
        {
            var pathLower = processPath.ToLowerInvariant();
            if (pathLower.Contains("microsoft") || 
                pathLower.Contains("windows") ||
                pathLower.Contains("program files\\microsoft") ||
                pathLower.Contains("program files (x86)\\microsoft"))
            {
                return true;
            }
        }

        return false;
    }
}
