using Xunit;

namespace GameAccountantCalendar.Tests;

public class ParsingTests
{
    private static readonly GameCalendar Calendar = SampleCalendars.CommonReckoning;

    [Fact]
    public void TheRoundTripFormatParsesBackToTheSameTick()
    {
        var original = Calendar.Date(1492, 7, 15, 6, 30, 7, 42, 3);

        Assert.True(Calendar.TryParse(original.ToString("o"), out var parsed));
        Assert.Equal(original, parsed);
        Assert.Equal(original.Ticks, parsed.Ticks);
    }

    [Fact]
    public void EveryDayOfAYearSurvivesTheRoundTrip()
    {
        for (int dayOfYear = 1; dayOfYear <= Calendar.DaysInYear; dayOfYear++)
        {
            var original = Calendar.DateFromDayOfYear(1492, dayOfYear, 13, 45, 2, 7, 8);
            Assert.True(Calendar.TryParse(original.ToString("o"), out var parsed));
            Assert.Equal(original, parsed);
        }
    }

    [Theory]
    [InlineData("1492-01-14", 1492, 1, 14, 0, 0, 0, 0, 0)]
    [InlineData("1492-01-14T06:30", 1492, 1, 14, 6, 30, 0, 0, 0)]
    [InlineData("1492-01-14 06:30", 1492, 1, 14, 6, 30, 0, 0, 0)]
    [InlineData("1492-01-14T06:30:07", 1492, 1, 14, 6, 30, 7, 0, 0)]
    [InlineData("1492-01-14T06:30:07:42", 1492, 1, 14, 6, 30, 7, 42, 0)]
    [InlineData("1492-01-14T06:30:07:42:3", 1492, 1, 14, 6, 30, 7, 42, 3)]
    [InlineData("1492-1-14T6:30", 1492, 1, 14, 6, 30, 0, 0, 0)]
    [InlineData("  1492-01-14  ", 1492, 1, 14, 0, 0, 0, 0, 0)]
    public void EverythingAfterTheDayIsOptional(
        string text, int year, int month, int day, int hour, int minute, int round, int turn, int tick)
    {
        Assert.True(Calendar.TryParse(text, out var parsed));
        Assert.Equal(Calendar.Date(year, month, day, hour, minute, round, turn, tick), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    [InlineData("1492")]
    [InlineData("1492-01")]
    [InlineData("1492-01-14-09")]
    [InlineData("1492-18-01")]          // no eighteenth month
    [InlineData("1492-03-02")]          // Firstplanting is one day long
    [InlineData("1492-01-31")]          // Frostwane is thirty days long
    [InlineData("1492-01-14T24:00")]    // hour is out of range
    [InlineData("1492-01-14T06:60")]    // minute is out of range
    [InlineData("1492-01-14T06:30:10")] // round is out of range
    [InlineData("1492-01-14T06:30:07:100")]
    [InlineData("1492-01-14T06:30:07:42:10")]
    [InlineData("1492-01-14T06:30:07:42:3:1")]
    [InlineData("0-01-01")]             // before the start year
    public void MalformedOrOutOfRangeTextIsRejected(string? text)
    {
        Assert.False(Calendar.TryParse(text, out var parsed));
        Assert.True(parsed.IsEmpty);
    }

    [Fact]
    public void ParsingIsBoundedByTheCalendarsOwnStartYear()
    {
        var calendar = new GameCalendarBuilder("Late reckoning")
            .WithStartYear(1370)
            .AddMonths(30, "One", "Two")
            .Build();

        Assert.False(calendar.TryParse("1369-01-01", out _));
        Assert.True(calendar.TryParse("1370-01-01", out var first));
        Assert.Equal(0UL, first.Ticks);
    }
}
