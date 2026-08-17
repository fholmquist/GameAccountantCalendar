namespace GameAccountantCalendar;

/// <summary>
/// One worked example, for tests, demos and as a starting point to copy. Real campaign calendars are
/// expected to come from a <see cref="GameCalendarBuilder"/> or from JSON.
/// </summary>
public static class SampleCalendars
{
    private static readonly Lazy<GameCalendar> LazyCommonReckoning = new(BuildCommonReckoning);

    /// <summary>
    /// A generic 365-day year: twelve 30-day months separated by five single-day festivals, a seven-day
    /// week, and a 24-hour day. The festivals sit outside the weekday cycle in the usual way for
    /// intercalary days, so they have no weekday of their own and do not advance the week.
    /// </summary>
    public static GameCalendar CommonReckoning => LazyCommonReckoning.Value;

    private static GameCalendar BuildCommonReckoning() => new GameCalendarBuilder("Common Reckoning")
        .WithDescription("A plain twelve-month year with five festival days, for demonstration and tests.")
        .WithDay(hours: 24, minutesPerHour: 60)
        .WithWeekdays("Sunsday", "Moonsday", "Fireday", "Watersday", "Woodsday", "Ironsday", "Starsday")
        .AddMonth("Frostwane", days: 30, altName: "Deep Winter")
        .AddMonth("Thawtide", days: 30)
        .AddFestival("Firstplanting")
        .AddMonth("Seedfall", days: 30)
        .AddMonth("Bloomrise", days: 30)
        .AddFestival("Greentide")
        .AddMonth("Longlight", days: 30)
        .AddMonth("Highsun", days: 30, altName: "Summertide")
        .AddFestival("Midyear")
        .AddMonth("Sunwane", days: 30)
        .AddMonth("Harvestide", days: 30)
        .AddFestival("Reaping")
        .AddMonth("Emberfall", days: 30)
        .AddMonth("Duskmere", days: 30)
        .AddFestival("Yearsend")
        .AddMonth("Coldreach", days: 30)
        .AddMonth("Deepnight", days: 30)
        .Build();
}
