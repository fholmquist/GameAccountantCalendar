using Xunit;

namespace GameAccountantCalendar.Tests;

/// <summary>
/// Renaming re-skins a calendar without disturbing the year underneath it, so a world can borrow
/// another's shape and give it its own words.
/// </summary>
public class RenamingTests
{
    private static readonly GameCalendar Calendar = SampleCalendars.CommonReckoning;

    private static readonly string[] TwelveMonths =
    [
        "Nivose", "Pluviose", "Ventose", "Germinal", "Floreal", "Prairial",
        "Messidor", "Thermidor", "Fructidor", "Vendemiaire", "Brumaire", "Frimaire",
    ];

    private static readonly string[] SevenDays =
    [
        "Primus", "Secundus", "Tertius", "Quartus", "Quintus", "Sextus", "Septimus",
    ];

    [Fact]
    public void TwelveNamesRenameTheOrdinaryMonthsAndLeaveTheFestivals()
    {
        var renamed = Calendar.WithMonthNames(TwelveMonths);

        Assert.Equal("Nivose", renamed.Month(1).Name);
        Assert.Equal("Pluviose", renamed.Month(2).Name);
        Assert.Equal("Firstplanting", renamed.Month(3).Name);   // festival, untouched
        Assert.Equal("Ventose", renamed.Month(4).Name);
        Assert.Equal("Frimaire", renamed.Month(17).Name);

        Assert.Equal(
            new[] { "Firstplanting", "Greentide", "Midyear", "Reaping", "Yearsend" },
            renamed.Months.Where(m => m.IsHoliday).Select(m => m.Name));
    }

    [Fact]
    public void SeventeenNamesRenameTheFestivalsToo()
    {
        var names = Enumerable.Range(1, Calendar.Months.Count).Select(i => $"M{i}").ToArray();
        var renamed = Calendar.WithMonthNames(names);

        Assert.Equal("M1", renamed.Month(1).Name);
        Assert.Equal("M3", renamed.Month(3).Name);
        Assert.True(renamed.Month(3).IsHoliday);
        Assert.Equal("M17", renamed.Month(17).Name);
        Assert.All(renamed.Months, m => Assert.StartsWith("M", m.Name));
    }

    [Fact]
    public void RenamingLeavesTheYearsShapeExactlyAsItWas()
    {
        var renamed = Calendar.WithMonthNames(TwelveMonths).WithWeekdayNames(SevenDays);

        Assert.Equal(Calendar.DaysInYear, renamed.DaysInYear);
        Assert.Equal(Calendar.MonthCount, renamed.MonthCount);
        Assert.Equal(Calendar.Months.Count, renamed.Months.Count);
        Assert.Equal(Calendar.WeekLength, renamed.WeekLength);
        Assert.Equal(Calendar.StartYear, renamed.StartYear);
        Assert.Equal(Calendar.MaxYear, renamed.MaxYear);
        Assert.Equal(Calendar.TicksPerYear, renamed.TicksPerYear);
        Assert.Equal(Calendar.Id, renamed.Id);
        Assert.Equal(Calendar.Label, renamed.Label);
        Assert.Equal(Calendar.Description, renamed.Description);
        Assert.Equal(Calendar.EpochWeekday, renamed.EpochWeekday);

        Assert.Equal(
            Calendar.Months.Select(m => (m.Number, m.StartDay, m.EndDay, m.IsHoliday, m.AltName)),
            renamed.Months.Select(m => (m.Number, m.StartDay, m.EndDay, m.IsHoliday, m.AltName)));
    }

    [Fact]
    public void ATickMeansTheSameMomentOnBothCalendars()
    {
        var renamed = Calendar.WithMonthNames(TwelveMonths).WithWeekdayNames(SevenDays);

        var before = Calendar.Date(1999, 1, 14, 6, 30, 7, 42, 3);
        var after = renamed.FromTicks(before.Ticks);

        Assert.Equal(before.Ticks, after.Ticks);
        Assert.Equal(before.Year, after.Year);
        Assert.Equal(before.MonthNumber, after.MonthNumber);
        Assert.Equal(before.Day, after.Day);
        Assert.Equal(before.Weekday!.Number, after.Weekday!.Number);

        // Only the words change.
        Assert.Equal("Frostwane", before.Month.Name);
        Assert.Equal("Nivose", after.Month.Name);
        Assert.Equal("Moonsday", before.Weekday.Name);
        Assert.Equal("Secundus", after.Weekday.Name);
    }

    [Fact]
    public void FestivalsStillSitOutsideTheWeekAfterRenaming()
    {
        var renamed = Calendar.WithMonthNames(TwelveMonths).WithWeekdayNames(SevenDays);

        Assert.Null(renamed.Date(1999, 3, 1).Weekday);
        Assert.Equal("Firstplanting 1999", renamed.Date(1999, 3, 1).ToString("d"));
    }

    [Fact]
    public void WeekdaysKeepTheirPlaceInTheCycle()
    {
        var renamed = Calendar.WithWeekdayNames(SevenDays);

        for (int day = 1; day <= 30; day++)
        {
            Assert.Equal(
                Calendar.Date(1999, 1, day).Weekday!.Number,
                renamed.Date(1999, 1, day).Weekday!.Number);
        }

        Assert.Equal("Primus", renamed.Date(1, 1, 1).Weekday?.Name);
    }

    [Fact]
    public void RenamedMonthsAreFindableByTheirNewNames()
    {
        var renamed = Calendar.WithMonthNames(TwelveMonths);

        Assert.Equal(1, renamed.FindMonth("nivose")?.Number);
        Assert.Null(renamed.FindMonth("Frostwane"));

        // The alternate name survives the rename and still resolves.
        Assert.Equal("Nivose", renamed.FindMonth("Deep Winter")?.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(16)]
    [InlineData(18)]
    public void TheWrongNumberOfMonthNamesIsRefused(int count)
    {
        var names = Enumerable.Range(1, count).Select(i => $"M{i}").ToArray();

        var error = Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithMonthNames(names));
        Assert.Contains("12", error.Message);
        Assert.Contains("17", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(8)]
    public void TheWrongNumberOfWeekdayNamesIsRefused(int count)
    {
        var names = Enumerable.Range(1, count).Select(i => $"D{i}").ToArray();

        var error = Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithWeekdayNames(names));
        Assert.Contains("7", error.Message);
    }

    [Fact]
    public void BlankNamesAreRefused()
    {
        var months = (string[])TwelveMonths.Clone();
        months[3] = "   ";

        Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithMonthNames(months));

        var days = (string[])SevenDays.Clone();
        days[0] = "";

        Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithWeekdayNames(days));
    }

    [Fact]
    public void ACalendarWithNoFestivalsTakesOneCountForBothReadings()
    {
        var plain = new GameCalendarBuilder("Plain")
            .WithWeekdays("A", "B", "C")
            .AddMonths(30, "One", "Two", "Three")
            .Build();

        var renamed = plain.WithMonthNames("Uno", "Dos", "Tres");

        Assert.Equal(new[] { "Uno", "Dos", "Tres" }, renamed.Months.Select(m => m.Name));
    }

    [Fact]
    public void ACalendarWithNoWeekTakesNoWeekdayNames()
    {
        var weekless = new GameCalendarBuilder("Weekless").AddMonth("Only", 10).Build();

        Assert.Equal(0, weekless.WithWeekdayNames().WeekLength);

        var error = Assert.Throws<CalendarValidationException>(() => _ = weekless.WithWeekdayNames("A"));
        Assert.Contains("no week", error.Message);
    }

    [Fact]
    public void TheLabelCanBeChangedOnItsOwn()
    {
        var renamed = Calendar.WithLabel("Revolutionary Calendar");

        Assert.Equal("Revolutionary Calendar", renamed.Label);
        Assert.Equal("Revolutionary Calendar", renamed.ToString());
        Assert.Equal("Common Reckoning", Calendar.Label);

        Assert.Equal(Calendar.Id, renamed.Id);
        Assert.Equal(Calendar.Description, renamed.Description);
        Assert.Equal(Calendar.DaysInYear, renamed.DaysInYear);
        Assert.Equal("Frostwane", renamed.Month(1).Name);
        Assert.Equal(Calendar.Date(1999, 1, 14).Ticks, renamed.Date(1999, 1, 14).Ticks);
    }

    [Fact]
    public void AllThreeRenamingsChainTogether()
    {
        var renamed = Calendar
            .WithLabel("Revolutionary Calendar")
            .WithMonthNames(TwelveMonths)
            .WithWeekdayNames(SevenDays);

        Assert.Equal("Revolutionary Calendar", renamed.Label);
        Assert.Equal("Nivose", renamed.Month(1).Name);
        Assert.Equal("Primus", renamed.Date(1, 1, 1).Weekday?.Name);
        Assert.Equal(Calendar.Id, renamed.Id);
        Assert.Equal(Calendar.DaysInYear, renamed.DaysInYear);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankLabelIsRefused(string label)
    {
        var error = Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithLabel(label));
        Assert.Contains("must have a label", error.Message);
    }

    [Fact]
    public void RenamingLeavesTheOriginalAlone()
    {
        var renamed = Calendar.WithMonthNames(TwelveMonths);

        Assert.Equal("Frostwane", Calendar.Month(1).Name);
        Assert.Equal("Nivose", renamed.Month(1).Name);
        Assert.NotSame(Calendar, renamed);
    }
}
