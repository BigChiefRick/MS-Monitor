using Microsoft.AspNetCore.Mvc;
using MicrosoftEndpointMonitor.Data;
using MicrosoftEndpointMonitor.Shared.Models;
using Microsoft.EntityFrameworkCore;
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
                database = _context.Database.CanConnect() ? "Connected" : "Disconnected"
            });
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                lock (_dataLock)
                {
                    return Ok(new { 
                        totalConnections = _currentData.TotalConnections,
                        microsoftConnections = _currentData.MicrosoftConnections,
                        activeServices = _currentData.ActiveServices ?? new object[0],
                        recentConnections = _currentData.RecentConnections ?? new object[0],
                        timestamp = _currentData.LastUpdated
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard data");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("connections")]
        public IActionResult GetConnections([FromQuery] int limit = 100)
        {
            lock (_dataLock)
            {
                var connections = _currentData.Connections ?? new object[0];
                return Ok(connections.Take(limit).ToArray());
            }
        }

        [HttpGet("connections/microsoft")]
        public IActionResult GetMicrosoftConnections([FromQuery] int limit = 50)
        {
            lock (_dataLock)
            {
                var connections = _currentData.Connections ?? new object[0];
                return Ok(connections.Take(limit).ToArray());
            }
        }

        [HttpGet("services")]
        public IActionResult GetActiveServices()
        {
            lock (_dataLock)
            {
                return Ok(_currentData.ActiveServices ?? new object[0]);
            }
        }

        [HttpGet("endpoints")]
        public async Task<IActionResult> GetMicrosoftEndpoints()
        {
            try
            {
                var endpoints = await _context.MicrosoftEndpoints
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.ServiceName)
                    .ToListAsync();
                
                return Ok(endpoints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Microsoft endpoints");
                return StatusCode(500, new { error = "Failed to get endpoints" });
            }
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts([FromQuery] int limit = 20)
        {
            try
            {
                var alerts = await _context.Alerts
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(limit)
                    .ToListAsync();
                
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alerts");
                return StatusCode(500, new { error = "Failed to get alerts" });
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

        [HttpPost("alerts")]
        public async Task<IActionResult> CreateAlert([FromBody] CreateAlertRequest request)
        {
            try
            {
                var alert = new Alert
                {
                    Title = request.Title,
                    Description = request.Description,
                    Severity = request.Severity,
                    Type = request.Type,
                    ConnectionId = request.ConnectionId
                };

                _context.Alerts.Add(alert);
                await _context.SaveChangesAsync();

                return Ok(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert");
                return StatusCode(500, new { error = "Failed to create alert" });
            }
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

    public class CreateAlertRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
        public AlertType Type { get; set; } = AlertType.NetworkConnection;
        public int? ConnectionId { get; set; }
    }
}
