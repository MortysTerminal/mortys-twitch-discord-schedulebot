using Discord;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mortys_twitch_discord_schedulebot.Models;

namespace mortys_twitch_discord_schedulebot.Services
{
    public class DiscordService : IAsyncDisposable
    {
        private readonly DiscordRestClient _client;
        private readonly ulong _guildId;
        private readonly ILogger<DiscordService> _logger;
        private readonly string _configuration_token;
        // Controls the language used in Discord event content (labels, fallback strings).
        // Set Discord__EventLanguage=de for German, defaults to English.
        private readonly bool _germanEvents;

        public DiscordService(IConfiguration configuration, ILogger<DiscordService> logger)
        {
            _logger = logger;

            var token = configuration["Discord:BotToken"]
                ?? throw new InvalidOperationException("Discord:BotToken is missing from configuration.");

            _configuration_token = token;

            var guildIdRaw = configuration["Discord:GuildId"]
                ?? throw new InvalidOperationException("Discord:GuildId is missing from configuration.");

            if (!ulong.TryParse(guildIdRaw, out _guildId))
                throw new InvalidOperationException("Discord:GuildId is invalid — must be a numeric value.");

            var lang = configuration["Discord:EventLanguage"] ?? "en";
            _germanEvents = lang.Equals("de", StringComparison.OrdinalIgnoreCase);

            _client = new DiscordRestClient();

            _logger.LogInformation(
                "DiscordService initialized (event language: {Language}).",
                _germanEvents ? "de" : "en"
            );
        }

        public async Task LoginAsync()
        {
            await _client.LoginAsync(TokenType.Bot, _configuration_token);
            _logger.LogInformation("Discord login successful.");
        }

        public async Task SyncEventsAsync(List<StreamScheduleEntry> scheduleEntries)
        {
            _logger.LogInformation("Starting Discord event synchronization...");

            var guild = await _client.GetGuildAsync(_guildId);

            if (guild == null)
            {
                _logger.LogError("Discord server (GuildId: {GuildId}) not found.", _guildId);
                return;
            }

            var existingEvents = (await guild.GetEventsAsync()).ToList();

            _logger.LogInformation(
                "Found {ScheduleCount} Twitch entries and {EventCount} existing Discord events.",
                scheduleEntries.Count,
                existingEvents.Count
            );

            var processedEventIds = new HashSet<ulong>();

            foreach (var entry in scheduleEntries)
            {
                var matchingEvent = existingEvents.FirstOrDefault(e =>
                    e.Description != null &&
                    e.Description.Contains(entry.TwitchSegmentId)
                );

                if (matchingEvent == null)
                {
                    var createdEvent = await CreateEventAsync(guild, entry);
                    if (createdEvent != null)
                        processedEventIds.Add(createdEvent.Id);
                }
                else
                {
                    await UpdateEventIfChangedAsync(matchingEvent, entry);
                    processedEventIds.Add(matchingEvent.Id);
                }
            }

            foreach (var existingEvent in existingEvents)
            {
                if (existingEvent.Description == null) continue;

                if (!processedEventIds.Contains(existingEvent.Id) && IsOurBotEvent(existingEvent, scheduleEntries))
                {
                    _logger.LogInformation(
                        "Discord event '{Name}' is no longer in the schedule — deleting.",
                        existingEvent.Name
                    );
                    await existingEvent.DeleteAsync();
                }
            }

            _logger.LogInformation("Discord event synchronization complete.");
        }

        private async Task<RestGuildEvent?> CreateEventAsync(RestGuild guild, StreamScheduleEntry entry)
        {
            try
            {
                var endTime = (entry.EndTime.HasValue && entry.EndTime.Value > entry.StartTime)
                    ? entry.EndTime.Value
                    : entry.StartTime.AddHours(3);

                var eventName = BuildEventName(entry);
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
                    "Discord event created: '{Name}' on {Date}",
                    eventName,
                    entry.StartTime.ToString("yyyy-MM-dd HH:mm")
                );

                return guildEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Discord event for '{Title}'.", entry.Title);
                return null;
            }
        }

        private async Task UpdateEventIfChangedAsync(RestGuildEvent existingEvent, StreamScheduleEntry entry)
        {
            var expectedName = BuildEventName(entry);
            var expectedDescription = BuildEventDescription(entry);
            var expectedEndTime = (entry.EndTime.HasValue && entry.EndTime.Value > entry.StartTime)
                ? entry.EndTime.Value
                : entry.StartTime.AddHours(3);

            bool hasChanged =
                existingEvent.Name != expectedName ||
                existingEvent.StartTime != entry.StartTime ||
                existingEvent.Description != expectedDescription;

            if (!hasChanged)
            {
                _logger.LogInformation("Event '{Name}' is up to date — no changes needed.", existingEvent.Name);
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

                _logger.LogInformation("Discord event updated: '{Name}'", expectedName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update event '{Name}'.", existingEvent.Name);
            }
        }

        // Builds the Discord event title, applying a language-specific fallback when Twitch has no title set.
        private string BuildEventName(StreamScheduleEntry entry)
        {
            var title = string.IsNullOrWhiteSpace(entry.Title)
                ? (_germanEvents ? $"Stream von {entry.StreamerDisplayName}" : $"Stream by {entry.StreamerDisplayName}")
                : entry.Title;

            var eventName = $"{entry.StreamerDisplayName} – {title}";

            return eventName.Length > 100 ? eventName[..97] + "..." : eventName;
        }

        // Builds the Discord event description with language-specific labels.
        // The hidden [twitch-segment-id:...] tag at the end is used to identify bot-managed events.
        private string BuildEventDescription(StreamScheduleEntry entry)
        {
            var berlinZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            var localStart = TimeZoneInfo.ConvertTime(entry.StartTime, berlinZone);

            var category = string.IsNullOrEmpty(entry.CategoryName)
                ? (_germanEvents ? "Keine Kategorie" : "No Category")
                : entry.CategoryName;

            if (_germanEvents)
            {
                return $"""
                        🎬 Inhalt: {category}
                        📺 Kanal: {entry.ChannelUrl}
                        🕐 Start: {localStart:dd.MM.yyyy HH:mm} Uhr

                        [twitch-segment-id:{entry.TwitchSegmentId}]
                        """;
            }

            return $"""
                    🎬 Content: {category}
                    📺 Channel: {entry.ChannelUrl}
                    🕐 Start: {localStart:dd.MM.yyyy HH:mm}

                    [twitch-segment-id:{entry.TwitchSegmentId}]
                    """;
        }

        private static bool IsOurBotEvent(RestGuildEvent guildEvent, List<StreamScheduleEntry> allEntries)
        {
            if (guildEvent.Description == null) return false;
            return guildEvent.Description.Contains("[twitch-segment-id:");
        }

        public async ValueTask DisposeAsync()
        {
            await _client.LogoutAsync();
            _client.Dispose();
        }
    }
}
