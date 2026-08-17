namespace GameAccountantCalendar;

/// <summary>
/// Builds a <see cref="GameCalendar"/> a month at a time, working out each month's day-of-year range
/// from the lengths you give it so the year always tiles correctly.
/// </summary>
/// <example>
/// <code>
/// var calendar = new GameCalendarBuilder("Common Reckoning")
///     .WithDay(hours: 24, minutesPerHour: 60)
///     .WithWeekdays("Firstday", "Seconday", "Thirdday", "Fourthday", "Fifthday")
///     .AddMonth("Frostwane", days: 30)
///     .AddFestival("Thawing")
///     .AddMonth("Seedfall", days: 30)
///     .Build();
/// </code>
/// </example>
public sealed class GameCalendarBuilder
{
    private readonly List<CalendarMonth> _months = [];
    private readonly List<CalendarWeekday> _weekdays = [];
    private string _label;
    private string? _description;
    private string? _url;
    private int _hoursInDay = 24;
    private int _minutesInHour = 60;
    private int _startYear = 1;
    private int _epochWeekday = 1;
    private bool _holidaysBreakWeekCycle = true;
    private Guid? _id;
    private Guid? _campaignId;
    private int _nextStartDay = 1;

    /// <summary>Starts a calendar with the given label.</summary>
    public GameCalendarBuilder(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new CalendarValidationException("A calendar must have a label.");
        _label = label;
    }

    /// <summary>Renames the calendar.</summary>
    public GameCalendarBuilder WithLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new CalendarValidationException("A calendar must have a label.");
        _label = label;
        return this;
    }

    /// <summary>Sets the longer description.</summary>
    public GameCalendarBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>Sets the reference link.</summary>
    public GameCalendarBuilder WithUrl(string? url)
    {
        _url = url;
        return this;
    }

    /// <summary>Sets how long a day is. Defaults to 24 hours of 60 minutes.</summary>
    public GameCalendarBuilder WithDay(int hours, int minutesPerHour)
    {
        _hoursInDay = hours;
        _minutesInHour = minutesPerHour;
        return this;
    }

    /// <summary>Replaces the week with the given day names, in order. Pass nothing for a calendar with no week.</summary>
    public GameCalendarBuilder WithWeekdays(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _weekdays.Clear();
        for (int i = 0; i < names.Length; i++)
            _weekdays.Add(new CalendarWeekday(i + 1, names[i]));
        return this;
    }

    /// <summary>
    /// Sets the year at tick 0 — the earliest year the calendar can express, and the point a tick count
    /// is measured from. Defaults to year 1. Set it below the earliest date the campaign needs to record,
    /// since there is no room underneath it.
    /// </summary>
    public GameCalendarBuilder WithStartYear(int year)
    {
        _startYear = year;
        return this;
    }

    /// <summary>Sets which weekday the start year's first day falls on, 1-based. Defaults to the first.</summary>
    public GameCalendarBuilder WithEpochWeekday(int weekday)
    {
        _epochWeekday = weekday;
        return this;
    }

    /// <summary>
    /// Sets whether festival days sit outside the weekday cycle — the default — or run through it
    /// like ordinary days.
    /// </summary>
    public GameCalendarBuilder WithHolidaysInWeekCycle(bool inCycle = true)
    {
        _holidaysBreakWeekCycle = !inCycle;
        return this;
    }

    /// <summary>Sets the calendar's stable identity and, optionally, the campaign it belongs to.</summary>
    public GameCalendarBuilder WithIdentity(Guid id, Guid? campaignId = null)
    {
        _id = id;
        _campaignId = campaignId;
        return this;
    }

    /// <summary>Appends an ordinary month of the given length.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="days">Number of days in the month.</param>
    /// <param name="altName">Secondary name, such as a common name for a formal one.</param>
    /// <param name="startingWeekday">Pins this month's first day to a weekday, 1-based.</param>
    public GameCalendarBuilder AddMonth(string name, int days, string? altName = null, int? startingWeekday = null)
        => Append(name, days, altName, isHoliday: false, startingWeekday);

    /// <summary>
    /// Appends an intercalary festival — a stretch of days between two months that, by default, has no
    /// weekday and does not advance the week.
    /// </summary>
    /// <param name="name">Display name.</param>
    /// <param name="days">Number of days, usually one.</param>
    /// <param name="altName">Secondary name.</param>
    public GameCalendarBuilder AddFestival(string name, int days = 1, string? altName = null)
        => Append(name, days, altName, isHoliday: true, startingWeekday: null);

    /// <summary>Appends several ordinary months that are all the same length.</summary>
    public GameCalendarBuilder AddMonths(int days, params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        foreach (string name in names)
            AddMonth(name, days);
        return this;
    }

    /// <summary>Builds the calendar and validates it.</summary>
    /// <exception cref="CalendarValidationException">The definition does not describe a usable year.</exception>
    public GameCalendar Build() => new(
        _label,
        _months,
        _weekdays,
        _hoursInDay,
        _minutesInHour,
        _startYear,
        _description,
        _url,
        _epochWeekday,
        _holidaysBreakWeekCycle,
        _id,
        _campaignId);

    private GameCalendarBuilder Append(string name, int days, string? altName, bool isHoliday, int? startingWeekday)
    {
        if (days < 1)
            throw new CalendarValidationException($"'{name}' must be at least one day long, but was given {days}.");

        _months.Add(new CalendarMonth(
            _months.Count + 1,
            name,
            _nextStartDay,
            _nextStartDay + days - 1,
            altName,
            isHoliday,
            startingWeekday));

        _nextStartDay += days;
        return this;
    }
}
