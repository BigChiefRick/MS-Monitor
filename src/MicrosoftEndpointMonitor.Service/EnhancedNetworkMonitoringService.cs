using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace MicrosoftEndpointMonitor.Service
{
    public class EnhancedNetworkMonitoringService : BackgroundService
    {
        private readonly ILogger<EnhancedNetworkMonitoringService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        
        // Monitoring targets (similar to ThousandEyes agents)
        private readonly Dictionary<string, EndpointTarget> _monitoringTargets;
        private readonly Dictionary<string, List<LatencyMeasurement>> _latencyHistory;
        private readonly Dictionary<string, RouteTraceResult> _routeTraces;
        
        // Change detection
        private readonly Dictionary<string, EndpointState> _previousStates;
        private readonly List<ChangeEvent> _changeLog;
        
        private Timer _monitoringTimer;
        private Timer _routeTraceTimer;
        private Timer _changeDetectionTimer;

        public EnhancedNetworkMonitoringService(
            ILogger<EnhancedNetworkMonitoringService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            
            _monitoringTargets = InitializeMonitoringTargets();
            _latencyHistory = new Dictionary<string, List<LatencyMeasurement>>();
            _routeTraces = new Dictionary<string, RouteTraceResult>();
            _previousStates = new Dictionary<string, EndpointState>();
            _changeLog = new List<ChangeEvent>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Enhanced Network Monitoring Service starting...");

            // Start monitoring timers
            _monitoringTimer = new Timer(PerformLatencyMeasurements, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            _routeTraceTimer = new Timer(PerformRouteTraces, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            _changeDetectionTimer = new Timer(DetectChanges, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // Monitor active connections continuously
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorActiveConnections(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during connection monitoring");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }

        private Dictionary<string, EndpointTarget> InitializeMonitoringTargets()
        {
            return new Dictionary<string, EndpointTarget>
            {
                // Microsoft 365 Core Services
                ["teams"] = new EndpointTarget
                {
                    Name = "Microsoft Teams",
                    Endpoints = new[]
                    {
                        "teams.microsoft.com",
                        "teams.live.com",
                        "api.teams.skype.com"
                    },
                    Port = 443,
                    Protocol = "HTTPS",
                    ExpectedLatency = 50,
                    CriticalService = true
                },
                ["outlook"] = new EndpointTarget
                {
                    Name = "Outlook Online",
                    Endpoints = new[]
                    {
                        "outlook.office365.com",
                        "outlook.office.com",
                        "smtp.office365.com"
                    },
                    Port = 443,
                    Protocol = "HTTPS",
                    ExpectedLatency = 40,
                    CriticalService = true
                },
                ["onedrive"] = new EndpointTarget
                {
                    Name = "OneDrive",
                    Endpoints = new[]
                    {
                        "onedrive.live.com",
                        "skyapi.onedrive.live.com",
                        "api.onedrive.com"
                    },
                    Port = 443,
                    Protocol = "HTTPS",
                    ExpectedLatency = 60,
                    CriticalService = false
                },
                ["sharepoint"] = new EndpointTarget
                {
                    Name = "SharePoint Online",
                    Endpoints = new[]
                    {
                        "sharepoint.com",
                        "sharepointonline.com"
                    },
                    Port = 443,
                    Protocol = "HTTPS",
                    ExpectedLatency = 50,
                    CriticalService = true
                },
                ["azure"] = new EndpointTarget
                {
                    Name = "Azure Services",
                    Endpoints = new[]
                    {
                        "management.azure.com",
                        "portal.azure.com",
                        "login.microsoftonline.com"
                    },
                    Port = 443,
                    Protocol = "HTTPS",
                    ExpectedLatency = 45,
                    CriticalService = true
                }
            };
        }

        private async void PerformLatencyMeasurements(object state)
        {
            try
            {
                var tasks = _monitoringTargets.Select(async target =>
                {
                    var measurements = new List<LatencyMeasurement>();
                    
                    foreach (var endpoint in target.Value.Endpoints)
                    {
                        var measurement = await MeasureLatency(endpoint, target.Value.Port);
                        measurement.ServiceName = target.Value.Name;
                        measurement.EndpointName = endpoint;
                        measurement.Timestamp = DateTime.UtcNow;
                        
                        measurements.Add(measurement);
                    }
                    
                    // Store measurements
                    if (!_latencyHistory.ContainsKey(target.Key))
                        _latencyHistory[target.Key] = new List<LatencyMeasurement>();
                    
                    _latencyHistory[target.Key].AddRange(measurements);
                    
                    // Keep only last 1000 measurements per service
                    if (_latencyHistory[target.Key].Count > 1000)
                        _latencyHistory[target.Key] = _latencyHistory[target.Key].TakeLast(1000).ToList();
                    
                    // Send to API
                    await SendMeasurementsToAPI(target.Key, measurements);
                });
                
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing latency measurements");
            }
        }

        private async Task<LatencyMeasurement> MeasureLatency(string hostname, int port)
        {
            var measurement = new LatencyMeasurement
            {
                Hostname = hostname,
                Port = port,
                Success = false,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Perform ping test
                using var ping = new Ping();
                var pingReply = await ping.SendPingAsync(hostname, 5000);
                measurement.PingLatency = pingReply.Status == IPStatus.Success ? (double)pingReply.RoundtripTime : -1;
                
                // Perform TCP connection test
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(hostname, port);
                
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
                {
                    stopwatch.Stop();
                    measurement.TcpLatency = stopwatch.Elapsed.TotalMilliseconds;
                    measurement.Success = true;
                }
                else
                {
                    measurement.TcpLatency = -1;
                }
                
                // HTTP latency test for HTTPS endpoints
                if (port == 443)
                {
                    measurement.HttpLatency = await MeasureHttpLatency($"https://{hostname}");
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to measure latency for {Hostname}:{Port}", hostname, port);
                measurement.ErrorMessage = ex.Message;
            }

            return measurement;
        }

        private async Task<double> MeasureHttpLatency(string url)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds;
            }
            catch
            {
                return -1;
            }
        }

        private async void PerformRouteTraces(object state)
        {
            try
            {
                foreach (var target in _monitoringTargets)
                {
                    var primaryEndpoint = target.Value.Endpoints.First();
                    var routeTrace = await PerformRouteTrace(primaryEndpoint);
                    _routeTraces[target.Key] = routeTrace;
                    
                    await SendRouteTraceToAPI(target.Key, routeTrace);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing route traces");
            }
        }

        private async Task<RouteTraceResult> PerformRouteTrace(string hostname)
        {
            var result = new RouteTraceResult
            {
                Hostname = hostname,
                Timestamp = DateTime.UtcNow,
                Hops = new List<RouteHop>()
            };

            try
            {
                // Perform traceroute using ping with varying TTL
                for (int ttl = 1; ttl <= 30; ttl++)
                {
                    using var ping = new Ping();
                    var options = new PingOptions(ttl, true);
                    var reply = await ping.SendPingAsync(hostname, 5000, new byte[32], options);
                    
                    var hop = new RouteHop
                    {
                        HopNumber = ttl,
                        IPAddress = reply.Address?.ToString() ?? "Unknown",
                        Latency = reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired 
                            ? (double)reply.RoundtripTime : -1,
                        Status = reply.Status.ToString()
                    };
                    
                    result.Hops.Add(hop);
                    
                    if (reply.Status == IPStatus.Success)
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Route trace failed for {Hostname}", hostname);
            }

            return result;
        }

        private async void DetectChanges(object state)
        {
            try
            {
                foreach (var target in _monitoringTargets)
                {
                    var currentState = await GetCurrentEndpointState(target.Key, target.Value);
                    
                    if (_previousStates.ContainsKey(target.Key))
                    {
                        var changes = DetectStateChanges(_previousStates[target.Key], currentState);
                        foreach (var change in changes)
                        {
                            _changeLog.Add(change);
                            await SendChangeEventToAPI(change);
                            
                            _logger.LogInformation("Change detected for {Service}: {ChangeType} - {Details}", 
                                target.Value.Name, change.ChangeType, change.Description);
                        }
                    }
                    
                    _previousStates[target.Key] = currentState;
                }
                
                // Cleanup old change logs (keep last 1000)
                if (_changeLog.Count > 1000)
                {
                    _changeLog.RemoveRange(0, _changeLog.Count - 1000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during change detection");
            }
        }

        private async Task<EndpointState> GetCurrentEndpointState(string serviceKey, EndpointTarget target)
        {
            var state = new EndpointState
            {
                ServiceKey = serviceKey,
                ServiceName = target.Name,
                Timestamp = DateTime.UtcNow,
                Endpoints = new List<EndpointInfo>()
            };

            foreach (var endpoint in target.Endpoints)
            {
                var endpointInfo = new EndpointInfo
                {
                    Hostname = endpoint,
                    Port = target.Port,
                    IsReachable = await IsEndpointReachable(endpoint, target.Port),
                    IPAddresses = await ResolveIPAddresses(endpoint),
                    ResponseTime = await MeasureResponseTime(endpoint, target.Port)
                };
                
                state.Endpoints.Add(endpointInfo);
            }

            // Calculate average metrics
            state.AverageLatency = state.Endpoints.Where(e => e.ResponseTime > 0).Average(e => e.ResponseTime);
            state.ReachabilityPercentage = (double)state.Endpoints.Count(e => e.IsReachable) / state.Endpoints.Count * 100;

            return state;
        }

        private List<ChangeEvent> DetectStateChanges(EndpointState previous, EndpointState current)
        {
            var changes = new List<ChangeEvent>();

            // Check reachability changes
            if (Math.Abs(previous.ReachabilityPercentage - current.ReachabilityPercentage) > 5)
            {
                changes.Add(new ChangeEvent
                {
                    ServiceName = current.ServiceName,
                    ChangeType = "Reachability",
                    Timestamp = DateTime.UtcNow,
                    PreviousValue = previous.ReachabilityPercentage.ToString("F1") + "%",
                    CurrentValue = current.ReachabilityPercentage.ToString("F1") + "%",
                    Description = $"Reachability changed from {previous.ReachabilityPercentage:F1}% to {current.ReachabilityPercentage:F1}%",
                    Severity = current.ReachabilityPercentage < 90 ? "High" : "Medium"
                });
            }

            // Check latency changes (significant increase > 50%)
            if (previous.AverageLatency > 0 && current.AverageLatency > 0)
            {
                var latencyIncrease = (current.AverageLatency - previous.AverageLatency) / previous.AverageLatency * 100;
                if (latencyIncrease > 50)
                {
                    changes.Add(new ChangeEvent
                    {
                        ServiceName = current.ServiceName,
                        ChangeType = "Latency",
                        Timestamp = DateTime.UtcNow,
                        PreviousValue = previous.AverageLatency.ToString("F1") + "ms",
                        CurrentValue = current.AverageLatency.ToString("F1") + "ms",
                        Description = $"Latency increased by {latencyIncrease:F1}% from {previous.AverageLatency:F1}ms to {current.AverageLatency:F1}ms",
                        Severity = latencyIncrease > 100 ? "High" : "Medium"
                    });
                }
            }

            // Check IP address changes
            foreach (var currentEndpoint in current.Endpoints)
            {
                var previousEndpoint = previous.Endpoints.FirstOrDefault(e => e.Hostname == currentEndpoint.Hostname);
                if (previousEndpoint != null)
                {
                    var newIPs = currentEndpoint.IPAddresses.Except(previousEndpoint.IPAddresses).ToList();
                    var removedIPs = previousEndpoint.IPAddresses.Except(currentEndpoint.IPAddresses).ToList();
                    
                    if (newIPs.Any() || removedIPs.Any())
                    {
                        changes.Add(new ChangeEvent
                        {
                            ServiceName = current.ServiceName,
                            ChangeType = "DNS",
                            Timestamp = DateTime.UtcNow,
                            PreviousValue = string.Join(", ", previousEndpoint.IPAddresses),
                            CurrentValue = string.Join(", ", currentEndpoint.IPAddresses),
                            Description = $"DNS resolution changed for {currentEndpoint.Hostname}",
                            Severity = "Low"
                        });
                    }
                }
            }

            return changes;
        }

        private async Task<bool> IsEndpointReachable(string hostname, int port)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(hostname, port);
                return await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask && !connectTask.IsFaulted;
            }
            catch
            {
                return false;
            }
        }

        private async Task<List<string>> ResolveIPAddresses(string hostname)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(hostname);
                return hostEntry.AddressList.Select(ip => ip.ToString()).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task<double> MeasureResponseTime(string hostname, int port)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(hostname, port);
                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds;
            }
            catch
            {
                return -1;
            }
        }

        private async Task MonitorActiveConnections(CancellationToken cancellationToken)
        {
            try
            {
                var connections = GetActiveConnections();
                var microsoftConnections = FilterMicrosoftConnections(connections);
                
                var monitoringData = new
                {
                    timestamp = DateTime.UtcNow,
                    totalConnections = connections.Count,
                    microsoftConnections = microsoftConnections.Count,
                    connections = microsoftConnections.Take(20).Select(conn => new
                    {
                        processName = conn.ProcessName,
                        serviceName = IdentifyService(conn.RemoteAddress, conn.RemotePort),
                        remoteAddress = conn.RemoteAddress,
                        remotePort = conn.RemotePort,
                        state = conn.State,
                        localPort = conn.LocalPort,
                        timestamp = DateTime.UtcNow
                    }),
                    activeServices = GetActiveServices(microsoftConnections)
                };

                await SendMonitoringDataToAPI(monitoringData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to monitor active connections");
            }
        }

        private async Task SendMeasurementsToAPI(string serviceKey, List<LatencyMeasurement> measurements)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { serviceKey, measurements });
                await PostToAPI("/api/network/latency", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send measurements to API");
            }
        }

        private async Task SendRouteTraceToAPI(string serviceKey, RouteTraceResult routeTrace)
        {
            try
            {
                var json = JsonSerializer.Serialize(new { serviceKey, routeTrace });
                await PostToAPI("/api/network/traceroute", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send route trace to API");
            }
        }

        private async Task SendChangeEventToAPI(ChangeEvent changeEvent)
        {
            try
            {
                var json = JsonSerializer.Serialize(changeEvent);
                await PostToAPI("/api/network/changes", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send change event to API");
            }
        }

        private async Task SendMonitoringDataToAPI(object data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                await PostToAPI("/api/network/update", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send monitoring data to API");
            }
        }

        private async Task PostToAPI(string endpoint, string json)
        {
            var apiUrl = _configuration.GetValue<string>("ApiUrl", "http://localhost:5000");
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{apiUrl}{endpoint}", content);
        }

        // Helper methods for connection monitoring...
        private List<NetworkConnection> GetActiveConnections()
        {
            // Implementation similar to previous version but enhanced
            // This would use netstat or similar to get active connections
            return new List<NetworkConnection>();
        }

        private List<NetworkConnection> FilterMicrosoftConnections(List<NetworkConnection> connections)
        {
            var microsoftDomains = new[]
            {
                "microsoft.com", "microsoftonline.com", "office.com", "office365.com",
                "teams.microsoft.com", "outlook.com", "live.com", "onedrive.com",
                "sharepoint.com", "skype.com", "azure.com", "windows.com"
            };

            return connections.Where(conn => 
                microsoftDomains.Any(domain => 
                    conn.RemoteAddress.Contains(domain, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private string IdentifyService(string remoteAddress, int port)
        {
            // Enhanced service identification logic
            var address = remoteAddress.ToLower();
            
            if (address.Contains("teams")) return "Microsoft Teams";
            if (address.Contains("outlook") || address.Contains("smtp.office365.com")) return "Outlook";
            if (address.Contains("onedrive")) return "OneDrive";
            if (address.Contains("sharepoint")) return "SharePoint";
            if (address.Contains("azure") || address.Contains("management.azure.com")) return "Azure";
            if (address.Contains("login.microsoftonline.com")) return "Azure AD";
            
            return "Microsoft Service";
        }

        private object[] GetActiveServices(List<NetworkConnection> connections)
        {
            return connections
                .GroupBy(c => IdentifyService(c.RemoteAddress, c.RemotePort))
                .Select(g => new
                {
                    name = g.Key,
                    connections = g.Count(),
                    processes = g.Select(c => c.ProcessName).Distinct().ToArray(),
                    avgLatency = g.Average(c => c.Latency ?? 0)
                })
                .ToArray();
        }

        public override void Dispose()
        {
            _monitoringTimer?.Dispose();
            _routeTraceTimer?.Dispose();
            _changeDetectionTimer?.Dispose();
            _httpClient?.Dispose();
            base.Dispose();
        }
    }

    // Enhanced data models
    public class EndpointTarget
    {
        public string Name { get; set; }
        public string[] Endpoints { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }
        public double ExpectedLatency { get; set; }
        public bool CriticalService { get; set; }
    }

    public class LatencyMeasurement
    {
        public string ServiceName { get; set; }
        public string EndpointName { get; set; }
        public string Hostname { get; set; }
        public int Port { get; set; }
        public double PingLatency { get; set; }
        public double TcpLatency { get; set; }
        public double HttpLatency { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class RouteTraceResult
    {
        public string Hostname { get; set; }
        public DateTime Timestamp { get; set; }
        public List<RouteHop> Hops { get; set; }
    }

    public class RouteHop
    {
        public int HopNumber { get; set; }
        public string IPAddress { get; set; }
        public double Latency { get; set; }
        public string Status { get; set; }
    }

    public class EndpointState
    {
        public string ServiceKey { get; set; }
        public string ServiceName { get; set; }
        public DateTime Timestamp { get; set; }
        public List<EndpointInfo> Endpoints { get; set; }
        public double AverageLatency { get; set; }
        public double ReachabilityPercentage { get; set; }
    }

    public class EndpointInfo
    {
        public string Hostname { get; set; }
        public int Port { get; set; }
        public bool IsReachable { get; set; }
        public List<string> IPAddresses { get; set; }
        public double ResponseTime { get; set; }
    }

    public class ChangeEvent
    {
        public string ServiceName { get; set; }
        public string ChangeType { get; set; }
        public DateTime Timestamp { get; set; }
        public string PreviousValue { get; set; }
        public string CurrentValue { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
    }

    public class NetworkConnection
    {
        public string ProcessName { get; set; }
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public int LocalPort { get; set; }
        public string State { get; set; }
        public double? Latency { get; set; }
    }
}
