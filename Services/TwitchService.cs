using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mortys_twitch_discord_schedulebot.Models;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Schedule;

namespace mortys_twitch_discord_schedulebot.Services
{
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi;
        private readonly List<TwitchChannelConfig> _channels;
        private readonly ILogger<TwitchService> _logger;
        private readonly int _lookaheadDays;

        public TwitchService(IConfiguration configuration, ILogger<TwitchService> logger)
        {
            _logger = logger;

            var clientId = configuration["Twitch:ClientId"]
                ?? throw new InvalidOperationException("Twitch:ClientId is missing from configuration.");

            var clientSecret = configuration["Twitch:ClientSecret"]
                ?? throw new InvalidOperationException("Twitch:ClientSecret is missing from configuration.");

            // Load channels from configuration — populated at runtime via environment variables:
            //   Twitch__Channels__0__Username / Twitch__Channels__0__DisplayName (and so on)
            var channels = configuration
                .GetSection("Twitch:Channels")
                .Get<List<TwitchChannelConfig>>();

            if (channels == null || channels.Count == 0)
            {
                _logger.LogError(
                    "No Twitch channels are configured. Set at least one channel pair via environment variables: " +
                    "Twitch__Channels__0__Username and Twitch__Channels__0__DisplayName"
                );
                throw new InvalidOperationException(
                    "No Twitch channels configured. " +
                    "Set Twitch__Channels__0__Username and Twitch__Channels__0__DisplayName."
                );
            }

            _channels = channels;
            _lookaheadDays = configuration.GetValue<int>("Sync:LookaheadDays", 14);

            _twitchApi = new TwitchAPI();
            _twitchApi.Settings.ClientId = clientId;
            _twitchApi.Settings.Secret = clientSecret;

            _logger.LogInformation(
                "TwitchService initialized with {Count} channel(s): {Channels}",
                _channels.Count,
                string.Join(", ", _channels.Select(c => c.DisplayName))
            );
        }

        public async Task<List<StreamScheduleEntry>> GetAllSchedulesAsync()
        {
            var allEntries = new List<StreamScheduleEntry>();

            await AuthenticateAsync();

            foreach (var channel in _channels)
            {
                _logger.LogInformation("Fetching schedule for channel: {Channel}", channel.Username);

                var entries = await GetScheduleForChannelAsync(channel);
                allEntries.AddRange(entries);

                _logger.LogInformation(
                    "Channel {Channel}: {Count} entries found.",
                    channel.Username,
                    entries.Count
                );
            }

            return allEntries;
        }

        private async Task AuthenticateAsync()
        {
            try
            {
                var token = await _twitchApi.Auth.GetAccessTokenAsync();
                _twitchApi.Settings.AccessToken = token;
                _logger.LogInformation("Twitch authentication successful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Twitch authentication failed.");
                throw;
            }
        }

        private async Task<List<StreamScheduleEntry>> GetScheduleForChannelAsync(TwitchChannelConfig channel)
        {
            var entries = new List<StreamScheduleEntry>();

            try
            {
                var userResponse = await _twitchApi.Helix.Users.GetUsersAsync(
                    logins: new List<string> { channel.Username }
                );

                if (userResponse.Users.Length == 0)
                {
                    _logger.LogWarning("Channel '{Channel}' was not found on Twitch.", channel.Username);
                    return entries;
                }

                var userId = userResponse.Users[0].Id;

                var scheduleResponse = await _twitchApi.Helix.Schedule.GetChannelStreamScheduleAsync(
                    broadcasterId: userId,
                    first: 10
                );

                if (scheduleResponse?.Schedule?.Segments == null)
                {
                    _logger.LogWarning("No schedule found for channel '{Channel}'.", channel.Username);
                    return entries;
                }

                var cutoff = DateTimeOffset.UtcNow.AddDays(_lookaheadDays);

                foreach (var segment in scheduleResponse.Schedule.Segments)
                {
                    if (segment.StartTime <= DateTimeOffset.UtcNow || segment.StartTime > cutoff)
                        continue;

                    entries.Add(new StreamScheduleEntry
                    {
                        TwitchSegmentId = segment.Id,
                        StreamerUsername = channel.Username,
                        StreamerDisplayName = channel.DisplayName,
                        ChannelUrl = channel.ChannelUrl,
                        // Empty title and category are resolved to language-specific fallbacks in DiscordService
                        Title = segment.Title ?? string.Empty,
                        CategoryName = segment.Category?.Name ?? string.Empty,
                        StartTime = segment.StartTime,
                        EndTime = segment.EndTime
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch schedule for channel '{Channel}'.", channel.Username);
            }

            return entries;
        }
    }
}
