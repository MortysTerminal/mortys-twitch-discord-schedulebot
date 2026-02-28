namespace mortys_twitch_discord_schedulebot.Models
{
    /// <summary>
    /// Repräsentiert einen konfigurierten Twitch-Kanal.
    /// Wird aus der appsettings.json geladen.
    /// </summary>
    public class TwitchChannelConfig
    {
        /// <summary>
        /// Der Twitch-Username des Kanals (kleingeschrieben, wie in der URL).
        /// Beispiel: "shroud"
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Der Anzeigename, der im Discord Event erscheint.
        /// Beispiel: "Shroud"
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gibt den vollständigen Twitch-Kanal-Link zurück.
        /// </summary>
        public string ChannelUrl => $"https://www.twitch.tv/{Username}";
    }
}