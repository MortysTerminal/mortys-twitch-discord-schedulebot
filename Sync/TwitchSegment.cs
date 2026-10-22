namespace mortys_twitch_discord_schedulebot.Sync
{
    /// <summary>
    /// One segment of a Twitch streaming schedule, independent of the API client's types.
    /// </summary>
    /// <param name="EndTime">Null when Twitch has no end time for the segment.</param>
    /// <param name="IsCanceled">True when the broadcaster canceled this occurrence of a recurring segment.</param>
    public record TwitchSegment(
        string Id,
        string Title,
        string CategoryName,
        DateTimeOffset StartTime,
        DateTimeOffset? EndTime,
        bool IsCanceled
    );

    /// <summary>
    /// The broadcaster's vacation, during which no segments take place.
    /// </summary>
    public record VacationWindow(DateTimeOffset StartTime, DateTimeOffset EndTime)
    {
        public bool Contains(DateTimeOffset time) => time >= StartTime && time < EndTime;
    }
}
