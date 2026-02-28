using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mortys_twitch_discord_schedulebot.Models;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Schedule;

namespace mortys_twitch_discord_schedulebot.Services
{
    /// <summary>
    /// Verantwortlich für die Kommunikation mit der Twitch API.
    /// Holt die Stream-Zeitpläne aller konfigurierten Kanäle.
    /// </summary>
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi;
        private readonly List<TwitchChannelConfig> _channels;
        private readonly ILogger<TwitchService> _logger;
        private readonly int _lookaheadDays;

        public TwitchService(IConfiguration configuration, ILogger<TwitchService> logger)
        {
            _logger = logger;

            // Twitch API Zugangsdaten aus der Konfiguration laden
            var clientId = configuration["Twitch:ClientId"]
                ?? throw new InvalidOperationException("Twitch ClientId fehlt in der Konfiguration.");

            var clientSecret = configuration["Twitch:ClientSecret"]
                ?? throw new InvalidOperationException("Twitch ClientSecret fehlt in der Konfiguration.");

            // Kanäle aus der Konfiguration laden
            _channels = configuration
                .GetSection("Twitch:Channels")
                .Get<List<TwitchChannelConfig>>()
                ?? throw new InvalidOperationException("Keine Twitch-Kanäle in der Konfiguration gefunden.");

            _lookaheadDays = configuration.GetValue<int>("Sync:LookaheadDays", 14);
            // Der zweite Parameter "14" ist der Fallback-Wert, falls der Key fehlt

            // TwitchLib initialisieren
            _twitchApi = new TwitchAPI();
            _twitchApi.Settings.ClientId = clientId;
            _twitchApi.Settings.Secret = clientSecret;

            _logger.LogInformation("TwitchService initialisiert mit {Count} Kanal/Kanälen.", _channels.Count);
        }

        /// <summary>
        /// Holt die Stream-Zeitpläne aller konfigurierten Kanäle.
        /// Gibt eine Liste von StreamScheduleEntry zurück.
        /// </summary>
        public async Task<List<StreamScheduleEntry>> GetAllSchedulesAsync()
        {
            var allEntries = new List<StreamScheduleEntry>();

            // Zugangstoken von Twitch holen (App Access Token)
            await AuthenticateAsync();

            foreach (var channel in _channels)
            {
                _logger.LogInformation("Hole Zeitplan für Kanal: {Channel}", channel.Username);

                var entries = await GetScheduleForChannelAsync(channel);
                allEntries.AddRange(entries);

                _logger.LogInformation(
                    "Kanal {Channel}: {Count} Einträge gefunden.",
                    channel.Username,
                    entries.Count
                );
            }

            return allEntries;
        }

        /// <summary>
        /// Authentifiziert den Bot bei der Twitch API.
        /// </summary>
        private async Task AuthenticateAsync()
        {
            try
            {
                var token = await _twitchApi.Auth.GetAccessTokenAsync();
                _twitchApi.Settings.AccessToken = token;
                _logger.LogInformation("Twitch Authentifizierung erfolgreich.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Twitch Authentifizierung.");
                throw;
            }
        }

        /// <summary>
        /// Holt den Zeitplan eines einzelnen Twitch-Kanals.
        /// </summary>
        private async Task<List<StreamScheduleEntry>> GetScheduleForChannelAsync(TwitchChannelConfig channel)
        {
            var entries = new List<StreamScheduleEntry>();

            try
            {
                // Zuerst die User-ID des Kanals ermitteln (Twitch API braucht die ID, nicht den Namen)
                var userResponse = await _twitchApi.Helix.Users.GetUsersAsync(
                    logins: new List<string> { channel.Username }
                );

                if (userResponse.Users.Length == 0)
                {
                    _logger.LogWarning("Kanal '{Channel}' wurde auf Twitch nicht gefunden.", channel.Username);
                    return entries;
                }

                var userId = userResponse.Users[0].Id;

                // Zeitplan abrufen
                var scheduleResponse = await _twitchApi.Helix.Schedule.GetChannelStreamScheduleAsync(
                    broadcasterId: userId,
                    first: 10 // Maximal 10 kommende Events holen
                );

                if (scheduleResponse?.Schedule?.Segments == null)
                {
                    _logger.LogWarning("Kein Zeitplan für Kanal '{Channel}' gefunden.", channel.Username);
                    return entries;
                }

                // Jeden Zeitplan-Eintrag in unser internes Model umwandeln
                foreach (var segment in scheduleResponse.Schedule.Segments)
                {
                    // Nur zukünftige Events berücksichtigen
                    // Nur Events in den nächsten 14 Tagen berücksichtigen
                    var cutoff = DateTimeOffset.UtcNow.AddDays(_lookaheadDays);

                    if (segment.StartTime <= DateTimeOffset.UtcNow || segment.StartTime > cutoff)
                        continue;

                    var title = string.IsNullOrWhiteSpace(segment.Title)
                        ? $"Stream von {channel.DisplayName}"
                        : segment.Title;

                    entries.Add(new StreamScheduleEntry
                    {
                        TwitchSegmentId = segment.Id,
                        StreamerUsername = channel.Username,
                        StreamerDisplayName = channel.DisplayName,
                        ChannelUrl = channel.ChannelUrl,
                        Title = title,
                        CategoryName = segment.Category?.Name ?? "Keine Kategorie",
                        StartTime = segment.StartTime,
                        EndTime = segment.EndTime
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Abrufen des Zeitplans für '{Channel}'.", channel.Username);
            }

            return entries;
        }
    }
}