using System.Runtime.InteropServices;
using System.Net;
using System.Diagnostics;
using MicrosoftEndpointMonitor.Shared.Models;

namespace MicrosoftEndpointMonitor.Service.Collectors;

/// <summary>
/// Enumerates TCP connections using Windows IP Helper API
/// </summary>
public class TcpConnectionEnumerator
{
    private readonly ILogger<TcpConnectionEnumerator> _logger;
    private readonly Dictionary<int, ProcessInfo> _processCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public TcpConnectionEnumerator(ILogger<TcpConnectionEnumerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all active TCP connections with process information
    /// </summary>
    public async Task<List<NetworkConnection>> GetActiveConnectionsAsync()
    {
        try
        {
            var connections = new List<NetworkConnection>();
            
            // Get IPv4 TCP connections
            var ipv4Connections = GetTcpConnections(AddressFamily.IPv4);
            connections.AddRange(ipv4Connections);
            
            // Get IPv6 TCP connections
            var ipv6Connections = GetTcpConnections(AddressFamily.IPv6);
            connections.AddRange(ipv6Connections);

            _logger.LogDebug("Enumerated {Count} TCP connections", connections.Count);
            return connections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate TCP connections");
            return new List<NetworkConnection>();
        }
    }

    private List<NetworkConnection> GetTcpConnections(AddressFamily addressFamily)
    {
        var connections = new List<NetworkConnection>();
        var tableClass = addressFamily == AddressFamily.IPv4 
            ? TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL 
            : TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL;

        var bufferSize = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 
            (int)addressFamily, tableClass, 0);

        if (result != 0 && result != ERROR_INSUFFICIENT_BUFFER)
        {
            _logger.LogWarning("Failed to get TCP table size. Error: {Error}", result);
            return connections;
        }

        var tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, 
                (int)addressFamily, tableClass, 0);

            if (result != 0)
            {
                _logger.LogWarning("Failed to get TCP table data. Error: {Error}", result);
                return connections;
            }

            if (addressFamily == AddressFamily.IPv4)
            {
                connections.AddRange(ParseTcpTable4(tcpTablePtr));
            }
            else
            {
                connections.AddRange(ParseTcpTable6(tcpTablePtr));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }

        return connections;
    }

    private List<NetworkConnection> ParseTcpTable4(IntPtr tcpTablePtr)
    {
        var connections = new List<NetworkConnection>();
        var table = Marshal.PtrToStructure<MIB_TCPTABLE_OWNER_PID>(tcpTablePtr);

        var rowPtr = IntPtr.Add(tcpTablePtr, Marshal.SizeOf<uint>());
        var rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

        for (int i = 0; i < table.dwNumEntries; i++)
        {
            var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
            var connection = CreateConnectionFromRow4(row);
            if (connection != null)
            {
                connections.Add(connection);
            }
            rowPtr = IntPtr.Add(rowPtr, rowSize);
        }

        return connections;
    }

    private List<NetworkConnection> ParseTcpTable6(IntPtr tcpTablePtr)
    {
        var connections = new List<NetworkConnection>();
        var table = Marshal.PtrToStructure<MIB_TCP6TABLE_OWNER_PID>(tcpTablePtr);

        var rowPtr = IntPtr.Add(tcpTablePtr, Marshal.SizeOf<uint>());
        var rowSize = Marshal.SizeOf<MIB_TCP6ROW_OWNER_PID>();

        for (int i = 0; i < table.dwNumEntries; i++)
        {
            var row = Marshal.PtrToStructure<MIB_TCP6ROW_OWNER_PID>(rowPtr);
            var connection = CreateConnectionFromRow6(row);
            if (connection != null)
            {
                connections.Add(connection);
            }
            rowPtr = IntPtr.Add(rowPtr, rowSize);
        }

        return connections;
    }

    private NetworkConnection? CreateConnectionFromRow4(MIB_TCPROW_OWNER_PID row)
    {
        try
        {
            var processInfo = GetProcessInfo((int)row.dwOwningPid);
            if (processInfo == null) return null;

            var localEndpoint = new IPEndPoint(row.dwLocalAddr, ConvertPort(row.dwLocalPort));
            var remoteEndpoint = new IPEndPoint(row.dwRemoteAddr, ConvertPort(row.dwRemotePort));
