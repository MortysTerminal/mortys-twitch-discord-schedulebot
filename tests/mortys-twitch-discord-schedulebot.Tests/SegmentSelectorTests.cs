using mortys_twitch_discord_schedulebot.Sync;

namespace mortys_twitch_discord_schedulebot.Tests
{
    public class SegmentSelectorTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

        private static TwitchSegment Segment(string id, DateTimeOffset start, bool canceled = false) =>
            new(id, "Title", "Category", start, start.AddHours(2), canceled);

        private static IEnumerable<string> SelectIds(IEnumerable<TwitchSegment> segments, VacationWindow? vacation = null) =>
            SegmentSelector.Select(segments, vacation, Now, lookaheadDays: 14).Select(s => s.Id);

        [Fact]
        public void Upcoming_segment_inside_the_window_is_selected()
        {
            Assert.Equal(["a"], SelectIds([Segment("a", Now.AddDays(1))]));
        }

        [Fact]
        public void Canceled_segment_is_skipped()
        {
            Assert.Equal(["a"], SelectIds([Segment("a", Now.AddDays(1)), Segment("b", Now.AddDays(2), canceled: true)]));
        }

        [Fact]
        public void Segments_outside_the_window_are_skipped()
        {
            var segments = new[]
            {
                Segment("past", Now.AddHours(-1)),
                Segment("now", Now),
                Segment("cutoff", Now.AddDays(14)),
                Segment("after", Now.AddDays(14).AddSeconds(1)),
            };

            Assert.Equal(["cutoff"], SelectIds(segments));
        }

        [Fact]
        public void Segments_during_vacation_are_skipped()
        {
            var vacation = new VacationWindow(Now.AddDays(2), Now.AddDays(5));
            var segments = new[]
            {
                Segment("before", Now.AddDays(1)),
                Segment("vacation-start", Now.AddDays(2)),
                Segment("during", Now.AddDays(3)),
                Segment("vacation-end", Now.AddDays(5)),
                Segment("after", Now.AddDays(6)),
            };

            Assert.Equal(["before", "vacation-end", "after"], SelectIds(segments, vacation));
        }

        [Fact]
        public void Vacation_that_ends_mid_window_only_hides_its_own_period()
        {
            var vacation = new VacationWindow(Now.AddDays(-3), Now.AddDays(1));
            var segments = new[] { Segment("in-vacation", Now.AddHours(12)), Segment("back", Now.AddDays(2)) };

            Assert.Equal(["back"], SelectIds(segments, vacation));
        }

        [Fact]
        public void Vacation_after_the_window_changes_nothing()
        {
            var vacation = new VacationWindow(Now.AddDays(20), Now.AddDays(30));

            Assert.Equal(["a"], SelectIds([Segment("a", Now.AddDays(1))], vacation));
        }
    }

    public class SchedulePagingTests
    {
        private static readonly DateTimeOffset Cutoff = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

        private static TwitchSegment Segment(DateTimeOffset start) => new("id", "", "", start, null, false);

        [Fact]
        public void Next_page_is_needed_when_the_page_ends_before_the_cutoff_and_a_cursor_exists()
        {
            Assert.True(SchedulePaging.NeedsNextPage([Segment(Cutoff.AddDays(-5)), Segment(Cutoff)], "cursor", Cutoff));
        }

        [Fact]
        public void No_next_page_when_the_page_reaches_past_the_cutoff()
        {
            Assert.False(SchedulePaging.NeedsNextPage([Segment(Cutoff.AddDays(-5)), Segment(Cutoff.AddSeconds(1))], "cursor", Cutoff));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void No_next_page_without_a_cursor(string? cursor)
        {
            Assert.False(SchedulePaging.NeedsNextPage([Segment(Cutoff.AddDays(-5))], cursor, Cutoff));
        }

        [Fact]
        public void No_next_page_after_an_empty_page()
        {
            Assert.False(SchedulePaging.NeedsNextPage([], "cursor", Cutoff));
        }
    }
}
