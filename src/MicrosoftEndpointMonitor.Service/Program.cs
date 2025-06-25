using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
                    services.AddHostedService<NetworkMonitorService>();
                })
                .Build();

            await host.RunAsync();
        }
    }

    public class NetworkMonitorService : BackgroundService
    {
        private readonly ILogger<NetworkMonitorService> _logger;

        public NetworkMonitorService(ILogger<NetworkMonitorService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MS-Monitor Service started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Monitoring network connections...");
                
                // TODO: Add actual monitoring logic here
                
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
