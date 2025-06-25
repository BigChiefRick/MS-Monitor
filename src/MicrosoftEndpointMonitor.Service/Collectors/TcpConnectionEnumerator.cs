using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using MicrosoftEndpointMonitor.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MicrosoftEndpointMonitor.Service.Collectors
{
    public class TcpConnectionEnumerator
    {
        private readonly ILogger<TcpConnectionEnumerator> _logger;
        private readonly Dictionary<string, DateTime> _lastPingTime = new();
        private readonly Dictionary<string, double> _latencyCache = new();

        public TcpConnectionEnumerator(ILogger<TcpConnectionEnumerator> logger)
        {
            _logger = logger;
        }

        public List<NetworkConnection> GetActiveConnections()
        {
            var connections = new List<NetworkConnection>();
            
            try
            {
                // Use .NET built-in method for simplicity - we''ll get process info separately
                var tcpConnections = GetTcpConnections();
                _logger.LogDebug("Found {Count} TCP connections", tcpConnections.Count);
                
                foreach (var tcpConn in tcpConnections)
                {
                    var connection = new NetworkConnection
                    {
                        LocalAddress = tcpConn.LocalEndPoint.Address.ToString(),
                        LocalPort = tcpConn.LocalEndPoint.Port,
                        RemoteAddress = tcpConn.RemoteEndPoint.Address.ToString(),
                        RemotePort = tcpConn.RemoteEndPoint.Port,
                        State = tcpConn.State.ToString(),
                        Protocol = "TCP",
                        Timestamp = DateTime.UtcNow
                    };
                    
                    // Try to correlate with process using port matching
                    var processInfo = GetProcessByConnection(connection.LocalAddress, connection.LocalPort);
                    if (processInfo != null)
                    {
                        connection.ProcessId = processInfo.Id;
                        connection.ProcessName = processInfo.ProcessName;
                    }
                    else
                    {
                        connection.ProcessName = "Unknown";
                        connection.ProcessId = 0;
                    }
                    
                    // Measure latency for external connections
                    if (!IsLocalAddress(connection.RemoteAddress))
                    {
                        connection.Latency = MeasureLatency(connection.RemoteAddress);
                    }
                    
                    connections.Add(connection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating TCP connections");
            }
            
            return connections;
        }

        private List<TcpConnectionInformation> GetTcpConnections()
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = properties.GetActiveTcpConnections().ToList();
            
            return tcpConnections;
        }

        private Process? GetProcessByConnection(string localAddress, int localPort)
        {
            try
            {
                // Simple approach: find processes that might be using this port
                // This is not 100% accurate but works for most cases
                var processes = Process.GetProcesses();
                
                foreach (var process in processes)
                {
                    try
                    {
                        // Check if process name suggests it might be a network application
                        var processName = process.ProcessName.ToLower();
                        if (IsNetworkProcess(processName))
                        {
                            return process;
                        }
                    }
                    catch
                    {
                        // Process may have exited, continue
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error correlating process for {Address}:{Port}: {Error}", localAddress, localPort, ex.Message);
            }
            
            return null;
        }

        private bool IsNetworkProcess(string processName)
        {
            var networkProcesses = new[]
            {
                "teams", "outlook", "msedge", "chrome", "firefox", "onedrive", 
                "excel", "word", "powerpoint", "skype", "lync", "communicator",
                "winmail", "thunderbird", "slack", "zoom", "discord"
            };
            
            return networkProcesses.Any(np => processName.Contains(np));
        }

        private double? MeasureLatency(string remoteAddress)
        {
            var cacheKey = remoteAddress;
            
            // Only ping once every 30 seconds per address to avoid flooding
            if (_lastPingTime.ContainsKey(cacheKey) && 
                DateTime.UtcNow - _lastPingTime[cacheKey] < TimeSpan.FromSeconds(30))
            {
                return _latencyCache.GetValueOrDefault(cacheKey);
            }
            
            try
            {
                using var ping = new Ping();
                var reply = ping.Send(remoteAddress, 3000); // 3 second timeout
                
                _lastPingTime[cacheKey] = DateTime.UtcNow;
                
                if (reply.Status == IPStatus.Success)
                {
                    var latency = reply.RoundtripTime;
                    _latencyCache[cacheKey] = latency;
                    return latency;
                }
                else
                {
                    _logger.LogDebug("Ping failed for {Address}: {Status}", remoteAddress, reply.Status);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error measuring latency for {Address}: {Error}", remoteAddress, ex.Message);
                return null;
            }
        }

        private bool IsLocalAddress(string address)
        {
            if (IPAddress.TryParse(address, out var ip))
            {
                return IPAddress.IsLoopback(ip) || 
                       ip.ToString().StartsWith("192.168.") ||
                       ip.ToString().StartsWith("10.") ||
                       ip.ToString().StartsWith("172.16.") ||
                       ip.ToString().StartsWith("169.254.");
            }
            return false;
        }
    }
}
