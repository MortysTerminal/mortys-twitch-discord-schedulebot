using mortys_twitch_discord_schedulebot.Models;
using mortys_twitch_discord_schedulebot.Sync;

namespace mortys_twitch_discord_schedulebot.Tests
{
    internal static class TestData
    {
        public static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        public static EventFormatter English => new(germanEvents: false, displayTimeZone: Berlin);
        public static EventFormatter German => new(germanEvents: true, displayTimeZone: Berlin);

        public static StreamScheduleEntry Entry(
            string segmentId = "segment-1",
            string title = "Minecraft Monday",
            string category = "Minecraft",
            DateTimeOffset? start = null,
            DateTimeOffset? end = null) =>
            new()
            {
                TwitchSegmentId = segmentId,
                StreamerUsername = "somechannel",
                StreamerDisplayName = "Some Channel",
                ChannelUrl = "https://www.twitch.tv/somechannel",
                Title = title,
                CategoryName = category,
                StartTime = start ?? new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero),
                EndTime = end,
            };

        // An existing Discord event exactly as the bot would have created it for the entry.
        public static ExistingEvent ExistingFor(StreamScheduleEntry entry, ulong id = 1, EventFormatter? formatter = null)
        {
            var desired = new EventPlanner(formatter ?? English).ToDesiredEvent(entry);
            return new ExistingEvent(id, desired.Name, desired.Description, desired.StartTime, desired.EndTime, desired.Location);
        }
    }
}
