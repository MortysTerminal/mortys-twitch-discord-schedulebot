namespace mortys_twitch_discord_schedulebot.Models
{
    /// <summary>
    /// Repräsentiert einen einzelnen Eintrag aus dem Twitch-Streamplan.
    /// Wird später in ein Discord Event umgewandelt.
    /// </summary>
    public class StreamScheduleEntry
    {
        /// <summary>
        /// Die eindeutige Twitch Segment-ID – damit erkennen wir Duplikate.
        /// </summary>
        public string TwitchSegmentId { get; set; } = string.Empty;

        /// <summary>
        /// Der Twitch-Username des Streamers.
        /// </summary>
        public string StreamerUsername { get; set; } = string.Empty;

        /// <summary>
        /// Der Anzeigename des Streamers (für Discord sichtbar).
        /// </summary>
        public string StreamerDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Der direkte Link zum Twitch-Kanal.
        /// </summary>
        public string ChannelUrl { get; set; } = string.Empty;

        /// <summary>
        /// Der Titel des geplanten Streams (z.B. "Minecraft Montag").
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Die Kategorie/das Spiel, das gespielt wird.
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// Startzeit des Streams (in UTC).
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Endzeit des Streams (in UTC). Kann null sein, wenn keine Endzeit gesetzt.
        /// </summary>
        public DateTimeOffset? EndTime { get; set; }
    }
}