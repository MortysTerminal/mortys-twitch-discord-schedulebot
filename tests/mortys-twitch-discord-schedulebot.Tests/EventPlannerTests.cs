using mortys_twitch_discord_schedulebot.Models;
using mortys_twitch_discord_schedulebot.Sync;
using static mortys_twitch_discord_schedulebot.Tests.TestData;

namespace mortys_twitch_discord_schedulebot.Tests
{
    public class EventPlannerTests
    {
        private readonly EventPlanner _planner = new(English);

        [Fact]
        public void New_entry_is_created()
        {
            var entry = Entry();

            var plan = _planner.Plan([entry], []);

            var created = Assert.Single(plan.Creates);
            Assert.Same(entry, created.Entry);
            Assert.Equal("Some Channel – Minecraft Monday", created.Name);
            Assert.Equal(entry.StartTime, created.StartTime);
            Assert.Equal(entry.StartTime.AddHours(3), created.EndTime);
            Assert.Equal("https://www.twitch.tv/somechannel", created.Location);
            Assert.Empty(plan.Updates);
            Assert.Empty(plan.Deletes);
        }

        [Fact]
        public void Matching_event_without_changes_is_left_alone()
        {
            var entry = Entry();
            var existing = ExistingFor(entry);

            var plan = _planner.Plan([entry], [existing]);

            Assert.Equal([existing], plan.Unchanged);
            Assert.Empty(plan.Creates);
            Assert.Empty(plan.Updates);
            Assert.Empty(plan.Deletes);
        }

        [Fact]
        public void Changed_title_updates_the_event()
        {
            var existing = ExistingFor(Entry(title: "Old title"));
            var entry = Entry(title: "New title");

            var plan = _planner.Plan([entry], [existing]);

            var update = Assert.Single(plan.Updates);
            Assert.Same(existing, update.Existing);
            Assert.Equal("Some Channel – New title", update.Desired.Name);
            Assert.Empty(plan.Creates);
            Assert.Empty(plan.Deletes);
        }

        [Fact]
        public void Changed_start_updates_the_event()
        {
            var start = new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero);
            var existing = ExistingFor(Entry(start: start));
            var entry = Entry(start: start.AddHours(1));

            var plan = _planner.Plan([entry], [existing]);

            var update = Assert.Single(plan.Updates);
            Assert.Equal(start.AddHours(1), update.Desired.StartTime);
        }

        [Fact]
        public void Changed_category_updates_the_event()
        {
            var existing = ExistingFor(Entry(category: "Minecraft"));
            var entry = Entry(category: "Just Chatting");

            var plan = _planner.Plan([entry], [existing]);

            Assert.Single(plan.Updates);
        }

        [Fact]
        public void Bot_event_no_longer_in_schedule_is_deleted()
        {
            var obsolete = ExistingFor(Entry(segmentId: "gone"), id: 7);

            var plan = _planner.Plan([], [obsolete]);

            Assert.Equal([obsolete], plan.Deletes);
        }

        [Fact]
        public void Manually_created_events_are_never_touched()
        {
            var manual = new ExistingEvent(9, "Community game night", "Bring snacks", DateTimeOffset.UnixEpoch, null, "Voice");
            var withoutDescription = new ExistingEvent(10, "Quiz", null, DateTimeOffset.UnixEpoch, null, null);

            var plan = _planner.Plan([Entry()], [manual, withoutDescription]);

            Assert.Empty(plan.Deletes);
            Assert.Empty(plan.Updates);
            Assert.Single(plan.Creates);
        }

        [Fact]
        public void Mixed_cycle_creates_updates_keeps_and_deletes()
        {
            var keep = Entry(segmentId: "keep");
            var change = Entry(segmentId: "change", title: "New title");
            var add = Entry(segmentId: "add");
            var existingKeep = ExistingFor(keep, id: 1);
            var existingChange = ExistingFor(Entry(segmentId: "change", title: "Old title"), id: 2);
            var existingGone = ExistingFor(Entry(segmentId: "gone"), id: 3);

            var plan = _planner.Plan([keep, change, add], [existingKeep, existingChange, existingGone]);

            Assert.Equal(["add"], plan.Creates.Select(c => c.Entry.TwitchSegmentId));
            Assert.Equal([2UL], plan.Updates.Select(u => u.Existing.Id));
            Assert.Equal([1UL], plan.Unchanged.Select(e => e.Id));
            Assert.Equal([3UL], plan.Deletes.Select(e => e.Id));
        }

        [Fact]
        public void German_planner_creates_German_content()
        {
            var plan = new EventPlanner(German).Plan([Entry(title: "")], []);

            var created = Assert.Single(plan.Creates);
            Assert.Equal("Some Channel – Stream von Some Channel", created.Name);
            Assert.Contains("Uhr", created.Description);
        }
    }
}
