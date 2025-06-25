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
            
            // Enhanced process-based classification with higher priority
            if (process.Contains("teams") || process.Contains("msteams")) 
                return "Microsoft Teams";
            if (process.Contains("outlook") || process.Contains("msoutlook")) 
                return "Microsoft Outlook";
            if (process.Contains("onedrive") || process.Contains("microsoftonedrive")) 
                return "Microsoft OneDrive";
            if (process.Contains("sharepoint") || process.Contains("microsoftsharepoint")) 
                return "Microsoft SharePoint";
            if (process.Contains("excel")) return "Microsoft Excel";
            if (process.Contains("winword") || process.Contains("word")) return "Microsoft Word";
            if (process.Contains("powerpnt") || process.Contains("powerpoint")) return "Microsoft PowerPoint";
            if (process.Contains("msaccess") || process.Contains("access")) return "Microsoft Access";
            if (process.Contains("onenote")) return "Microsoft OneNote";
            if (process.Contains("skype") || process.Contains("lync")) return "Skype for Business";
            if (process.Contains("groove")) return "Microsoft Groove";
            if (process.Contains("officeclicktorun")) return "Microsoft Office (Update)";
            if (process.Contains("officefilesync")) return "Microsoft Office (Sync)";
            
            // Browser processes get lower priority
            if (process.Contains("msedge") && !process.Contains("webview")) return "Microsoft Edge";
            if (process.Contains("msedgewebview2")) return "Microsoft Edge WebView";
            
            // Generic Office processes
            if (process.Contains("office") || process.Contains("mso")) return "Microsoft Office";
            
            // Use detected service name if process classification fails
            return detectedService;
        }

        private HashSet<string> InitializeMicrosoftDomains()
        {
            return new HashSet<string>
            {
                // Core Microsoft domains
                "microsoft.com", "microsoftonline.com", "office.com", "office365.com",
                "outlook.com", "hotmail.com", "live.com", "msn.com",
                
                // Teams specific
                "teams.microsoft.com", "teams.live.com", "teams.office.com",
                
                // Office 365 & SharePoint
                "sharepoint.com", "officeapps.live.com", "outlook.office.com",
                "login.microsoftonline.com", "graph.microsoft.com",
                
                // OneDrive
                "onedrive.com", "onedrive.live.com", "files.1drv.com",
                
                // Azure & cloud services
                "azure.com", "azurewebsites.net", "blob.core.windows.net",
                "servicebus.windows.net", "database.windows.net",
                
                // Other Microsoft services
                "xbox.com", "skype.com", "bing.com", "msedge.net",
                "visualstudio.com", "github.com"
            };
        }

        private Dictionary<string, string> InitializeServicePatterns()
        {
            return new Dictionary<string, string>
            {
                // Teams patterns
                { "teams", "Microsoft Teams" },
                { "teams.microsoft", "Microsoft Teams" },
                { "teams.live", "Microsoft Teams" },
                
                // Outlook & Exchange
                { "outlook", "Microsoft Outlook" },
                { "exchange", "Microsoft Exchange" },
                { "mail", "Microsoft Outlook" },
                
                // OneDrive & SharePoint
                { "onedrive", "Microsoft OneDrive" },
                { "sharepoint", "Microsoft SharePoint" },
                { "files.1drv", "Microsoft OneDrive" },
                
                // Office apps
                { "office", "Microsoft Office 365" },
                { "officeapps", "Microsoft Office" },
                { "excel", "Microsoft Excel" },
                { "word", "Microsoft Word" },
                { "powerpoint", "Microsoft PowerPoint" },
                
                // Authentication & Graph
                { "login", "Microsoft Authentication" },
                { "graph", "Microsoft Graph API" },
                
                // Azure services
                { "azure", "Microsoft Azure" },
                { "servicebus", "Azure Service Bus" },
                { "blob.core", "Azure Blob Storage" },
                
                // Communication
                { "skype", "Skype for Business" },
                { "lync", "Skype for Business" }
            };
        }

        private Dictionary<string, (IPAddress start, IPAddress end)> InitializeMicrosoftIpRanges()
        {
            var ranges = new Dictionary<string, (IPAddress, IPAddress)>();
            
            // Comprehensive Microsoft IP ranges
            ranges["Microsoft Teams"] = (IPAddress.Parse("52.108.0.0"), IPAddress.Parse("52.108.255.255"));
            ranges["Microsoft Office 365"] = (IPAddress.Parse("52.96.0.0"), IPAddress.Parse("52.127.255.255"));
            ranges["Microsoft Azure"] = (IPAddress.Parse("20.0.0.0"), IPAddress.Parse("20.255.255.255"));
            ranges["Microsoft OneDrive"] = (IPAddress.Parse("52.121.0.0"), IPAddress.Parse("52.121.255.255"));
            ranges["Microsoft Exchange Online"] = (IPAddress.Parse("40.92.0.0"), IPAddress.Parse("40.107.255.255"));
            ranges["Microsoft SharePoint"] = (IPAddress.Parse("52.244.0.0"), IPAddress.Parse("52.244.255.255"));
            ranges["Microsoft Graph API"] = (IPAddress.Parse("40.126.0.0"), IPAddress.Parse("40.126.255.255"));
            ranges["Microsoft Authentication"] = (IPAddress.Parse("40.124.0.0"), IPAddress.Parse("40.124.255.255"));
            ranges["Skype for Business"] = (IPAddress.Parse("52.114.0.0"), IPAddress.Parse("52.114.255.255"));
            ranges["Microsoft Edge Update"] = (IPAddress.Parse("13.107.42.0"), IPAddress.Parse("13.107.42.255"));
            
            // Additional Azure ranges
            ranges["Azure West US"] = (IPAddress.Parse("13.91.0.0"), IPAddress.Parse("13.91.255.255"));
            ranges["Azure East US"] = (IPAddress.Parse("52.168.0.0"), IPAddress.Parse("52.168.255.255"));
            
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
