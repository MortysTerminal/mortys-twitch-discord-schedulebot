using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using mortys_twitch_discord_schedulebot.Services;
using Serilog;
using TwitchLib.Api.Helix.Models.Extensions.ReleasedExtensions;
using TwitchLib.Api.ThirdParty.ModLookup;

// ── Serilog Logger konfigurieren ──────────────────────────────────────────────
// Dieser Logger läuft bevor der Host gestartet wird (für Startfehler)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Mortys Twitch-Discord Schedulebot startet...");

    var host = Host.CreateDefaultBuilder(args)

        // ── Konfiguration ──────────────────────────────────────────────────────
        .ConfigureAppConfiguration((context, config) =>
        {
            config
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
        })

        // ── Serilog als Logging-Provider einsetzen ─────────────────────────────
        .UseSerilog((context, services, loggerConfig) =>
        {
            loggerConfig
                .MinimumLevel.Information()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
        })

        // ── Services registrieren (Dependency Injection) ───────────────────────
        .ConfigureServices((context, services) =>
        {
            // Unsere eigenen Services
            services.AddSingleton<TwitchService>();
            services.AddSingleton<DiscordService>();

            // SyncService als Hosted Service – startet automatisch mit dem Bot
            services.AddHostedService<SyncService>();
        })

        .Build();

    // Bot starten – läuft jetzt bis er manuell gestoppt wird (Strg+C oder Docker stop)
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Bot konnte nicht gestartet werden.");
}
finally
{
    // Serilog sauber beenden
    await Log.CloseAndFlushAsync();
}