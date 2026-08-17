using System.Globalization;
using Xunit;

namespace GameAccountantCalendar.Tests;

public class FormattingTests
{
    private static readonly GameCalendar Calendar = SampleCalendars.CommonReckoning;

    private static GameDate Sample => Calendar.Date(1999, 1, 14, 6, 30);

    private static GameDate MidCombat => Calendar.Date(1999, 1, 14, 6, 30, 7, 42, 3);

    [Theory]
    [InlineData("d", "14 Frostwane 1999")]
    [InlineData("D", "Moonsday, 14 Frostwane 1999")]
    [InlineData("t", "6:30")]
    [InlineData("T", "6:30_00:00:00")]
    [InlineData("f", "14 Frostwane 1999 6:30")]
    [InlineData("F", "Moonsday, 14 Frostwane 1999 6:30")]
    [InlineData("y", "Frostwane 1999")]
    [InlineData("n", "1999-01-14")]
    [InlineData("o", "1999-01-14T06:30_00:00:00")]
    public void StandardFormats(string format, string expected)
        => Assert.Equal(expected, Sample.ToString(format));

    [Fact]
    public void TheRoundTripFormatCarriesEveryUnit()
    {
        Assert.Equal("1999-01-14T06:30_07:42:03", MidCombat.ToString("o"));
        Assert.Equal("6:30_07:42:03", MidCombat.ToString("T"));
    }

    [Fact]
    public void TheDefaultFormatIsTheLongOne()
    {
        Assert.Equal("Moonsday, 14 Frostwane 1999 6:30", Sample.ToString());
        Assert.Equal(Sample.ToString("F"), Sample.ToString(null));
    }

    [Fact]
    public void ASingleDayFestivalDropsTheDayNumber()
    {
        var festival = Calendar.Date(1999, 3, 1, 12, 0);

        Assert.Equal("Firstplanting 1999", festival.ToString("d"));
        Assert.Equal("Firstplanting 1999", festival.ToString("D"));
        Assert.Equal("Firstplanting 1999 12:00", festival.ToString("F"));
    }

    [Fact]
    public void ADayWithNoWeekdayDropsItAndItsComma()
    {
        var festival = Calendar.Date(1999, 3, 1);

        Assert.Null(festival.Weekday);
        Assert.DoesNotContain(",", festival.ToString("D"));
    }

    [Theory]
    [InlineData("dddd", "Moonsday")]
    [InlineData("ddd", "Moo")]
    [InlineData("dd", "14")]
    [InlineData("%d", "14")]
    [InlineData("MMMM", "Frostwane")]
    [InlineData("MMM", "Deep Winter")]
    [InlineData("MM", "01")]
    [InlineData("%M", "1")]
    [InlineData("yyyy", "1999")]
    [InlineData("yyyyyy", "001999")]
    [InlineData("%y", "1999")]
    [InlineData("DDD", "014")]
    [InlineData("HH:mm", "06:30")]
    [InlineData("H:mm", "6:30")]
    [InlineData("dddd, d MMMM yyyy", "Moonsday, 14 Frostwane 1999")]
    public void CustomFormats(string format, string expected)
        => Assert.Equal(expected, Sample.ToString(format));

    [Theory]
    [InlineData("RR", "07")]
    [InlineData("%R", "7")]
    [InlineData("TT", "42")]
    [InlineData("%T", "42")]
    [InlineData("%K", "3")]
    [InlineData("KK", "03")]
    [InlineData("HH:mm_RR:TT:KK", "06:30_07:42:03")]
    [InlineData("'round' R', turn' TT', tick' K", "round 7, turn 42, tick 3")]
    public void CombatSpecifiers(string format, string expected)
        => Assert.Equal(expected, MidCombat.ToString(format));

    [Fact]
    public void TheUnderscoreDividesTheWallClockFromTheCombatClock()
    {
        var halves = MidCombat.ToString("o").Split('_');

        Assert.Equal(2, halves.Length);
        Assert.EndsWith("06:30", halves[0]);
        Assert.Equal("07:42:03", halves[1]);
    }

    [Fact]
    public void TheRoundTripPatternIsTheOneTheStandardFormatUses()
        => Assert.Equal(MidCombat.ToString("o"), MidCombat.ToString(GameDateFormatter.RoundTripPattern));

    [Fact]
    public void ShortMonthNameFallsBackToAnAbbreviation()
    {
        // Thawtide has no alternate name, so MMM truncates instead.
        Assert.Equal("Tha", Calendar.Date(1999, 2, 1).ToString("MMM"));
    }

    [Fact]
    public void LiteralsAndEscapesAreCopiedThrough()
    {
        Assert.Equal("day 14 of Frostwane", Sample.ToString("'day' d 'of' MMMM"));
        Assert.Equal("day 14 of Frostwane", Sample.ToString("\"day\" d \"of\" MMMM"));
        Assert.Equal("d14", Sample.ToString("\\d%d"));
        Assert.Equal("14/1/1999", Sample.ToString("d/M/y"));
    }

    [Fact]
    public void MalformedFormatsAreReported()
    {
        Assert.Throws<FormatException>(() => _ = Sample.ToString("d 'unclosed"));
        Assert.Throws<FormatException>(() => _ = Sample.ToString("dd\\"));
        Assert.Throws<FormatException>(() => _ = Sample.ToString("dd%"));
    }

    [Fact]
    public void YearsBeforeTheCommonEraKeepTheirSign()
    {
        var calendar = new GameCalendarBuilder("Ancient")
            .WithStartYear(-500)
            .AddMonths(30, "One", "Two")
            .Build();

        Assert.Equal("3 Two -44", calendar.Date(-44, 2, 3).ToString("d"));
        Assert.Equal("-0044", calendar.Date(-44, 2, 3).ToString("yyyy"));
    }

    [Fact]
    public void FormattingUsesTheCultureItIsGiven()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("14 Frostwane 1999 6:30", Sample.ToString("f", german));
    }

    [Fact]
    public void PaddingFollowsTheCalendarsOwnUnits()
    {
        var calendar = new GameCalendarBuilder("Wide")
            .WithDay(hours: 5, minutesPerHour: 500)
            .AddMonth("Only", 1200)
            .Build();

        var date = calendar.Date(1, 1, 1000, 4, 7);

        Assert.Equal("4:007", date.ToString("t"));
        Assert.Equal("1000", date.ToString("DDD"));
    }
}
