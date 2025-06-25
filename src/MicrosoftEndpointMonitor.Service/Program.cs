using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using MicrosoftEndpointMonitor.Data;
using MicrosoftEndpointMonitor.Service.Collectors;
using MicrosoftEndpointMonitor.Service.Services;
using MicrosoftEndpointMonitor.Api.Hubs;

namespace MicrosoftEndpointMonitor.Service;

/// <summary>
/// Entry point for the Microsoft Endpoint Monitor Service
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var host = CreateHostBuilder(args).Build();
            
            // Initialize database on startup
            using (var scope = host.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<NetworkContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                
                try
                {
                    await context.Database.EnsureCreatedAsync();
                    logger.LogInformation("Database initialized successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize database");
                    throw;
                }
            }

            Console.WriteLine("Microsoft Endpoint Monitor Service starting...");
            Console.WriteLine("Press Ctrl+C to stop the service");

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                    optional: true, reloadOnChange: true);
                config.AddCommandLine(args);
                config.AddEnvironmentVariables("MEM_"); // Microsoft Endpoint Monitor prefix
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // Add Entity Framework
                services.AddDbContext<NetworkContext>(options =>
                {
                    var connectionString = configuration.GetConnectionString("DefaultConnection") 
                        ?? "Data Source=../database/network_monitor.db";
                    options.UseSqlite(connectionString);
                });

                // Add monitoring services
                services.AddScoped<TcpConnectionEnumerator>();
                services.AddScoped<MicrosoftEndpointDetector>();

                // Add SignalR client for communicating with API
                services.AddSingleton<IHubConnectionBuilder>(provider =>
                {
                    var hubUrl = configuration.GetValue<string>("SignalR:HubUrl", "http://localhost:5000/networkhub");
                    return new HubConnectionBuilder().WithUrl(hubUrl);
                });

                // Create a mock SignalR hub context for the service
                // In production, this would connect to the actual API
                services.AddSingleton<IHubContext<NetworkMonitorHub>>(provider =>
                {
                    return new MockHubContext<NetworkMonitorHub>();
                });

                // Add the main monitoring service
                services.AddHostedService<NetworkMonitorService>();

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                    builder.AddDebug();
                    
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        builder.SetMinimumLevel(LogLevel.Debug);
                    }
                    else
                    {
                        builder.SetMinimumLevel(LogLevel.Information);
                    }
                });
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            })
            .UseWindowsService(options =>
            {
                options.ServiceName = "Microsoft Endpoint Monitor";
            })
            .UseConsoleLifetime();
}

/// <summary>
/// Mock SignalR Hub Context for standalone service operation
/// </summary>
public class MockHubContext<T> : IHubContext<T> where T : Hub
{
    public IHubClients Clients => new MockHubClients();
    public IGroupManager Groups => new MockGroupManager();
}

/// <summary>
/// Mock SignalR Clients for standalone service operation
/// </summary>
public class MockHubClients : IHubClients
{
    public IClientProxy All => new MockClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new MockClientProxy();
    public IClientProxy Client(string connectionId) => new MockClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new MockClientProxy();
    public IClientProxy Group(string groupName) => new MockClientProxy();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new MockClientProxy();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new MockClientProxy();
    public IClientProxy User(string userId) => new MockClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new MockClientProxy();
}

/// <summary>
/// Mock SignalR Client Proxy for standalone service operation
/// </summary>
public class MockClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        // Log the message that would have been sent
        Console.WriteLine($"[SignalR Mock] Would send: {method} with {args?.Length ?? 0} arguments");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock SignalR Group Manager for standalone service operation
/// </summary>
public class MockGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SignalR Mock] Would add {connectionId} to group {groupName}");
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[SignalR Mock] Would remove {connectionId} from group {groupName}");
        return Task.CompletedTask;
    }
}
}
