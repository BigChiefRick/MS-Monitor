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

            return new NetworkConnection
            {
                Pid = (int)row.dwOwningPid,
                ProcessName = processInfo.Name,
                ProcessPath = processInfo.ExecutablePath,
                ProcessCommandLine = processInfo.CommandLine,
                LocalIp = localEndpoint.Address.ToString(),
                LocalPort = localEndpoint.Port,
                RemoteIp = remoteEndpoint.Address.ToString(),
                RemotePort = remoteEndpoint.Port,
                ConnectionState = GetConnectionState((TcpConnectionState)row.dwState),
                Protocol = "TCP",
                EstablishedTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create connection from IPv4 row for PID {Pid}", row.dwOwningPid);
            return null;
        }
    }

    private NetworkConnection? CreateConnectionFromRow6(MIB_TCP6ROW_OWNER_PID row)
    {
        try
        {
            var processInfo = GetProcessInfo((int)row.dwOwningPid);
            if (processInfo == null) return null;

            var localAddr = new IPAddress(row.ucLocalAddr);
            var remoteAddr = new IPAddress(row.ucRemoteAddr);
            var localEndpoint = new IPEndPoint(localAddr, ConvertPort(row.dwLocalPort));
            var remoteEndpoint = new IPEndPoint(remoteAddr, ConvertPort(row.dwRemotePort));

            return new NetworkConnection
            {
                Pid = (int)row.dwOwningPid,
                ProcessName = processInfo.Name,
                ProcessPath = processInfo.ExecutablePath,
                ProcessCommandLine = processInfo.CommandLine,
                LocalIp = localEndpoint.Address.ToString(),
                LocalPort = localEndpoint.Port,
                RemoteIp = remoteEndpoint.Address.ToString(),
                RemotePort = remoteEndpoint.Port,
                ConnectionState = GetConnectionState((TcpConnectionState)row.dwState),
                Protocol = "TCP",
                EstablishedTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create connection from IPv6 row for PID {Pid}", row.dwOwningPid);
            return null;
        }
    }

    private ProcessInfo? GetProcessInfo(int pid)
    {
        if (pid == 0 || pid == 4) return null; // System processes

        // Check cache first
        if (_processCache.TryGetValue(pid, out var cachedInfo) && 
            DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry)
        {
            return cachedInfo;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            var processInfo = new ProcessInfo
            {
                Pid = pid,
                Name = process.ProcessName,
                ExecutablePath = GetProcessExecutablePath(process),
                CommandLine = GetProcessCommandLine(process),
                StartTime = process.StartTime,
                UserName = GetProcessUser(process),
                IsMicrosoftApp = IsMicrosoftProcess(process),
                AppVersion = GetProcessVersion(process),
                AppDescription = GetProcessDescription(process),
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _processCache[pid] = processInfo;
            return processInfo;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get process info for PID {Pid}", pid);
            return null;
        }
    }

    private string? GetProcessExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private string? GetProcessCommandLine(Process process)
    {
        try
        {
            // This would require WMI or other methods to get command line
            // For now, return null - can be enhanced later
            return null;
        }
        catch
        {
            return null;
        }
    }

    private string? GetProcessUser(Process process)
    {
        try
        {
            // This would require additional Windows API calls
            // For now, return null - can be enhanced later
            return null;
        }
        catch
        {
            return null;
        }
    }

    private bool IsMicrosoftProcess(Process process)
    {
        try
        {
            var fileName = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(fileName)) return false;

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);
            return fileVersionInfo.CompanyName?.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    private string? GetProcessVersion(Process process)
    {
        try
        {
            var fileName = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(fileName)) return null;

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);
            return fileVersionInfo.FileVersion;
        }
        catch
        {
            return null;
        }
    }

    private string? GetProcessDescription(Process process)
    {
        try
        {
            var fileName = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(fileName)) return null;

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);
            return fileVersionInfo.FileDescription;
        }
        catch
        {
            return null;
        }
    }

    private static int ConvertPort(uint port)
    {
        return IPAddress.NetworkToHostOrder((short)port);
    }

    private static string GetConnectionState(TcpConnectionState state)
    {
        return state switch
        {
            TcpConnectionState.Closed => "CLOSED",
            TcpConnectionState.Listen => "LISTENING",
            TcpConnectionState.SynSent => "SYN_SENT",
            TcpConnectionState.SynRcvd => "SYN_RECEIVED",
            TcpConnectionState.Established => "ESTABLISHED",
            TcpConnectionState.FinWait1 => "FIN_WAIT_1",
            TcpConnectionState.FinWait2 => "FIN_WAIT_2",
            TcpConnectionState.CloseWait => "CLOSE_WAIT",
            TcpConnectionState.Closing => "CLOSING",
            TcpConnectionState.LastAck => "LAST_ACK",
            TcpConnectionState.TimeWait => "TIME_WAIT",
            TcpConnectionState.DeleteTcb => "DELETE_TCB",
            _ => "UNKNOWN"
        };
    }

    public void ClearProcessCache()
    {
        _processCache.Clear();
        _lastCacheUpdate = DateTime.MinValue;
    }

    #region Windows API Declarations

    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    private enum AddressFamily
    {
        IPv4 = 2,
        IPv6 = 23
    }

    private enum TCP_TABLE_CLASS
    {
        TCP_TABLE_BASIC_LISTENER,
        TCP_TABLE_BASIC_CONNECTIONS,
        TCP_TABLE_BASIC_ALL,
        TCP_TABLE_OWNER_PID_LISTENER,
        TCP_TABLE_OWNER_PID_CONNECTIONS,
        TCP_TABLE_OWNER_PID_ALL,
        TCP_TABLE_OWNER_MODULE_LISTENER,
        TCP_TABLE_OWNER_MODULE_CONNECTIONS,
        TCP_TABLE_OWNER_MODULE_ALL
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPTABLE_OWNER_PID
    {
        public uint dwNumEntries;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6TABLE_OWNER_PID
    {
        public uint dwNumEntries;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucLocalAddr;
        public uint dwLocalScopeId;
        public uint dwLocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ucRemoteAddr;
        public uint dwRemoteScopeId;
        public uint dwRemotePort;
        public uint dwState;
        public uint dwOwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        TCP_TABLE_CLASS tblClass,
        uint reserved);

    #endregion
}
