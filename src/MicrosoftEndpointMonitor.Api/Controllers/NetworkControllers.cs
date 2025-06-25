using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicrosoftEndpointMonitor.Data;
using MicrosoftEndpointMonitor.Shared.Models;
using MicrosoftEndpointMonitor.Service.Services;

namespace MicrosoftEndpointMonitor.Api.Controllers;

/// <summary>
/// API Controller for network monitoring operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NetworkController : ControllerBase
{
    private readonly ILogger<NetworkController> _logger;
    private readonly NetworkContext _context;
    private readonly MicrosoftEndpointDetector _endpointDetector;

    public NetworkController(
        ILogger<NetworkController> logger,
        NetworkContext context,
        MicrosoftEndpointDetector endpointDetector)
    {
        _logger = logger;
        _context = context;
        _endpointDetector = endpointDetector;
    }

    /// <summary>
    /// Get active network connections
    /// </summary>
    [HttpGet("connections")]
    public async Task<ActionResult<ApiResponse<PagedResult<NetworkConnection>>>> GetConnections(
        [FromQuery] ConnectionFilter? filter = null)
    {
        try
        {
            filter ??= new ConnectionFilter();

            var query = _context.Connections.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filter.ProcessName))
            {
                query = query.Where(c => c.ProcessName.Contains(filter.ProcessName));
            }

            if (!string.IsNullOrEmpty(filter.MicrosoftService))
            {
                query = query.Where(c => c.MicrosoftService == filter.MicrosoftService);
            }

            if (!string.IsNullOrEmpty(filter.ServiceCategory))
            {
                query = query.Where(c => c.ServiceCategory == filter.ServiceCategory);
            }

            if (!string.IsNullOrEmpty(filter.ConnectionState))
            {
                query = query.Where(c => c.ConnectionState == filter.ConnectionState);
            }

            if (filter.StartTime.HasValue)
            {
                query = query.Where(c => c.EstablishedTime >= filter.StartTime.Value);
            }

            if (filter.EndTime.HasValue)
            {
                query = query.Where(c => c.EstablishedTime <= filter.EndTime.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            }

            if (filter.MinBytes.HasValue)
            {
                query = query.Where(c => (c.BytesSent + c.BytesReceived) >= filter.MinBytes.Value);
            }

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply sorting
            query = filter.SortBy.ToLowerInvariant() switch
            {
                "processname" => filter.SortDescending 
                    ? query.OrderByDescending(c => c.ProcessName)
                    : query.OrderBy(c => c.ProcessName),
                "microsoftservice" => filter.SortDescending
                    ? query.OrderByDescending(c => c.MicrosoftService)
                    : query.OrderBy(c => c.MicrosoftService),
                "totalbytes" => filter.SortDescending
                    ? query.OrderByDescending(c => c.BytesSent + c.BytesReceived)
                    : query.OrderBy(c => c.BytesSent + c.BytesReceived),
                "connectionstate" => filter.SortDescending
                    ? query.OrderByDescending(c => c.ConnectionState)
                    : query.OrderBy(c => c.ConnectionState),
                _ => filter.SortDescending
                    ? query.OrderByDescending(c => c.EstablishedTime)
                    : query.OrderBy(c => c.EstablishedTime)
            };

            // Apply pagination
            var connections = await query
                .Skip(filter.Skip)
                .Take(filter.Take)
                .ToListAsync();

            var result = new PagedResult<NetworkConnection>
            {
                Items = connections,
                TotalCount = totalCount,
                PageNumber = (filter.Skip / filter.Take) + 1,
                PageSize = filter.Take
            };

            return Ok(new ApiResponse<PagedResult<NetworkConnection>>
            {
                Success = true,
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connections");
            return StatusCode(500, new ApiResponse<PagedResult<NetworkConnection>>
            {
                Success = false,
                Message = "Failed to retrieve connections",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Get connection by ID
    /// </summary>
    [HttpGet("connections/{id}")]
    public async Task<ActionResult<ApiResponse<NetworkConnection>>> GetConnection(int id)
    {
        try
        {
            var connection = await _context.Connections
                .FirstOrDefaultAsync(c => c.Id == id);

            if (connection == null)
            {
                return NotFound(new ApiResponse<NetworkConnection>
                {
                    Success = false,
                    Message = "Connection not found",
                    ErrorCode = "NOT_FOUND"
                });
            }

            return Ok(new ApiResponse<NetworkConnection>
            {
                Success = true,
                Data = connection
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection {Id}", id);
            return StatusCode(500, new ApiResponse<NetworkConnection>
            {
                Success = false,
                Message = "Failed to retrieve connection",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// Get connection metrics for a specific connection
    /// </summary>
    [HttpGet("connections/{id}/metrics")]
    public async Task<ActionResult<ApiResponse<List<ConnectionMetric>>>> GetConnectionMetrics(
