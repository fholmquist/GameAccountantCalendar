using Xunit;

namespace GameAccountantCalendar.Tests;

public class ParsingTests
{
    private static readonly GameCalendar Calendar = SampleCalendars.CommonReckoning;

    [Fact]
    public void TheRoundTripFormatParsesBackToTheSameTick()
    {
        var original = Calendar.Date(1999, 7, 15, 6, 30, 7, 42, 3);

        Assert.True(Calendar.TryParse(original.ToString("o"), out var parsed));
        Assert.Equal(original, parsed);
        Assert.Equal(original.Ticks, parsed.Ticks);
    }

    [Fact]
    public void EveryDayOfAYearSurvivesTheRoundTrip()
    {
        for (int dayOfYear = 1; dayOfYear <= Calendar.DaysInYear; dayOfYear++)
        {
            var original = Calendar.DateFromDayOfYear(1999, dayOfYear, 13, 45, 2, 7, 8);
            Assert.True(Calendar.TryParse(original.ToString("o"), out var parsed));
            Assert.Equal(original, parsed);
        }
    }

    [Theory]
    [InlineData("1999-01-14", 1999, 1, 14, 0, 0, 0, 0, 0)]
    [InlineData("1999-01-14T06:30", 1999, 1, 14, 6, 30, 0, 0, 0)]
    [InlineData("1999-01-14 06:30", 1999, 1, 14, 6, 30, 0, 0, 0)]
    [InlineData("1999-01-14T06:30_07", 1999, 1, 14, 6, 30, 7, 0, 0)]
    [InlineData("1999-01-14T06:30_07:42", 1999, 1, 14, 6, 30, 7, 42, 0)]
    [InlineData("1999-01-14T06:30_07:42:03", 1999, 1, 14, 6, 30, 7, 42, 3)]
    [InlineData("1999-1-14T6:30_7:4:3", 1999, 1, 14, 6, 30, 7, 4, 3)]
    [InlineData("  1999-01-14  ", 1999, 1, 14, 0, 0, 0, 0, 0)]
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
    [InlineData("1999")]
    [InlineData("1999-01")]
    [InlineData("1999-01-14-09")]
    [InlineData("1999-18-01")]                   // no eighteenth month
    [InlineData("1999-03-02")]                   // Firstplanting is one day long
    [InlineData("1999-01-31")]                   // Frostwane is thirty days long
    [InlineData("1999-01-14T24:00")]             // hour is out of range
    [InlineData("1999-01-14T06:60")]             // minute is out of range
    [InlineData("1999-01-14T06:30_10")]          // round is out of range
    [InlineData("1999-01-14T06:30_07:100")]      // turn is out of range
    [InlineData("1999-01-14T06:30_07:42:100")]   // tick is out of range
    [InlineData("1999-01-14T06:30_07:42:03:1")]  // one combat field too many
    [InlineData("1999-01-14T06:30_")]            // the underscore promises a combat clock
    [InlineData("1999-01-14T06:30:07:42:03")]    // the old colon-only shape is no longer valid
    [InlineData("0-01-01")]                      // before the start year
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
