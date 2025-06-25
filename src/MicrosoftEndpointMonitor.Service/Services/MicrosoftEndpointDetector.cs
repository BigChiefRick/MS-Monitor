using Microsoft.Extensions.Logging;

namespace MicrosoftEndpointMonitor.Service.Services
{
    public class MicrosoftEndpointDetector
    {
        private readonly ILogger<MicrosoftEndpointDetector> _logger;

        public MicrosoftEndpointDetector(ILogger<MicrosoftEndpointDetector> logger)
        {
            _logger = logger;
        }

        // TODO: Implement Microsoft endpoint detection
    }
}
