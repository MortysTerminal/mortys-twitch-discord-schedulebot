using mortys_twitch_discord_schedulebot.Models;

namespace mortys_twitch_discord_schedulebot.Sync
{
    /// <summary>
    /// Decides which Discord events to create, update or delete so that Discord mirrors the Twitch schedule.
    /// Pure: works on snapshots, performs no I/O.
    /// </summary>
    public class EventPlanner
    {
        private readonly EventFormatter _formatter;

        public EventPlanner(EventFormatter formatter)
        {
            _formatter = formatter;
        }

        public SyncPlan Plan(IReadOnlyList<StreamScheduleEntry> scheduleEntries, IReadOnlyList<ExistingEvent> existingEvents)
        {
            var creates = new List<DesiredEvent>();
            var updates = new List<EventUpdate>();
            var unchanged = new List<ExistingEvent>();
            var matchedEventIds = new HashSet<ulong>();

            foreach (var entry in scheduleEntries)
            {
                var desired = ToDesiredEvent(entry);

                // Events are matched by the segment ID embedded in their description.
                var matchingEvent = existingEvents.FirstOrDefault(e =>
                    e.Description != null &&
                    e.Description.Contains(entry.TwitchSegmentId)
                );

                if (matchingEvent == null)
                {
                    creates.Add(desired);
                    continue;
                }

                matchedEventIds.Add(matchingEvent.Id);

                if (HasChanged(matchingEvent, desired))
                    updates.Add(new EventUpdate(matchingEvent, desired));
                else
                    unchanged.Add(matchingEvent);
            }

            var deletes = existingEvents
                .Where(e => !matchedEventIds.Contains(e.Id) && IsBotEvent(e))
                .ToList();

            return new SyncPlan(creates, updates, unchanged, deletes);
        }

        public DesiredEvent ToDesiredEvent(StreamScheduleEntry entry) =>
            new(
                Entry: entry,
                Name: _formatter.BuildName(entry),
                Description: _formatter.BuildDescription(entry),
                StartTime: entry.StartTime,
                EndTime: EventFormatter.BuildEndTime(entry),
                Location: entry.ChannelUrl
            );

        public static bool IsBotEvent(ExistingEvent existingEvent) =>
            existingEvent.Description != null &&
            existingEvent.Description.Contains(EventFormatter.SegmentTagPrefix);

        // Only name, start and description are compared; any difference rewrites all fields.
        private static bool HasChanged(ExistingEvent existing, DesiredEvent desired) =>
            existing.Name != desired.Name ||
            existing.StartTime != desired.StartTime ||
            existing.Description != desired.Description;
    }
}
