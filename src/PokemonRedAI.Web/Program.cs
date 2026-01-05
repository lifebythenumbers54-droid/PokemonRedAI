using Microsoft.AspNetCore.Components.Server.Circuits;
using PokemonRedAI.Web.Hubs;
using PokemonRedAI.Web.Services;
using Serilog;
using Serilog.Events;

// Configure Serilog early to capture startup errors
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "pokemonredai-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(logPath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=== Pokemon Red AI Starting ===");
    Log.Information("Log file location: {LogPath}", logPath);

    // Add global unhandled exception handlers
    AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
    {
        Log.Fatal((Exception)args.ExceptionObject, "Unhandled AppDomain exception");
        Log.CloseAndFlush();
    };

    TaskScheduler.UnobservedTaskException += (sender, args) =>
    {
        Log.Error(args.Exception, "Unobserved task exception");
        args.SetObserved();
    };

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for all logging
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor(options =>
    {
        options.DetailedErrors = true;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
    });
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
        options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB
    });

    // Register custom services
    builder.Services.AddSingleton<GameStateService>();
    builder.Services.AddScoped<CircuitHandler, CircuitHandlerService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // Removed UseHttpsRedirection - not needed for local development and was causing warnings
    app.UseStaticFiles();
    app.UseRouting();

    app.MapBlazorHub();
    app.MapHub<GameHub>("/gamehub");
    app.MapFallbackToPage("/_Host");

    Log.Information("Pokemon Red AI Web Server starting...");
    Log.Information("Navigate to http://localhost:5187 to access the dashboard");
    Log.Information("Log files are written to: {LogDir}", Path.GetDirectoryName(logPath));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
