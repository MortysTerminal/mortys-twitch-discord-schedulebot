using Discord;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mortys_twitch_discord_schedulebot.Models;

namespace mortys_twitch_discord_schedulebot.Services
{
    /// <summary>
    /// Verantwortlich für die gesamte Kommunikation mit Discord.
    /// Erstellt, aktualisiert und löscht Discord Events basierend auf dem Twitch-Zeitplan.
    /// </summary>
    public class DiscordService : IAsyncDisposable
    {
        private readonly DiscordRestClient _client;
        private readonly ulong _guildId;
        private readonly ILogger<DiscordService> _logger;
        private readonly string _configuration_token;

        public DiscordService(IConfiguration configuration, ILogger<DiscordService> logger)
        {
            _logger = logger;

            var token = configuration["Discord:BotToken"]
                ?? throw new InvalidOperationException("Discord BotToken fehlt in der Konfiguration.");

            _configuration_token = token;

            var guildIdRaw = configuration["Discord:GuildId"]
                ?? throw new InvalidOperationException("Discord GuildId fehlt in der Konfiguration.");

            if (!ulong.TryParse(guildIdRaw, out _guildId))
                throw new InvalidOperationException("Discord GuildId ist ungültig – muss eine Zahl sein.");

            // Wir nutzen den RestClient – kein Gateway nötig, da wir nur Events verwalten
            _client = new DiscordRestClient();

            _logger.LogInformation("DiscordService initialisiert.");
        }

        /// <summary>
        /// Verbindet den Bot mit Discord.
        /// Muss einmal vor allen anderen Operationen aufgerufen werden.
        /// </summary>
        public async Task LoginAsync()
        {
            await _client.LoginAsync(TokenType.Bot, _configuration_token);
            _logger.LogInformation("Discord Login erfolgreich.");
        }

        /// <summary>
        /// Synchronisiert die Twitch-Zeitpläne mit den Discord Events.
        /// - Neue Einträge werden als Events erstellt
        /// - Bestehende Events werden aktualisiert falls sich etwas geändert hat
        /// - Events die nicht mehr im Zeitplan sind werden gelöscht
        /// </summary>
        public async Task SyncEventsAsync(List<StreamScheduleEntry> scheduleEntries)
        {
            _logger.LogInformation("Starte Discord Event Synchronisation...");

            var guild = await _client.GetGuildAsync(_guildId);

            if (guild == null)
            {
                _logger.LogError("Discord Server (GuildId: {GuildId}) nicht gefunden.", _guildId);
                return;
            }

            // Alle bestehenden Discord Events laden
            var existingEvents = (await guild.GetEventsAsync()).ToList();

            _logger.LogInformation(
                "Gefunden: {ScheduleCount} Twitch-Einträge, {EventCount} bestehende Discord Events.",
                scheduleEntries.Count,
                existingEvents.Count
            );

            // Für jeden Twitch-Zeitplaneintrag prüfen ob ein Event existiert oder erstellt werden muss
            var processedEventIds = new HashSet<ulong>();

            foreach (var entry in scheduleEntries)
            {
                // Wir erkennen ein zusammengehöriges Event anhand des Titels + Streamers in der Description
                var matchingEvent = existingEvents.FirstOrDefault(e =>
                    e.Description != null &&
                    e.Description.Contains(entry.TwitchSegmentId)
                );

                if (matchingEvent == null)
                {
                    // Event existiert noch nicht → erstellen
                    var createdEvent = await CreateEventAsync(guild, entry);
                    if (createdEvent != null)
                        processedEventIds.Add(createdEvent.Id);
                }
                else
                {
                    // Event existiert bereits → aktualisieren falls nötig
                    await UpdateEventIfChangedAsync(matchingEvent, entry);
                    processedEventIds.Add(matchingEvent.Id);
                }
            }

            // Events löschen die nicht mehr im Twitch-Zeitplan sind
            // (nur Events die von unserem Bot erstellt wurden – erkennbar an der TwitchSegmentId in der Description)
            foreach (var existingEvent in existingEvents)
            {
                if (existingEvent.Description == null) continue;

                // Prüfen ob dieses Event von unserem Bot stammt (enthält eine Twitch Segment ID)
                bool isBotEvent = scheduleEntries.Any(e =>
                    existingEvent.Description.Contains(e.TwitchSegmentId)
                );

                // Wenn es ein Bot-Event ist, aber nicht mehr in processedEventIds → löschen
                bool isOurBotEvent = existingEvents
                    .Where(e => e.Description != null)
                    .Any(e => scheduleEntries.Any(s => e.Description!.Contains(s.TwitchSegmentId)) &&
                               e.Id == existingEvent.Id);

                if (!processedEventIds.Contains(existingEvent.Id) && IsOurBotEvent(existingEvent, scheduleEntries))
                {
                    _logger.LogInformation(
                        "Discord Event '{Name}' ist nicht mehr im Zeitplan – wird gelöscht.",
                        existingEvent.Name
                    );
                    await existingEvent.DeleteAsync();
                }
            }

            _logger.LogInformation("Discord Event Synchronisation abgeschlossen.");
        }

        /// <summary>
        /// Erstellt ein neues Discord Event aus einem Twitch-Zeitplaneintrag.
        /// </summary>
        private async Task<RestGuildEvent?> CreateEventAsync(RestGuild guild, StreamScheduleEntry entry)
        {
            try
            {
                // Sicherstellen dass EndTime immer NACH StartTime liegt
                var endTime = (entry.EndTime.HasValue && entry.EndTime.Value > entry.StartTime)
                    ? entry.EndTime.Value
                    : entry.StartTime.AddHours(3);

                // Der Titel zeigt: "Streamername – Streamtitel"
                var eventName = $"{entry.StreamerDisplayName} – {entry.Title}";

                // Maximale Discord-Titellänge ist 100 Zeichen
                if (eventName.Length > 100)
                    eventName = eventName[..97] + "...";

                // Die Description enthält alle Details + die TwitchSegmentId zur späteren Erkennung
                var description = BuildEventDescription(entry);

                var guildEvent = await guild.CreateEventAsync(
                    name: eventName,
                    startTime: entry.StartTime,
                    type: GuildScheduledEventType.External,
                    privacyLevel: GuildScheduledEventPrivacyLevel.Private,
                    description: description,
                    endTime: endTime,
                    location: entry.ChannelUrl
                );

                _logger.LogInformation(
                    "Discord Event erstellt: '{Name}' am {Date}",
                    eventName,
                    entry.StartTime.ToString("dd.MM.yyyy HH:mm")
                );

                return guildEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Erstellen des Discord Events für '{Title}'.", entry.Title);
                return null;
            }
        }

        /// <summary>
        /// Aktualisiert ein bestehendes Discord Event, wenn sich Titel, Zeit oder Spiel geändert haben.
        /// </summary>
        private async Task UpdateEventIfChangedAsync(RestGuildEvent existingEvent, StreamScheduleEntry entry)
        {
            var expectedName = $"{entry.StreamerDisplayName} – {entry.Title}";
            if (expectedName.Length > 100)
                expectedName = expectedName[..97] + "...";

            var expectedDescription = BuildEventDescription(entry);
            var expectedEndTime = (entry.EndTime.HasValue && entry.EndTime.Value > entry.StartTime)
                ? entry.EndTime.Value
                : entry.StartTime.AddHours(3);

            // Prüfen ob sich irgendetwas geändert hat
            bool hasChanged =
                existingEvent.Name != expectedName ||
                existingEvent.StartTime != entry.StartTime ||
                existingEvent.Description != expectedDescription;

            if (!hasChanged)
            {
                _logger.LogInformation("Event '{Name}' ist aktuell – keine Änderung nötig.", existingEvent.Name);
                return;
            }

            try
            {
                await existingEvent.ModifyAsync(props =>
                {
                    props.Name = expectedName;
                    props.StartTime = entry.StartTime;
                    props.EndTime = expectedEndTime;
                    props.Description = expectedDescription;
                    props.Location = entry.ChannelUrl;
                });

                _logger.LogInformation("Discord Event aktualisiert: '{Name}'", expectedName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Aktualisieren des Events '{Name}'.", existingEvent.Name);
            }
        }

        /// <summary>
        /// Baut den Beschreibungstext für ein Discord Event.
        /// Die TwitchSegmentId ist versteckt am Ende – sie dient zur Wiedererkennung.
        /// </summary>
        private static string BuildEventDescription(StreamScheduleEntry entry)
        {
            // Deutsche Zeitzone – funktioniert korrekt für CET (UTC+1) und CEST (UTC+2)
            var germanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            var localStartTime = TimeZoneInfo.ConvertTime(entry.StartTime, germanTimeZone);

            return $"""
                    🎬 Inhalt: {entry.CategoryName}
                    📺 Kanal: {entry.ChannelUrl}
                    🕐 Start: {localStartTime:dd.MM.yyyy HH:mm} Uhr

                    [twitch-segment-id:{entry.TwitchSegmentId}]
                    """;
        }

        /// <summary>
        /// Prüft ob ein Discord Event von unserem Bot erstellt wurde.
        /// Erkennungsmerkmal: Die Description enthält eine bekannte TwitchSegmentId.
        /// </summary>
        private static bool IsOurBotEvent(RestGuildEvent guildEvent, List<StreamScheduleEntry> allEntries)
        {
            if (guildEvent.Description == null) return false;

            // Ein Bot-Event enthält immer das Muster [twitch-segment-id:...]
            return guildEvent.Description.Contains("[twitch-segment-id:");
        }

        public async ValueTask DisposeAsync()
        {
            await _client.LogoutAsync();
            _client.Dispose();
        }
    }
}