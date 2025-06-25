using Microsoft.EntityFrameworkCore;
using MicrosoftEndpointMonitor.Data;
using MicrosoftEndpointMonitor.Api.Hubs;
using MicrosoftEndpointMonitor.Service;
using MicrosoftEndpointMonitor.Service.Collectors;
using MicrosoftEndpointMonitor.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
builder.Services.AddDbContext<NetworkContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=../database/network_monitor.db";
    options.UseSqlite(connectionString);
});

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowElectronApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "file://")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add monitoring services
builder.Services.AddScoped<TcpConnectionEnumerator>();
builder.Services.AddScoped<MicrosoftEndpointDetector>();

// Add hosted services
builder.Services.AddHostedService<NetworkMonitorService>();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseCors("AllowElectronApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NetworkMonitorHub>("/networkhub");

// Health check endpoint
app.MapGet("/health", () => new { 
    Status = "Healthy", 
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0"
});

// Initialize database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NetworkContext>();
    try
    {
        await context.Database.EnsureCreatedAsync();
        
        // Run any pending migrations
        if (context.Database.GetPendingMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }
        
        app.Logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize database");
        throw;
    }
}

app.Logger.LogInformation("Microsoft Endpoint Monitor API starting on {Environment}", app.Environment.EnvironmentName);

app.Run();
