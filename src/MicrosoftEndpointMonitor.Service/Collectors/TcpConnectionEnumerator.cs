using Microsoft.Extensions.Logging;

namespace MicrosoftEndpointMonitor.Service.Collectors
{
    public class TcpConnectionEnumerator
    {
        private readonly ILogger<TcpConnectionEnumerator> _logger;

        public TcpConnectionEnumerator(ILogger<TcpConnectionEnumerator> logger)
        {
            _logger = logger;
        }

        // TODO: Implement TCP connection enumeration
    }
}
