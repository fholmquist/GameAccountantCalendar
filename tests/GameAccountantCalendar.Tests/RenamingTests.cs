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

    private static readonly string[] FiveFestivals =
    [
        "Sansculottides", "Vertu", "Genie", "Travail", "Opinion",
    ];

    [Fact]
    public void FestivalsCanBeRenamedWithoutTouchingTheMonths()
    {
        var renamed = Calendar.WithFestivalNames(FiveFestivals);

        Assert.Equal(
            FiveFestivals,
            renamed.Months.Where(m => m.IsHoliday).Select(m => m.Name));

        Assert.Equal(
            Calendar.Months.Where(m => !m.IsHoliday).Select(m => m.Name),
            renamed.Months.Where(m => !m.IsHoliday).Select(m => m.Name));

        Assert.Equal("Sansculottides", renamed.Month(3).Name);
        Assert.Equal("Frostwane", renamed.Month(1).Name);
    }

    [Fact]
    public void RenamedFestivalsKeepTheirDaysAndStayOutsideTheWeek()
    {
        var renamed = Calendar.WithFestivalNames(FiveFestivals);
        var festival = renamed.Date(1999, 3, 1);

        Assert.True(festival.IsHoliday);
        Assert.Null(festival.Weekday);
        Assert.Equal("Sansculottides 1999", festival.ToString("d"));
        Assert.Equal(Calendar.Date(1999, 3, 1).Ticks, festival.Ticks);
        Assert.Equal(
            Calendar.Months.Select(m => (m.Number, m.StartDay, m.EndDay, m.IsHoliday)),
            renamed.Months.Select(m => (m.Number, m.StartDay, m.EndDay, m.IsHoliday)));
    }

    [Fact]
    public void MonthsAndFestivalsCanBeRenamedIndependently()
    {
        var renamed = Calendar
            .WithMonthNames(TwelveMonths)
            .WithFestivalNames(FiveFestivals);

        Assert.Equal("Nivose", renamed.Month(1).Name);
        Assert.Equal("Sansculottides", renamed.Month(3).Name);
        Assert.Equal("Frimaire", renamed.Month(17).Name);

        // Order does not matter; neither call disturbs the other's entries.
        var reversed = Calendar
            .WithFestivalNames(FiveFestivals)
            .WithMonthNames(TwelveMonths);

        Assert.Equal(
            renamed.Months.Select(m => m.Name),
            reversed.Months.Select(m => m.Name));
    }

    [Fact]
    public void FestivalCountNamesTheEntriesMonthCountLeavesOut()
    {
        Assert.Equal(12, Calendar.MonthCount);
        Assert.Equal(5, Calendar.FestivalCount);
        Assert.Equal(Calendar.Months.Count, Calendar.MonthCount + Calendar.FestivalCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(12)]
    public void TheWrongNumberOfFestivalNamesIsRefused(int count)
    {
        var names = Enumerable.Range(1, count).Select(i => $"F{i}").ToArray();

        var error = Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithFestivalNames(names));
        Assert.Contains("5 festivals", error.Message);
    }

    [Fact]
    public void ACalendarWithNoFestivalsTakesNoFestivalNames()
    {
        var plain = new GameCalendarBuilder("Plain")
            .WithWeekdays("A", "B", "C")
            .AddMonths(30, "One", "Two", "Three")
            .Build();

        Assert.Equal(0, plain.FestivalCount);
        Assert.Equal(3, plain.WithFestivalNames().Months.Count);

        var error = Assert.Throws<CalendarValidationException>(() => _ = plain.WithFestivalNames("Feast"));
        Assert.Contains("no festivals", error.Message);
    }

    [Fact]
    public void BlankFestivalNamesAreRefused()
    {
        var names = (string[])FiveFestivals.Clone();
        names[2] = " ";

        Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithFestivalNames(names));
    }

    /// <summary>Twelve slots: a secondary name on the first three, none on the rest.</summary>
    private static readonly string?[] TwelveAltNames =
    [
        "Snowfast", "Rainturn", "Windwake", null, null, null, null, null, null, null, null, null,
    ];

    [Fact]
    public void MonthAltNamesCanBeReplaced()
    {
        var renamed = Calendar.WithMonthAltNames(TwelveAltNames);

        // Entry 3 is the Firstplanting festival, so the third name lands on entry 4, Seedfall.
        Assert.Equal("Snowfast", renamed.Month(1).AltName);
        Assert.Equal("Rainturn", renamed.Month(2).AltName);
        Assert.Equal("Seedfall", renamed.Month(4).Name);
        Assert.Equal("Windwake", renamed.Month(4).AltName);
        Assert.Null(renamed.Month(5).AltName);

        // The primary names and the year's shape are untouched.
        Assert.Equal("Frostwane", renamed.Month(1).Name);
        Assert.Equal(
            Calendar.Months.Select(m => (m.Number, m.Name, m.StartDay, m.EndDay, m.IsHoliday)),
            renamed.Months.Select(m => (m.Number, m.Name, m.StartDay, m.EndDay, m.IsHoliday)));
    }

    [Fact]
    public void FestivalsKeepTheirAltNamesWhenOnlyTheMonthsAreGiven()
    {
        var renamed = Calendar.WithMonthAltNames(TwelveAltNames);

        Assert.All(renamed.Months.Where(m => m.IsHoliday), m => Assert.Null(m.AltName));
    }

    [Fact]
    public void NullAndBlankAltNamesClearRatherThanSetAnEmptyOne()
    {
        Assert.Equal("Deep Winter", Calendar.Month(1).AltName);
        Assert.Equal("Summertide", Calendar.Month(8).AltName);

        var cleared = Calendar.WithMonthAltNames(new string?[12]);
        Assert.All(cleared.Months, m => Assert.Null(m.AltName));

        var blanked = Calendar.WithMonthAltNames("", "  ", null, null, null, null, null, null, null, null, null, null);
        Assert.Null(blanked.Month(1).AltName);
        Assert.Null(blanked.Month(2).AltName);
    }

    [Fact]
    public void ClearingAnAltNameSendsMMMBackToTheMonthsOwnName()
    {
        Assert.Equal("Deep Winter", Calendar.Date(1999, 1, 1).ToString("MMM"));

        var cleared = Calendar.WithMonthAltNames(new string?[12]);

        Assert.Equal("Fro", cleared.Date(1999, 1, 1).ToString("MMM"));
        Assert.Equal("Frostwane", cleared.Date(1999, 1, 1).ToString("MMMM"));
    }

    [Fact]
    public void MonthsAreFoundByTheirNewAltNames()
    {
        var renamed = Calendar.WithMonthAltNames(TwelveAltNames);

        Assert.Equal("Frostwane", renamed.FindMonth("Snowfast")?.Name);
        Assert.Null(renamed.FindMonth("Deep Winter"));
        Assert.Equal("Frostwane", renamed.FindMonth("Frostwane")?.Name);
    }

    [Fact]
    public void SeventeenAltNamesCoverTheFestivalsToo()
    {
        var names = Enumerable.Range(1, Calendar.Months.Count).Select(i => (string?)$"A{i}").ToArray();
        var renamed = Calendar.WithMonthAltNames(names);

        Assert.Equal("A1", renamed.Month(1).AltName);
        Assert.Equal("A3", renamed.Month(3).AltName);
        Assert.True(renamed.Month(3).IsHoliday);
        Assert.Equal("A17", renamed.Month(17).AltName);
    }

    [Fact]
    public void FestivalAltNamesCanBeReplacedOnTheirOwn()
    {
        var renamed = Calendar.WithFestivalAltNames("Sowing", "Greening", "Zenith", "Harvest", "Turning");

        Assert.Equal(
            new[] { "Sowing", "Greening", "Zenith", "Harvest", "Turning" },
            renamed.Months.Where(m => m.IsHoliday).Select(m => m.AltName));

        // The ordinary months keep the secondary names they had.
        Assert.Equal("Deep Winter", renamed.Month(1).AltName);
        Assert.Equal("Summertide", renamed.Month(8).AltName);
        Assert.Equal("Firstplanting", renamed.Month(3).Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(18)]
    public void TheWrongNumberOfMonthAltNamesIsRefused(int count)
    {
        var names = Enumerable.Range(1, count).Select(i => (string?)$"A{i}").ToArray();

        var error = Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithMonthAltNames(names));
        Assert.Contains("12", error.Message);
        Assert.Contains("17", error.Message);
        Assert.Contains(nameof(GameCalendar.WithFestivalAltNames), error.Message);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void TheWrongNumberOfFestivalAltNamesIsRefused(int count)
    {
        var names = Enumerable.Range(1, count).Select(i => (string?)$"A{i}").ToArray();

        var error = Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithFestivalAltNames(names));
        Assert.Contains("5 festivals", error.Message);
    }

    [Fact]
    public void NamesAndAltNamesAreReplacedIndependently()
    {
        var renamed = Calendar
            .WithMonthNames(TwelveMonths)
            .WithMonthAltNames(TwelveAltNames);

        Assert.Equal("Nivose", renamed.Month(1).Name);
        Assert.Equal("Snowfast", renamed.Month(1).AltName);

        var reversed = Calendar
            .WithMonthAltNames(TwelveAltNames)
            .WithMonthNames(TwelveMonths);

        Assert.Equal(
            renamed.Months.Select(m => (m.Name, m.AltName)),
            reversed.Months.Select(m => (m.Name, m.AltName)));
    }

    [Fact]
    public void ABlankAltNameNeverSurvivesConstruction()
    {
        Assert.Null(new CalendarMonth(1, "Only", 1, 30, altName: "").AltName);
        Assert.Null(new CalendarMonth(1, "Only", 1, 30, altName: "   ").AltName);
        Assert.Equal("Alt", new CalendarMonth(1, "Only", 1, 30, altName: "Alt").AltName);
    }

    private static readonly string?[] SevenShortDays =
    [
        "Sun", "Moo", "Fir", "Wat", "Woo", "Iro", "Sta",
    ];

    [Fact]
    public void WeekdayAltNamesCanBeReplaced()
    {
        var renamed = Calendar.WithWeekdayAltNames("Sn", "Mn", "Fr", "Wt", "Wd", "In", "St");

        Assert.Equal(
            new[] { "Sn", "Mn", "Fr", "Wt", "Wd", "In", "St" },
            renamed.Weekdays.Select(w => w.AltName));

        // The primary names and the cycle are untouched.
        Assert.Equal(
            Calendar.Weekdays.Select(w => (w.Number, w.Name)),
            renamed.Weekdays.Select(w => (w.Number, w.Name)));
        Assert.Equal("Moonsday", renamed.Date(1999, 1, 14).Weekday?.Name);
    }

    [Fact]
    public void TheWeekdayAltNameIsWhatDddRenders()
    {
        // With none set, ddd shortens the primary name.
        Assert.Equal("Moo", Calendar.Date(1999, 1, 14).ToString("ddd"));

        var renamed = Calendar.WithWeekdayAltNames("Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat");

        Assert.Equal("Mon", renamed.Date(1999, 1, 14).ToString("ddd"));
        Assert.Equal("Moonsday", renamed.Date(1999, 1, 14).ToString("dddd"));
    }

    [Fact]
    public void ClearingAWeekdayAltNameSendsDddBackToShortening()
    {
        var renamed = Calendar
            .WithWeekdayAltNames(SevenShortDays)
            .WithWeekdayAltNames(new string?[7]);

        Assert.All(renamed.Weekdays, w => Assert.Null(w.AltName));
        Assert.Equal("Moo", renamed.Date(1999, 1, 14).ToString("ddd"));
    }

    [Fact]
    public void BlankWeekdayAltNamesClearRatherThanSetAnEmptyOne()
    {
        var renamed = Calendar.WithWeekdayAltNames("", "  ", null, "Wt", null, null, null);

        Assert.Null(renamed.Weekdays[0].AltName);
        Assert.Null(renamed.Weekdays[1].AltName);
        Assert.Equal("Wt", renamed.Weekdays[3].AltName);
        Assert.Null(new CalendarWeekday(1, "Only", altName: "   ").AltName);
    }

    [Fact]
    public void WeekdaysAreFoundByTheirAltNames()
    {
        var renamed = Calendar.WithWeekdayAltNames("Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat");

        Assert.Equal(2, renamed.FindWeekday("Mon")?.Number);
        Assert.Equal(2, renamed.FindWeekday("moonsday")?.Number);
        Assert.Null(renamed.FindWeekday("Blursday"));
    }

    [Fact]
    public void WeekdayNamesAndAltNamesAreReplacedIndependently()
    {
        var renamed = Calendar
            .WithWeekdayNames(SevenDays)
            .WithWeekdayAltNames(SevenShortDays);

        Assert.Equal("Secundus", renamed.Weekdays[1].Name);
        Assert.Equal("Moo", renamed.Weekdays[1].AltName);

        var reversed = Calendar
            .WithWeekdayAltNames(SevenShortDays)
            .WithWeekdayNames(SevenDays);

        Assert.Equal(
            renamed.Weekdays.Select(w => (w.Name, w.AltName)),
            reversed.Weekdays.Select(w => (w.Name, w.AltName)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(8)]
    public void TheWrongNumberOfWeekdayAltNamesIsRefused(int count)
    {
        var names = Enumerable.Range(1, count).Select(i => (string?)$"A{i}").ToArray();

        var error = Assert.Throws<CalendarValidationException>(() => _ = Calendar.WithWeekdayAltNames(names));
        Assert.Contains("7", error.Message);
    }

    [Fact]
    public void ACalendarWithNoWeekTakesNoWeekdayAltNames()
    {
        var weekless = new GameCalendarBuilder("Weekless").AddMonth("Only", 10).Build();

        Assert.Equal(0, weekless.WithWeekdayAltNames().WeekLength);

        var error = Assert.Throws<CalendarValidationException>(() => _ = weekless.WithWeekdayAltNames("A"));
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
            .WithFestivalNames(FiveFestivals)
            .WithWeekdayNames(SevenDays);

        Assert.Equal("Revolutionary Calendar", renamed.Label);
        Assert.Equal("Nivose", renamed.Month(1).Name);
        Assert.Equal("Sansculottides", renamed.Month(3).Name);
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
