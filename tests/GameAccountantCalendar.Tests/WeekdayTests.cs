using Xunit;

namespace GameAccountantCalendar.Tests;

public class WeekdayTests
{
    private static readonly GameCalendar Calendar = SampleCalendars.CommonReckoning;

    [Fact]
    public void TheEpochFallsOnTheEpochWeekday()
        => Assert.Equal("Sunsday", Calendar.Epoch.Weekday?.Name);

    [Fact]
    public void TheWeekRunsInOrderThroughAMonth()
    {
        var names = Enumerable.Range(1, 8)
            .Select(day => Calendar.Date(1, 1, day).Weekday!.Name)
            .ToArray();

        Assert.Equal(
            new[] { "Sunsday", "Moonsday", "Fireday", "Watersday", "Woodsday", "Ironsday", "Starsday", "Sunsday" },
            names);
    }

    [Fact]
    public void FestivalsHaveNoWeekdayAndDoNotAdvanceTheWeek()
    {
        var lastOfThawtide = Calendar.Date(1, 2, 30);
        var festival = Calendar.Date(1, 3, 1);
        var firstOfSeedfall = Calendar.Date(1, 4, 1);

        Assert.Null(festival.Weekday);
        Assert.Equal("Watersday", lastOfThawtide.Weekday?.Name);
        Assert.Equal("Woodsday", firstOfSeedfall.Weekday?.Name);
    }

    [Fact]
    public void FestivalsCanBeBroughtIntoTheWeekInstead()
    {
        var calendar = new GameCalendarBuilder("Continuous")
            .WithWeekdays("A", "B", "C")
            .WithHolidaysInWeekCycle()
            .AddMonth("First", 3)
            .AddFestival("Pause")
            .AddMonth("Second", 3)
            .Build();

        Assert.Equal("A", calendar.Date(1, 2, 1).Weekday?.Name);
        Assert.Equal("B", calendar.Date(1, 3, 1).Weekday?.Name);
    }

    [Fact]
    public void TheWeekCarriesAcrossTheYearBoundary()
    {
        // 360 days sit inside the week cycle, and 360 % 7 == 3, so each year starts three days later.
        Assert.Equal("Sunsday", Calendar.Date(1, 1, 1).Weekday?.Name);
        Assert.Equal("Watersday", Calendar.Date(2, 1, 1).Weekday?.Name);
        Assert.Equal("Starsday", Calendar.Date(3, 1, 1).Weekday?.Name);
    }

    [Fact]
    public void EveryConsecutiveDayAdvancesTheWeekByExactlyOne()
    {
        var date = Calendar.Date(1996, 1, 1);
        var end = Calendar.Date(2000, 1, 1);
        int? previous = null;

        while (date < end)
        {
            var weekday = date.Weekday;
            if (weekday is not null)
            {
                if (previous is int last)
                {
                    int expected = (last % Calendar.WeekLength) + 1;
                    Assert.Equal(expected, weekday.Number);
                }

                previous = weekday.Number;
            }

            date = date.AddDays(1);
        }
    }

    [Fact]
    public void AMonthCanPinItsOwnStartingWeekday()
    {
        var calendar = new GameCalendarBuilder("Pinned")
            .WithStartYear(-10)
            .WithWeekdays("A", "B", "C", "D")
            .AddMonth("Drifting", 5)
            .AddMonth("Anchored", 4, startingWeekday: 1)
            .AddMonth("Trailing", 4)
            .Build();

        // The pin holds in every year, however the months before it fall.
        foreach (int year in new[] { 1, 2, 7, -3 })
        {
            Assert.Equal("A", calendar.Date(year, 2, 1).Weekday?.Name);
            Assert.Equal("D", calendar.Date(year, 2, 4).Weekday?.Name);
            Assert.Equal("A", calendar.Date(year, 3, 1).Weekday?.Name);
        }
    }

    [Fact]
    public void ACalendarWithNoWeekHasNoWeekdays()
    {
        var calendar = new GameCalendarBuilder("Weekless")
            .AddMonth("Only", 10)
            .Build();

        Assert.Equal(0, calendar.WeekLength);
        Assert.Null(calendar.Date(1, 1, 5).Weekday);
        Assert.Throws<InvalidOperationException>(() => _ = calendar.Weeks(1));
    }

    [Fact]
    public void WeekdaysCanBeLookedUpByName()
    {
        Assert.Equal(3, Calendar.FindWeekday("fireday")?.Number);
        Assert.Null(Calendar.FindWeekday("Blursday"));
    }
}
