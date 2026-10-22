using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using mortys_twitch_discord_schedulebot.Models;
using mortys_twitch_discord_schedulebot.Sync;
using TwitchLib.Api;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.Schedule;

namespace mortys_twitch_discord_schedulebot.Services
{
    public class TwitchService
    {
        private readonly TwitchAPI _twitchApi;
        private readonly List<TwitchChannelConfig> _channels;
        private readonly ILogger<TwitchService> _logger;
        private readonly int _lookaheadDays;
        private readonly TimeProvider _timeProvider;

        public TwitchService(IConfiguration configuration, ILogger<TwitchService> logger, TimeProvider timeProvider)
        {
            _logger = logger;
            _timeProvider = timeProvider;

            var clientId = configuration["Twitch:ClientId"]
                ?? throw new InvalidOperationException("Twitch:ClientId is missing from configuration.");

            var clientSecret = configuration["Twitch:ClientSecret"]
                ?? throw new InvalidOperationException("Twitch:ClientSecret is missing from configuration.");

            // Load channels from configuration — populated at runtime via environment variables:
            //   Twitch__Channels__0__Username / Twitch__Channels__0__DisplayName (and so on)
            var channels = configuration
                .GetSection("Twitch:Channels")
                .Get<List<TwitchChannelConfig>>();

            // Filter out any slots that were declared in docker-compose.yml but left
            // unset in Portainer (Docker passes them as empty strings in that case).
            var validChannels = channels?
                .Where(c => !string.IsNullOrWhiteSpace(c.Username))
                .ToList();

            if (validChannels == null || validChannels.Count == 0)
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

            _channels = validChannels;
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

        // Throws when authentication fails, so the caller skips the whole cycle.
        // Per-channel failures are returned as failed results instead of throwing.
        public async Task<List<ChannelSchedule>> GetAllSchedulesAsync()
        {
            var schedules = new List<ChannelSchedule>();

            await AuthenticateAsync();

            foreach (var channel in _channels)
            {
                _logger.LogInformation("Fetching schedule for channel: {Channel}", channel.Username);

                var schedule = await GetScheduleForChannelAsync(channel);
                schedules.Add(schedule);

                if (schedule.Succeeded)
                {
                    _logger.LogInformation(
                        "Channel {Channel}: {Count} entries found.",
                        channel.Username,
                        schedule.Entries.Count
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "Channel {Channel}: schedule could not be fetched — its Discord events are kept this cycle.",
                        channel.Username
                    );
                }
            }

            return schedules;
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

        private async Task<ChannelSchedule> GetScheduleForChannelAsync(TwitchChannelConfig channel)
        {
            var entries = new List<StreamScheduleEntry>();

            try
            {
                var userResponse = await _twitchApi.Helix.Users.GetUsersAsync(
                    logins: new List<string> { channel.Username }
                );

                // Unknown login (typo, renamed channel): treat as a failure so existing events stay.
                if (userResponse.Users.Length == 0)
                {
                    _logger.LogWarning("Channel '{Channel}' was not found on Twitch.", channel.Username);
                    return ChannelSchedule.Failure(channel.Username);
                }

                var userId = userResponse.Users[0].Id;

                var scheduleResponse = await _twitchApi.Helix.Schedule.GetChannelStreamScheduleAsync(
                    broadcasterId: userId,
                    first: 10
                );

                if (scheduleResponse?.Schedule?.Segments == null)
                {
                    _logger.LogInformation("Channel '{Channel}' has no scheduled segments.", channel.Username);
                    return ChannelSchedule.Success(channel.Username, entries);
                }

                var now = _timeProvider.GetUtcNow();

                foreach (var segment in scheduleResponse.Schedule.Segments)
                {
                    if (!ScheduleWindow.Includes(segment.StartTime, now, _lookaheadDays))
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
            catch (BadResourceException)
            {
                // Twitch answers 404 when the broadcaster has not created a streaming schedule.
                _logger.LogInformation("Channel '{Channel}' has no streaming schedule.", channel.Username);
                return ChannelSchedule.Success(channel.Username, entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch schedule for channel '{Channel}'.", channel.Username);
                return ChannelSchedule.Failure(channel.Username);
            }

            return ChannelSchedule.Success(channel.Username, entries);
        }
    }
}
