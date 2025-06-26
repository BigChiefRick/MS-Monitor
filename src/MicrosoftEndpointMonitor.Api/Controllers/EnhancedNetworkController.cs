using Microsoft.AspNetCore.Mvc;
using MicrosoftEndpointMonitor.Data;
using MicrosoftEndpointMonitor.Shared.Models;
using System.Text.Json;

namespace MicrosoftEndpointMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NetworkController : ControllerBase
    {
        private readonly NetworkContext _context;
        private readonly ILogger<NetworkController> _logger;
        private static readonly object _dataLock = new object();
        private static NetworkData _currentData = new NetworkData();
        
        // Enhanced data storage for ThousandEyes-like features
        private static readonly Dictionary<string, List<LatencyMeasurement>> _latencyHistory = new();
        private static readonly Dictionary<string, RouteTraceResult> _routeTraces = new();
        private static readonly List<ChangeEvent> _changeEvents = new();

        public NetworkController(NetworkContext context, ILogger<NetworkController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { 
                status = "Healthy", 
                timestamp = DateTime.UtcNow,
                version = "2.0.0",
                features = new[] { "latency-tracking", "change-detection", "route-tracing" }
            });
        }

        [HttpGet("dashboard")]
        public IActionResult GetDashboard()
        {
            lock (_dataLock)
            {
                return Ok(new { 
                    totalConnections = _currentData.TotalConnections,
                    microsoftConnections = _currentData.MicrosoftConnections,
                    activeServices = _currentData.ActiveServices ?? new object[0],
                    recentConnections = _currentData.RecentConnections ?? new object[0],
                    timestamp = _currentData.LastUpdated,
                    // Enhanced metrics
                    averageLatency = CalculateAverageLatency(),
                    reachabilityScore = CalculateReachabilityScore(),
                    changeEventsCount = _changeEvents.Count(e => e.Timestamp > DateTime.UtcNow.AddDays(-1))
                });
            }
        }

        [HttpGet("connections")]
        public IActionResult GetConnections()
        {
            lock (_dataLock)
            {
                return Ok(_currentData.Connections ?? new object[0]);
            }
        }

        [HttpGet("connections/microsoft")]
        public IActionResult GetMicrosoftConnections()
        {
            lock (_dataLock)
            {
                // Filter and enhance Microsoft connections
                var microsoftConnections = (_currentData.Connections ?? new object[0])
                    .Take(20)
                    .Select(conn => EnhanceConnectionData(conn))
                    .ToArray();
                
                return Ok(microsoftConnections);
            }
        }

        [HttpGet("services")]
        public IActionResult GetServices()
        {
            lock (_dataLock)
            {
                var services = (_currentData.ActiveServices ?? new object[0])
                    .Select(service => EnhanceServiceData(service))
                    .ToArray();
                
                return Ok(services);
            }
        }

        [HttpGet("latency/{serviceKey}")]
        public IActionResult GetLatencyHistory(string serviceKey)
        {
            lock (_dataLock)
            {
                if (_latencyHistory.ContainsKey(serviceKey))
                {
                    var recent = _latencyHistory[serviceKey]
                        .Where(l => l.Timestamp > DateTime.UtcNow.AddHours(-1))
                        .OrderBy(l => l.Timestamp)
                        .ToArray();
                    
                    return Ok(recent);
                }
                return Ok(new object[0]);
            }
        }

        [HttpGet("traceroute/{serviceKey}")]
        public IActionResult GetRouteTrace(string serviceKey)
        {
            lock (_dataLock)
            {
                if (_routeTraces.ContainsKey(serviceKey))
                {
                    return Ok(_routeTraces[serviceKey]);
                }
                return NotFound();
            }
        }

        [HttpGet("changes")]
        public IActionResult GetChangeEvents()
        {
            lock (_dataLock)
            {
                var recentChanges = _changeEvents
                    .Where(c => c.Timestamp > DateTime.UtcNow.AddDays(-7))
                    .OrderByDescending(c => c.Timestamp)
                    .Take(50)
                    .ToArray();
                
                return Ok(recentChanges);
            }
        }

        [HttpPost("latency")]
        public IActionResult UpdateLatency([FromBody] JsonElement data)
        {
            try
            {
                lock (_dataLock)
                {
                    if (data.TryGetProperty("serviceKey", out var serviceKeyElement) &&
                        data.TryGetProperty("measurements", out var measurementsElement))
                    {
                        var serviceKey = serviceKeyElement.GetString();
                        var measurements = JsonSerializer.Deserialize<LatencyMeasurement[]>(measurementsElement.GetRawText());
                        
                        if (!_latencyHistory.ContainsKey(serviceKey))
                            _latencyHistory[serviceKey] = new List<LatencyMeasurement>();
                        
                        _latencyHistory[serviceKey].AddRange(measurements);
                        
                        // Keep only last 1000 measurements
                        if (_latencyHistory[serviceKey].Count > 1000)
                            _latencyHistory[serviceKey] = _latencyHistory[serviceKey].TakeLast(1000).ToList();
                    }
                }

                return Ok(new { status = "Updated", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating latency data");
                return BadRequest(new { error = "Failed to update latency data" });
            }
        }

        [HttpPost("traceroute")]
        public IActionResult UpdateRouteTrace([FromBody] JsonElement data)
        {
            try
            {
                lock (_dataLock)
                {
                    if (data.TryGetProperty("serviceKey", out var serviceKeyElement) &&
                        data.TryGetProperty("routeTrace", out var routeTraceElement))
                    {
                        var serviceKey = serviceKeyElement.GetString();
                        var routeTrace = JsonSerializer.Deserialize<RouteTraceResult>(routeTraceElement.GetRawText());
                        
                        _routeTraces[serviceKey] = routeTrace;
                    }
                }

                return Ok(new { status = "Updated", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating route trace data");
                return BadRequest(new { error = "Failed to update route trace data" });
            }
        }

        [HttpPost("changes")]
        public IActionResult UpdateChangeEvent([FromBody] JsonElement data)
        {
            try
            {
                lock (_dataLock)
                {
                    var changeEvent = JsonSerializer.Deserialize<ChangeEvent>(data.GetRawText());
                    _changeEvents.Add(changeEvent);
                    
                    // Keep only last 1000 change events
                    if (_changeEvents.Count > 1000)
                    {
                        _changeEvents.RemoveRange(0, _changeEvents.Count - 1000);
                    }
                }

                return Ok(new { status = "Updated", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating change event");
                return BadRequest(new { error = "Failed to update change event" });
            }
        }

        [HttpPost("update")]
        public IActionResult UpdateData([FromBody] JsonElement data)
        {
            try
            {
                lock (_dataLock)
                {
                    if (data.TryGetProperty("totalConnections", out var totalElement))
                        _currentData.TotalConnections = totalElement.GetInt32();
                    
                    if (data.TryGetProperty("microsoftConnections", out var microsoftElement))
                        _currentData.MicrosoftConnections = microsoftElement.GetInt32();
                    
                    if (data.TryGetProperty("connections", out var connectionsElement))
                    {
                        _currentData.Connections = JsonSerializer.Deserialize<object[]>(connectionsElement.GetRawText());
                        _currentData.RecentConnections = _currentData.Connections?.Take(10).ToArray();
                    }
                    
                    if (data.TryGetProperty("activeServices", out var servicesElement))
                        _currentData.ActiveServices = JsonSerializer.Deserialize<object[]>(servicesElement.GetRawText());
                    
                    _currentData.LastUpdated = DateTime.UtcNow;
                }

                _logger.LogInformation("Updated network data: {Total} total, {Microsoft} Microsoft connections", 
                    _currentData.TotalConnections, _currentData.MicrosoftConnections);

                return Ok(new { status = "Updated", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating network data");
                return BadRequest(new { error = "Failed to update data" });
            }
        }

        private double CalculateAverageLatency()
        {
            if (!_latencyHistory.Any()) return 0;
            
            var recentMeasurements = _latencyHistory.Values
                .SelectMany(measurements => measurements
                    .Where(m => m.Timestamp > DateTime.UtcNow.AddMinutes(-5) && m.Success)
                    .Select(m => m.TcpLatency > 0 ? m.TcpLatency : m.PingLatency))
                .Where(latency => latency > 0)
                .ToList();
            
            return recentMeasurements.Any() ? recentMeasurements.Average() : 0;
        }

        private double CalculateReachabilityScore()
        {
            if (!_latencyHistory.Any()) return 100;
            
            var recentMeasurements = _latencyHistory.Values
                .SelectMany(measurements => measurements
                    .Where(m => m.Timestamp > DateTime.UtcNow.AddMinutes(-5)))
                .ToList();
            
            if (!recentMeasurements.Any()) return 100;
            
            var successfulMeasurements = recentMeasurements.Count(m => m.Success);
            return (double)successfulMeasurements / recentMeasurements.Count * 100;
        }

        private object EnhanceConnectionData(object connection)
        {
            // Add additional metrics or formatting as needed
            return connection;
        }

        private object EnhanceServiceData(object service)
        {
            // Add additional metrics or formatting as needed
            return service;
        }
    }

    public class NetworkData
    {
        public int TotalConnections { get; set; }
        public int MicrosoftConnections { get; set; }
        public object[]? Connections { get; set; }
        public object[]? ActiveServices { get; set; }
        public object[]? RecentConnections { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
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
}
