using Xunit;

namespace GameAccountantCalendar.Tests;

public class CalendarDefinitionTests
{
    [Fact]
    public void SampleCalendarTilesTheYear()
    {
        var calendar = SampleCalendars.CommonReckoning;

        Assert.Equal(365, calendar.DaysInYear);
        Assert.Equal(12, calendar.MonthCount);
        Assert.Equal(17, calendar.Months.Count);
        Assert.Equal(7, calendar.WeekLength);
        Assert.Equal(1440, calendar.MinutesInDay);
        Assert.Equal(365L * 1440 * GameCalendar.TicksPerMinute, calendar.TicksPerYear);
    }

    [Fact]
    public void BuilderChainsMonthStartDays()
    {
        var calendar = SampleCalendars.CommonReckoning;

        Assert.Equal((1, 30), (calendar.Month(1).StartDay, calendar.Month(1).EndDay));
        Assert.Equal((61, 61), (calendar.Month(3).StartDay, calendar.Month(3).EndDay));
        Assert.Equal((62, 91), (calendar.Month(4).StartDay, calendar.Month(4).EndDay));
        Assert.Equal((336, 365), (calendar.Month(17).StartDay, calendar.Month(17).EndDay));
    }

    [Fact]
    public void MonthsWithAGapAreRejected()
    {
        var months = new[]
        {
            new CalendarMonth(1, "First", 1, 30),
            new CalendarMonth(2, "Second", 32, 61),
        };

        var error = Assert.Throws<CalendarValidationException>(() => _ = new GameCalendar("Gappy", months));
        Assert.Contains("no gaps or overlaps", error.Message);
    }

    [Fact]
    public void MonthsThatOverlapAreRejected()
    {
        var months = new[]
        {
            new CalendarMonth(1, "First", 1, 30),
            new CalendarMonth(2, "Second", 30, 59),
        };

        Assert.Throws<CalendarValidationException>(() => _ = new GameCalendar("Overlapping", months));
    }

    [Fact]
    public void MonthNumbersMustBeContiguous()
    {
        var months = new[]
        {
            new CalendarMonth(1, "First", 1, 30),
            new CalendarMonth(3, "Third", 31, 60),
        };

        var error = Assert.Throws<CalendarValidationException>(() => _ = new GameCalendar("Skipped", months));
        Assert.Contains("no gaps or duplicates", error.Message);
    }

    [Fact]
    public void AYearOfNothingButFestivalsIsRejected()
    {
        var months = new[] { new CalendarMonth(1, "Endless Party", 1, 5, isHoliday: true) };

        Assert.Throws<CalendarValidationException>(() => _ = new GameCalendar("Party", months));
    }

    [Fact]
    public void EpochWeekdayMustBeInsideTheWeek()
    {
        var builder = new GameCalendarBuilder("Off the end")
            .WithWeekdays("One", "Two")
            .AddMonth("Only", 10)
            .WithEpochWeekday(5);

        Assert.Throws<CalendarValidationException>(() => _ = builder.Build());
    }

    [Fact]
    public void PinnedStartingWeekdayMustBeInsideTheWeek()
    {
        var builder = new GameCalendarBuilder("Bad pin")
            .WithWeekdays("One", "Two")
            .AddMonth("Only", 10, startingWeekday: 9);

        Assert.Throws<CalendarValidationException>(() => _ = builder.Build());
    }

    [Fact]
    public void ACalendarNeedsAtLeastOneMonth()
        => Assert.Throws<CalendarValidationException>(() => _ = new GameCalendarBuilder("Empty").Build());

    [Fact]
    public void FindsMonthsByNameAndAltName()
    {
        var calendar = SampleCalendars.CommonReckoning;

        Assert.Equal("Frostwane", calendar.FindMonth("frostwane")?.Name);
        Assert.Equal("Frostwane", calendar.FindMonth("Deep Winter")?.Name);
        Assert.Null(calendar.FindMonth("Nonesuch"));
    }

    [Fact]
    public void DescribesSpansInTheCalendarsOwnUnits()
    {
        var calendar = SampleCalendars.CommonReckoning;

        Assert.Equal("0 ticks", calendar.Describe(GameTimeSpan.Zero));
        Assert.Equal("1 minute", calendar.Describe(calendar.Minutes(1)));
        Assert.Equal("2 hours", calendar.Describe(calendar.Hours(2)));
        Assert.Equal("1 day, 2 hours, 5 minutes", calendar.Describe(calendar.Days(1) + calendar.Hours(2) + calendar.Minutes(5)));
        Assert.Equal("-3 days", calendar.Describe(calendar.Days(-3)));
        Assert.Equal("7 days", calendar.Describe(calendar.Weeks(1)));
    }

    [Fact]
    public void AnHourIsWhateverTheCalendarSaysItIs()
    {
        var calendar = new GameCalendarBuilder("Ten-hour world")
            .WithDay(hours: 10, minutesPerHour: 100)
            .AddMonth("Only", 20)
            .Build();

        Assert.Equal(1000, calendar.MinutesInDay);
        Assert.Equal("1 day, 2 hours, 3 minutes", calendar.Describe(calendar.Days(1) + calendar.Hours(2) + calendar.Minutes(3)));
        Assert.Equal(9, calendar.Date(1, 1, 1, hour: 9, minute: 99).Hour);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = calendar.Date(1, 1, 1, hour: 10));
    }
}
