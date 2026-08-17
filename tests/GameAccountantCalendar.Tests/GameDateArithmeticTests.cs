using Xunit;

namespace GameAccountantCalendar.Tests;

public class GameDateArithmeticTests
{
    private static readonly GameCalendar Calendar = SampleCalendars.CommonReckoning;

    [Fact]
    public void TheEpochIsTheFirstInstantOfTheStartYear()
    {
        var epoch = Calendar.Epoch;

        Assert.Equal(0UL, epoch.Ticks);
        Assert.Equal(Calendar.StartYear, epoch.Year);
        Assert.Equal(1, epoch.MonthNumber);
        Assert.Equal(1, epoch.Day);
        Assert.Equal(1, epoch.DayOfYear);
        Assert.Equal(0, epoch.Hour);
        Assert.Equal(0, epoch.Minute);
        Assert.Equal(0, epoch.Round);
        Assert.Equal(0, epoch.Turn);
        Assert.Equal(0, epoch.Tick);
    }

    [Fact]
    public void TicksCountUpInTheCalendarsOwnUnits()
    {
        Assert.Equal(14_400_000L, Calendar.TicksPerDay);
        Assert.Equal(600_000L, Calendar.TicksPerHour);
        Assert.Equal(365L * 14_400_000, Calendar.TicksPerYear);

        Assert.Equal(14_400_000UL, Calendar.Date(1, 1, 2).Ticks);
        Assert.Equal(365UL * 14_400_000, Calendar.Date(2, 1, 1).Ticks);
    }

    [Theory]
    [InlineData(1, 1, 1, 0, 0, 0, 0, 0)]
    [InlineData(1, 1, 30, 23, 59, 9, 99, 9)]
    [InlineData(1492, 7, 15, 6, 30, 7, 42, 3)]
    [InlineData(1492, 3, 1, 0, 0, 0, 0, 1)]
    [InlineData(9999, 17, 30, 12, 0, 5, 0, 0)]
    public void TicksRoundTripThroughEveryField(
        int year, int month, int day, int hour, int minute, int round, int turn, int tick)
    {
        var date = Calendar.Date(year, month, day, hour, minute, round, turn, tick);
        var again = Calendar.FromTicks(date.Ticks);

        Assert.Equal(year, again.Year);
        Assert.Equal(month, again.MonthNumber);
        Assert.Equal(day, again.Day);
        Assert.Equal(hour, again.Hour);
        Assert.Equal(minute, again.Minute);
        Assert.Equal(round, again.Round);
        Assert.Equal(turn, again.Turn);
        Assert.Equal(tick, again.Tick);
        Assert.Equal(date, again);
    }

    [Fact]
    public void EveryDayOfAYearRoundTrips()
    {
        for (int dayOfYear = 1; dayOfYear <= Calendar.DaysInYear; dayOfYear++)
        {
            var date = Calendar.DateFromDayOfYear(1492, dayOfYear);
            Assert.Equal(dayOfYear, date.DayOfYear);
            Assert.Equal(date, Calendar.Date(date.Year, date.MonthNumber, date.Day));
        }
    }

    [Fact]
    public void TicksSurviveARoundTripThroughABigintColumn()
    {
        var date = Calendar.Date(1492, 7, 15, 6, 30, 7, 42, 3);
        long stored = date.ToInt64();

        Assert.True(stored > 0);
        Assert.Equal(date, Calendar.FromTicks(stored));
        Assert.True(Calendar.MaxTicks <= (ulong)long.MaxValue);
    }

    [Fact]
    public void AddingDaysCrossesFestivalsLikeAnyOtherDay()
    {
        var lastOfThawtide = Calendar.Date(1492, 2, 30);
        var festival = lastOfThawtide.AddDays(1);
        var firstOfSeedfall = lastOfThawtide.AddDays(2);

        Assert.Equal("Firstplanting", festival.Month.Name);
        Assert.True(festival.IsHoliday);
        Assert.Equal("Seedfall", firstOfSeedfall.Month.Name);
        Assert.Equal(1, firstOfSeedfall.Day);
    }

    [Fact]
    public void AddingDaysRollsOverTheYearInBothDirections()
    {
        var lastDay = Calendar.Date(1492, 17, 30);
        var newYear = lastDay.AddDays(1);

        Assert.Equal(1493, newYear.Year);
        Assert.Equal(1, newYear.DayOfYear);
        Assert.Equal(lastDay, newYear.AddDays(-1));
    }

    [Fact]
    public void AddingTimeCarriesIntoTheNextDay()
    {
        var evening = Calendar.Date(1492, 1, 1, 23, 30);
        var later = evening.AddMinutes(45);

        Assert.Equal(2, later.Day);
        Assert.Equal(0, later.Hour);
        Assert.Equal(15, later.Minute);
        Assert.Equal(evening, later.AddMinutes(-45));
    }

    [Fact]
    public void AddYearsKeepsTheSameDayOfTheYear()
    {
        var date = Calendar.Date(1492, 7, 15, 6, 30, 4, 20, 1);
        var later = date.AddYears(8);

        Assert.Equal(1500, later.Year);
        Assert.Equal(date.DayOfYear, later.DayOfYear);
        Assert.Equal(date.MonthNumber, later.MonthNumber);
        Assert.Equal(date.Day, later.Day);
        Assert.Equal((6, 30, 4, 20, 1), (later.Hour, later.Minute, later.Round, later.Turn, later.Tick));
        Assert.Equal(date, later.AddYears(-8));
    }

    [Fact]
    public void AddMonthsStepsOverFestivals()
    {
        var thawtide = Calendar.Date(1492, 2, 15);

        Assert.Equal("Seedfall", thawtide.AddMonths(1).Month.Name);
        Assert.Equal(15, thawtide.AddMonths(1).Day);
    }

    [Fact]
    public void AddMonthsWrapsAcrossTheYear()
    {
        var frostwane = Calendar.Date(1492, 1, 15);

        Assert.Equal(1493, frostwane.AddMonths(12).Year);
        Assert.Equal("Frostwane", frostwane.AddMonths(12).Month.Name);
        Assert.Equal(1491, frostwane.AddMonths(-1).Year);
        Assert.Equal("Deepnight", frostwane.AddMonths(-1).Month.Name);
    }

    [Fact]
    public void AddMonthsFromAFestivalMovesToTheNextOrdinaryMonth()
    {
        var festival = Calendar.Date(1492, 3, 1, 9, 15);
        var stepped = festival.AddMonths(0);

        Assert.Equal("Seedfall", stepped.Month.Name);
        Assert.Equal(1, stepped.Day);
        Assert.Equal(9, stepped.Hour);
        Assert.Equal(15, stepped.Minute);
        Assert.Equal("Bloomrise", festival.AddMonths(1).Month.Name);
    }

    [Fact]
    public void AddMonthsClampsToAShorterMonth()
    {
        var calendar = new GameCalendarBuilder("Uneven")
            .WithWeekdays("A", "B", "C")
            .AddMonth("Long", 31)
            .AddMonth("Short", 28)
            .Build();

        var end = calendar.Date(10, 1, 31);
        var clamped = end.AddMonths(1);

        Assert.Equal("Short", clamped.Month.Name);
        Assert.Equal(28, clamped.Day);

        // Clamping is lossy, so stepping back does not return to the 31st.
        Assert.Equal(28, clamped.AddMonths(-1).Day);
    }

    [Fact]
    public void SubtractingTwoDatesGivesASpan()
    {
        var start = Calendar.Date(1492, 1, 1, 0, 0);
        var end = Calendar.Date(1492, 1, 3, 6, 30);

        Assert.Equal(((2L * 1440) + (6 * 60) + 30) * GameCalendar.TicksPerMinute, (end - start).TotalTicks);
        Assert.Equal("2 days, 6 hours, 30 minutes", Calendar.Describe(end - start));
        Assert.True((start - end).IsNegative);
    }

    [Fact]
    public void DaysUntilCountsMidnightsCrossed()
    {
        var lateNight = Calendar.Date(1492, 1, 1, 23, 59);
        var earlyMorning = Calendar.Date(1492, 1, 2, 0, 1);

        Assert.Equal(1, lateNight.DaysUntil(earlyMorning));
        Assert.Equal(-1, earlyMorning.DaysUntil(lateNight));
        Assert.Equal(0, lateNight.DaysUntil(Calendar.Date(1492, 1, 1, 0, 0)));
    }

    [Fact]
    public void BoundariesSnapToTheStartOfTheMinuteDayMonthAndYear()
    {
        var date = Calendar.Date(1492, 4, 17, 13, 45, 6, 12, 8);

        Assert.Equal(Calendar.Date(1492, 4, 17, 13, 45), date.StartOfMinute);
        Assert.Equal(Calendar.Date(1492, 4, 17), date.StartOfDay);
        Assert.Equal(Calendar.Date(1492, 4, 17, 23, 59, 9, 99, 9), date.EndOfDay);
        Assert.Equal(Calendar.Date(1492, 4, 1), date.StartOfMonth);
        Assert.Equal(Calendar.Date(1492, 1, 1), date.StartOfYear);
        Assert.Equal(Calendar.Date(1492, 4, 17, 8, 0), date.WithTime(8, 0));
    }

    [Fact]
    public void SpansAddSubtractAndScale()
    {
        var day = Calendar.Days(1);

        Assert.Equal(Calendar.Days(3), day * 3);
        Assert.Equal(Calendar.Days(3), 3 * day);
        Assert.Equal(Calendar.Hours(25), day + Calendar.Hours(1));
        Assert.Equal(Calendar.Days(-1), -day);
        Assert.True(Calendar.Hours(1) < day);
        Assert.Equal(Calendar.Days(1), Calendar.Days(-1).Absolute);
        Assert.Equal(1440, day.TotalMinutes);
    }

    [Fact]
    public void DatesOnDifferentCalendarsCannotBeCompared()
    {
        var other = new GameCalendarBuilder("Elsewhere").AddMonth("Only", 10).Build();

        Assert.Throws<ArgumentException>(() => _ = Calendar.Epoch.CompareTo(other.Epoch));
        Assert.Throws<ArgumentException>(() => _ = Calendar.Epoch - other.Epoch);
        Assert.False(Calendar.Epoch == other.Epoch);
    }

    [Fact]
    public void AnEmptyDateSaysSoRatherThanPretendingToBeTheEpoch()
    {
        GameDate empty = default;

        Assert.True(empty.IsEmpty);
        Assert.Equal(string.Empty, empty.ToString());
        Assert.Throws<InvalidOperationException>(() => _ = empty.Year);
    }

    [Fact]
    public void DatesSortByTick()
    {
        var dates = new[]
        {
            Calendar.Date(1492, 5, 1),
            Calendar.Date(1490, 1, 1),
            Calendar.Date(1492, 1, 1),
        };

        Array.Sort(dates);

        Assert.Equal(new[] { 1490, 1492, 1492 }, dates.Select(d => d.Year));
        Assert.Equal(new[] { 1, 1, 5 }, dates.Select(d => d.MonthNumber));
    }
}
