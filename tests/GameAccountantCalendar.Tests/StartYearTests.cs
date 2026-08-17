using Xunit;

namespace GameAccountantCalendar.Tests;

/// <summary>
/// The start year is what keeps a tick count unsigned: reckoning begins there, so nothing earlier
/// is representable and nothing later goes negative.
/// </summary>
public class StartYearTests
{
    private static GameCalendar WithStartYear(int year) => new GameCalendarBuilder("Reckoning")
        .WithStartYear(year)
        .WithWeekdays("A", "B", "C", "D", "E", "F", "G")
        .AddMonths(30, "One", "Two", "Three", "Four", "Five", "Six",
                       "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve")
        .Build();

    [Fact]
    public void ReckoningStartsAtTickZeroInTheStartYear()
    {
        var calendar = WithStartYear(1370);

        Assert.Equal(1370, calendar.StartYear);
        Assert.Equal(1370, calendar.Epoch.Year);
        Assert.Equal(0UL, calendar.Date(1370, 1, 1).Ticks);
        Assert.Equal((ulong)calendar.TicksPerYear, calendar.Date(1371, 1, 1).Ticks);
    }

    [Fact]
    public void YearsBeforeTheStartYearAreNotRepresentable()
    {
        var calendar = WithStartYear(1370);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = calendar.Date(1369, 12, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = calendar.Epoch.AddTicks(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = calendar.Epoch.AddDays(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = calendar.FromTicks(-1L));
    }

    [Fact]
    public void TheDefaultStartYearIsOne()
        => Assert.Equal(1, SampleCalendars.CommonReckoning.StartYear);

    [Fact]
    public void ANegativeStartYearShiftsTheWholeSpanBack()
    {
        var calendar = WithStartYear(-5000);

        Assert.Equal(0UL, calendar.Date(-5000, 1, 1).Ticks);
        Assert.Equal(-5000, calendar.Epoch.Year);
        Assert.Equal(-4999, calendar.Epoch.AddYears(1).Year);
        Assert.Equal("1 One -5000", calendar.Epoch.ToString("d"));
    }

    [Fact]
    public void EveryRepresentableTickFitsInBothIntegerTypes()
    {
        foreach (int startYear in new[] { 1, 1370, -5000 })
        {
            var calendar = WithStartYear(startYear);
            var last = calendar.Date(calendar.MaxYear, 12, 30, 23, 59, 9, 99, 9);

            Assert.Equal(calendar.MaxTicks, last.Ticks);
            Assert.True(calendar.MaxTicks <= (ulong)long.MaxValue);
            Assert.Equal(last, calendar.FromTicks(last.ToInt64()));
        }
    }

    [Fact]
    public void MovingPastTheLastReckonedYearIsRefused()
    {
        var calendar = WithStartYear(1370);
        var last = calendar.FromTicks(calendar.MaxTicks);

        Assert.Equal(calendar.MaxYear, last.Year);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = last.AddTicks(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = calendar.FromTicks(calendar.MaxTicks + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = calendar.Date(calendar.MaxYear + 1, 1, 1));
    }

    [Fact]
    public void TheWeekCycleIsAnchoredToTheStartYear()
    {
        var calendar = WithStartYear(1370);

        Assert.Equal("A", calendar.Date(1370, 1, 1).Weekday?.Name);
        Assert.Equal("B", calendar.Date(1370, 1, 2).Weekday?.Name);
    }
}
