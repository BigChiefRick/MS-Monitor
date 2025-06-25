using Microsoft.EntityFrameworkCore;
using MicrosoftEndpointMonitor.Data;
using MicrosoftEndpointMonitor.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MS-Monitor API", Version = "v1" });
});

// Add SignalR
builder.Services.AddSignalR();

// Add CORS for Electron app
builder.Services.AddCors(options =>
{
    options.AddPolicy("ElectronApp", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Entity Framework
builder.Services.AddDbContext<NetworkContext>(options =>
    options.UseSqlite("Data Source=../database/endpoint_monitor.db"));

var app = builder.Build();

// Configure pipeline
app.UseCors("ElectronApp");

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MS-Monitor API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();
app.MapControllers();

// Add SignalR hub
app.MapHub<NetworkMonitorHub>("/networkhub");

app.Run();
