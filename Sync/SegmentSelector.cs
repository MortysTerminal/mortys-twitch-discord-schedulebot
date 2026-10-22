namespace mortys_twitch_discord_schedulebot.Sync
{
    /// <summary>
    /// Selects the Twitch segments that are mirrored to Discord. Pure: no I/O, no clock.
    /// </summary>
    public static class SegmentSelector
    {
        public static IEnumerable<TwitchSegment> Select(
            IEnumerable<TwitchSegment> segments,
            VacationWindow? vacation,
            DateTimeOffset now,
            int lookaheadDays) =>
            segments.Where(s =>
                ScheduleWindow.Includes(s.StartTime, now, lookaheadDays) &&
                !s.IsCanceled &&
                (vacation == null || !vacation.Contains(s.StartTime))
            );
    }

    /// <summary>
    /// Decides whether another page of the Twitch schedule is needed to cover the look-ahead window.
    /// </summary>
    public static class SchedulePaging
    {
        // Twitch allows at most 25 segments per page.
        public const int PageSize = 25;

        // Upper bound so a misbehaving cursor can never loop forever (25 × 10 = 250 segments).
        public const int MaxPages = 10;

        public static bool NeedsNextPage(IReadOnlyCollection<TwitchSegment> page, string? cursor, DateTimeOffset cutoff) =>
            !string.IsNullOrEmpty(cursor) &&
            page.Count > 0 &&
            page.Max(s => s.StartTime) <= cutoff;
    }
}
