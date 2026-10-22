using mortys_twitch_discord_schedulebot.Models;

namespace mortys_twitch_discord_schedulebot.Sync
{
    /// <summary>
    /// Builds the Discord event content (name, description, end time) for a Twitch schedule entry.
    /// Pure: no I/O, no clock.
    /// </summary>
    public class EventFormatter
    {
        // Marks bot-managed events; the segment ID follows the colon.
        public const string SegmentTagPrefix = "[twitch-segment-id:";

        // Discord limits event names to 100 characters.
        private const int MaxNameLength = 100;

        // Used when Twitch has no end time (or an end time not after the start).
        private static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(3);

        private readonly bool _germanEvents;
        private readonly TimeZoneInfo _displayTimeZone;

        public EventFormatter(bool germanEvents, TimeZoneInfo displayTimeZone)
        {
            _germanEvents = germanEvents;
            _displayTimeZone = displayTimeZone;
        }

        /// <summary>
        /// Creates a formatter from the configured language ("de" for German, anything else English),
        /// showing start times in Europe/Berlin.
        /// </summary>
        public static EventFormatter FromLanguage(string? language) =>
            new(
                germanEvents: string.Equals(language, "de", StringComparison.OrdinalIgnoreCase),
                displayTimeZone: TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")
            );

        public bool IsGerman => _germanEvents;

        // Builds the Discord event title, applying a language-specific fallback when Twitch has no title set.
        public string BuildName(StreamScheduleEntry entry)
        {
            var title = string.IsNullOrWhiteSpace(entry.Title)
                ? (_germanEvents ? $"Stream von {entry.StreamerDisplayName}" : $"Stream by {entry.StreamerDisplayName}")
                : entry.Title;

            var eventName = $"{entry.StreamerDisplayName} – {title}";

            return eventName.Length > MaxNameLength ? eventName[..(MaxNameLength - 3)] + "..." : eventName;
        }

        // Builds the Discord event description with language-specific labels.
        // The hidden [twitch-segment-id:...] tag at the end is used to identify bot-managed events.
        public string BuildDescription(StreamScheduleEntry entry)
        {
            var localStart = TimeZoneInfo.ConvertTime(entry.StartTime, _displayTimeZone);

            var category = string.IsNullOrEmpty(entry.CategoryName)
                ? (_germanEvents ? "Keine Kategorie" : "No Category")
                : entry.CategoryName;

            if (_germanEvents)
            {
                return $"""
                        🎬 Inhalt: {category}
                        📺 Kanal: {entry.ChannelUrl}
                        🕐 Start: {localStart:dd.MM.yyyy HH:mm} Uhr

                        {SegmentTagPrefix}{entry.TwitchSegmentId}]
                        """;
            }

            return $"""
                    🎬 Content: {category}
                    📺 Channel: {entry.ChannelUrl}
                    🕐 Start: {localStart:dd.MM.yyyy HH:mm}

                    {SegmentTagPrefix}{entry.TwitchSegmentId}]
                    """;
        }

        public static DateTimeOffset BuildEndTime(StreamScheduleEntry entry) =>
            entry.EndTime.HasValue && entry.EndTime.Value > entry.StartTime
                ? entry.EndTime.Value
                : entry.StartTime.Add(DefaultDuration);
    }
}
