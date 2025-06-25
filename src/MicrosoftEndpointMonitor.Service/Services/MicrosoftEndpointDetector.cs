using System.Net;
using Microsoft.Extensions.Logging;

namespace MicrosoftEndpointMonitor.Service.Services
{
    public class MicrosoftEndpointDetector
    {
        private readonly ILogger<MicrosoftEndpointDetector> _logger;
        private readonly HashSet<string> _microsoftDomains;
        private readonly Dictionary<string, string> _servicePatterns;
        private readonly Dictionary<string, (IPAddress start, IPAddress end)> _microsoftIpRanges;

        public MicrosoftEndpointDetector(ILogger<MicrosoftEndpointDetector> logger)
        {
            _logger = logger;
            _microsoftDomains = InitializeMicrosoftDomains();
            _servicePatterns = InitializeServicePatterns();
            _microsoftIpRanges = InitializeMicrosoftIpRanges();
        }

        public bool IsMicrosoftEndpoint(string ipAddress, out string serviceName)
        {
            serviceName = "Unknown Microsoft Service";
            
            if (!IPAddress.TryParse(ipAddress, out var ip))
                return false;
            
            // Check against known Microsoft IP ranges
            foreach (var range in _microsoftIpRanges)
            {
                if (IsIpInRange(ip, range.Value.start, range.Value.end))
                {
                    serviceName = range.Key;
                    return true;
                }
            }
            
            // Try reverse DNS lookup for domain-based detection
            try
            {
                var hostEntry = Dns.GetHostEntry(ip);
                var hostname = hostEntry.HostName.ToLower();
                
                foreach (var domain in _microsoftDomains)
                {
                    if (hostname.Contains(domain))
                    {
                        serviceName = ClassifyByDomain(hostname);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("DNS lookup failed for {IP}: {Error}", ipAddress, ex.Message);
            }
            
            return false;
        }

        public string ClassifyMicrosoftService(string processName, string detectedService)
        {
            var process = processName.ToLower();
            
            // Process-based classification (most specific)
            if (process.Contains("teams")) return "Microsoft Teams";
            if (process.Contains("outlook")) return "Microsoft Outlook";
            if (process.Contains("onedrive")) return "Microsoft OneDrive";
            if (process.Contains("excel") || process.Contains("word") || process.Contains("powerpoint")) 
                return "Microsoft Office";
            if (process.Contains("edge") || process.Contains("msedge")) return "Microsoft Edge";
            if (process.Contains("skype")) return "Skype for Business";
            
            // Use detected service name if process classification fails
            return detectedService;
        }

        private HashSet<string> InitializeMicrosoftDomains()
        {
            return new HashSet<string>
            {
                "microsoft.com", "microsoftonline.com", "office.com", "office365.com",
                "outlook.com", "hotmail.com", "live.com", "msn.com",
                "teams.microsoft.com", "graph.microsoft.com", "login.microsoftonline.com",
                "azure.com", "azurewebsites.net", "blob.core.windows.net",
                "onedrive.com", "sharepoint.com", "dynamics.com",
                "xbox.com", "skype.com", "bing.com"
            };
        }

        private Dictionary<string, string> InitializeServicePatterns()
        {
            return new Dictionary<string, string>
            {
                { "teams", "Microsoft Teams" },
                { "outlook", "Microsoft Outlook" },
                { "onedrive", "Microsoft OneDrive" },
                { "sharepoint", "Microsoft SharePoint" },
                { "office", "Microsoft Office 365" },
                { "graph", "Microsoft Graph API" },
                { "login", "Microsoft Authentication" },
                { "azure", "Microsoft Azure" },
                { "skype", "Skype for Business" },
                { "exchange", "Microsoft Exchange" }
            };
        }

        private Dictionary<string, (IPAddress start, IPAddress end)> InitializeMicrosoftIpRanges()
        {
            var ranges = new Dictionary<string, (IPAddress, IPAddress)>();
            
            // Major Microsoft IP ranges (simplified for demonstration)
            ranges["Microsoft Teams"] = (IPAddress.Parse("52.108.0.0"), IPAddress.Parse("52.108.255.255"));
            ranges["Microsoft Office 365"] = (IPAddress.Parse("52.96.0.0"), IPAddress.Parse("52.127.255.255"));
            ranges["Microsoft Azure"] = (IPAddress.Parse("20.0.0.0"), IPAddress.Parse("20.255.255.255"));
            ranges["Microsoft OneDrive"] = (IPAddress.Parse("52.121.0.0"), IPAddress.Parse("52.121.255.255"));
            ranges["Microsoft Exchange Online"] = (IPAddress.Parse("40.92.0.0"), IPAddress.Parse("40.107.255.255"));
            
            return ranges;
        }

        private string ClassifyByDomain(string hostname)
        {
            foreach (var pattern in _servicePatterns)
            {
                if (hostname.Contains(pattern.Key))
                {
                    return pattern.Value;
                }
            }
            return "Microsoft Service";
        }

        private bool IsIpInRange(IPAddress ip, IPAddress start, IPAddress end)
        {
            var ipBytes = ip.GetAddressBytes();
            var startBytes = start.GetAddressBytes();
            var endBytes = end.GetAddressBytes();
            
            for (int i = 0; i < ipBytes.Length; i++)
            {
                if (ipBytes[i] < startBytes[i] || ipBytes[i] > endBytes[i])
                    return false;
            }
            
            return true;
        }
    }
}
