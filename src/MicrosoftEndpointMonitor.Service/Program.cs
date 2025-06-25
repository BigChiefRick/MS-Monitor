using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MicrosoftEndpointMonitor.Service.Collectors;
using MicrosoftEndpointMonitor.Service.Services;
using MicrosoftEndpointMonitor.Shared.Models;
using System.Text.Json;
using System.Text;

namespace MicrosoftEndpointMonitor.Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<TcpConnectionEnumerator>();
                    services.AddSingleton<MicrosoftEndpointDetector>();
                    services.AddHostedService<NetworkMonitorService>();
                    services.AddHttpClient();
                })
                .Build();

            await host.RunAsync();
        }
    }

    public class NetworkMonitorService : BackgroundService
    {
        private readonly ILogger<NetworkMonitorService> _logger;
        private readonly TcpConnectionEnumerator _connectionEnumerator;
        private readonly MicrosoftEndpointDetector _endpointDetector;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl = "http://localhost:5000";

        public NetworkMonitorService(
            ILogger<NetworkMonitorService> logger,
            TcpConnectionEnumerator connectionEnumerator,
            MicrosoftEndpointDetector endpointDetector,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _connectionEnumerator = connectionEnumerator;
            _endpointDetector = endpointDetector;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MS-Monitor Service started - Real network monitoring with latency tracking active");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Get real TCP connections with latency measurements
                    var connections = _connectionEnumerator.GetActiveConnections();

                    // Detect Microsoft endpoints
                    var microsoftConnections = new List<NetworkConnection>();
                    foreach (var connection in connections)
                    {
                        if (_endpointDetector.IsMicrosoftEndpoint(connection.RemoteAddress, out string serviceName))
                        {
                            connection.IsMicrosoftEndpoint = true;
                            connection.ServiceName = _endpointDetector.ClassifyMicrosoftService(connection.ProcessName, serviceName);
                            microsoftConnections.Add(connection);
                        }
                    }

                    _logger.LogInformation("Monitoring Cycle: {TotalConnections} total, {MicrosoftConnections} Microsoft endpoints detected",
                        connections.Count, microsoftConnections.Count);

                    // Log detailed Microsoft connections with latency
                    foreach (var msConn in microsoftConnections.Where(c => c.Latency.HasValue))
                    {
                        _logger.LogInformation("MS Endpoint: {Service} ({Process}) -> {RemoteAddress}:{RemotePort} | Latency: {Latency}ms | State: {State}",
                            msConn.ServiceName, msConn.ProcessName, msConn.RemoteAddress, msConn.RemotePort, msConn.Latency, msConn.State);
                    }

                    // Send data to API
                    await SendDataToApi(connections, microsoftConnections);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during network monitoring cycle");
                }

                await Task.Delay(5000, stoppingToken); // Monitor every 5 seconds
            }
        }

        private async Task SendDataToApi(List<NetworkConnection> allConnections, List<NetworkConnection> microsoftConnections)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();

                var data = new
                {
                    totalConnections = allConnections.Count,
                    microsoftConnections = microsoftConnections.Count,
                    connections = microsoftConnections.Select(c => new
                    {
                        localAddress = c.LocalAddress,
                        localPort = c.LocalPort,
                        remoteAddress = c.RemoteAddress,
                        remotePort = c.RemotePort,
                        processName = c.ProcessName,
                        serviceName = c.ServiceName,
                        latency = c.Latency,
                        state = c.State,
                        timestamp = c.Timestamp
                    }).ToArray(),
                    activeServices = microsoftConnections
                        .GroupBy(c => c.ServiceName)
                        .Select(g => new
                        {
                            name = g.Key,
                            connections = g.Count(),
                            avgLatency = g.Where(c => c.Latency.HasValue)
                                         .Select(c => c.Latency!.Value)  // Fixed nullable warning
                                         .DefaultIfEmpty(0)
                                         .Average(),
                            processes = g.Select(c => c.ProcessName).Distinct().ToArray()
                        }).ToArray(),
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{_apiBaseUrl}/api/network/update", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully sent monitoring data to API");
                }
                else
                {
                    _logger.LogWarning("Failed to send data to API: {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException)
            {
                _logger.LogDebug("API not available - running in standalone monitoring mode");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending data to API");
            }
        }
    }
}
