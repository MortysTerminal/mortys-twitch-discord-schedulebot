using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using mortys_twitch_discord_schedulebot.Services;

namespace mortys_twitch_discord_schedulebot.Services
{
    /// <summary>
    /// Der SyncService ist der Taktgeber des Bots.
    /// Er läuft als Hintergrunddienst und ruft alle X Minuten
    /// den TwitchService und DiscordService auf.
    /// </summary>
    public class SyncService : BackgroundService
    {
        private readonly TwitchService _twitchService;
        private readonly DiscordService _discordService;
        private readonly ILogger<SyncService> _logger;
        private readonly int _intervalMinutes;

        public SyncService(
            TwitchService twitchService,
            DiscordService discordService,
            IConfiguration configuration,
            ILogger<SyncService> logger)
        {
            _twitchService = twitchService;
            _discordService = discordService;
            _logger = logger;

            // Intervall aus Konfiguration lesen, Fallback: 30 Minuten
            _intervalMinutes = configuration.GetValue<int>("Sync:IntervalMinutes", 30);
        }

        /// <summary>
        /// Wird automatisch gestartet wenn der Bot hochfährt.
        /// Läuft in einer Endlosschleife bis der Bot gestoppt wird.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "SyncService gestartet – Synchronisation alle {Interval} Minuten.",
                _intervalMinutes
            );

            // Discord einmalig beim Start verbinden
            await _discordService.LoginAsync();

            // Sofort beim Start einmal synchronisieren, dann erst warten
            await RunSyncAsync();

            // Endlosschleife – läuft bis der Bot gestoppt wird
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation(
                        "Nächste Synchronisation in {Interval} Minuten.",
                        _intervalMinutes
                    );

                    // Warten bis zum nächsten Sync
                    await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);

                    // Synchronisation durchführen
                    await RunSyncAsync();
                }
                catch (TaskCanceledException)
                {
                    // Normaler Shutdown – keine Fehlermeldung nötig
                    break;
                }
                catch (Exception ex)
                {
                    // Fehler loggen aber NICHT crashen – Bot läuft weiter
                    _logger.LogError(ex, "Unerwarteter Fehler im SyncService. Nächster Versuch in {Interval} Minuten.", _intervalMinutes);
                    await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
                }
            }

            _logger.LogInformation("SyncService wird beendet.");
        }

        /// <summary>
        /// Führt einen kompletten Sync-Durchlauf durch:
        /// 1. Twitch Zeitpläne holen
        /// 2. Discord Events synchronisieren
        /// </summary>
        private async Task RunSyncAsync()
        {
            _logger.LogInformation("═══════════════════════════════════════");
            _logger.LogInformation("Starte Synchronisation...");

            try
            {
                // Schritt 1: Twitch Zeitpläne holen
                var schedules = await _twitchService.GetAllSchedulesAsync();

                _logger.LogInformation("{Count} Twitch-Einträge gefunden.", schedules.Count);

                // Schritt 2: Mit Discord synchronisieren
                await _discordService.SyncEventsAsync(schedules);

                _logger.LogInformation("Synchronisation erfolgreich abgeschlossen.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler während der Synchronisation.");
            }

            _logger.LogInformation("═══════════════════════════════════════");
        }
    }
}