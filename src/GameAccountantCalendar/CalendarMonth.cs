namespace GameAccountantCalendar;

/// <summary>
/// One stretch of consecutive days in a year — either an ordinary month or, when
/// <see cref="IsHoliday"/> is set, an intercalary festival that sits between two months.
/// </summary>
/// <remarks>
/// Mirrors the <c>calendars.gt_month</c> table. A month is defined by the absolute day-of-year
/// range it covers rather than by a length, so the months of a calendar must tile the year
/// end to end with no gaps and no overlaps.
/// </remarks>
public sealed class CalendarMonth
{
    /// <summary>Creates a month covering <paramref name="startDay"/> through <paramref name="endDay"/> inclusive.</summary>
    /// <param name="number">Position in the year, 1-based (<c>month_num</c>). Holidays are numbered alongside months.</param>
    /// <param name="name">Display name (<c>month_name</c>).</param>
    /// <param name="startDay">First day of year this month covers, 1-based (<c>start_day</c>).</param>
    /// <param name="endDay">Last day of year this month covers, inclusive (<c>end_day</c>).</param>
    /// <param name="altName">Secondary name, such as the common name for a formal one (<c>alt_name</c>).</param>
    /// <param name="isHoliday">Whether this is an intercalary festival rather than an ordinary month (<c>is_holiday</c>).</param>
    /// <param name="startingWeekday">
    /// Pins the first day of this month to a specific weekday, 1-based (<c>starting_weekday</c>).
    /// Leave null to let the weekday cycle run on from the preceding month.
    /// </param>
    public CalendarMonth(
        int number,
        string name,
        int startDay,
        int endDay,
        string? altName = null,
        bool isHoliday = false,
        int? startingWeekday = null)
    {
        if (number < 1)
            throw new CalendarValidationException($"Month number must be 1 or greater, but was {number}.");
        if (string.IsNullOrWhiteSpace(name))
            throw new CalendarValidationException($"Month {number} must have a name.");
        if (startDay < 1)
            throw new CalendarValidationException($"Month '{name}' starts on day {startDay}; the first day of a year is day 1.");
        if (endDay < startDay)
            throw new CalendarValidationException($"Month '{name}' ends on day {endDay}, before its start day {startDay}.");
        if (startingWeekday is int sw && sw < 1)
            throw new CalendarValidationException($"Month '{name}' pins a starting weekday of {sw}; weekdays are numbered from 1.");

        Number = number;
        Name = name;
        StartDay = startDay;
        EndDay = endDay;
        // A month either has a secondary name or it does not; a blank one is the latter, so that
        // formats falling back on it (MMM) reach the primary name rather than rendering nothing.
        AltName = string.IsNullOrWhiteSpace(altName) ? null : altName;
        IsHoliday = isHoliday;
        StartingWeekday = startingWeekday;
    }

    /// <summary>Position in the year, 1-based.</summary>
    public int Number { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Secondary name, or null when the month only has one.</summary>
    public string? AltName { get; }

    /// <summary>True when this is an intercalary festival rather than an ordinary month.</summary>
    public bool IsHoliday { get; }

    /// <summary>First day of year covered, 1-based.</summary>
    public int StartDay { get; }

    /// <summary>Last day of year covered, inclusive.</summary>
    public int EndDay { get; }

    /// <summary>The weekday this month's first day is pinned to (1-based), or null to follow the running cycle.</summary>
    public int? StartingWeekday { get; }

    /// <summary>Number of days in this month.</summary>
    public int Length => EndDay - StartDay + 1;

    /// <summary>A copy of this month under a different name, keeping its place in the year.</summary>
    public CalendarMonth WithName(string name)
        => new(Number, name, StartDay, EndDay, AltName, IsHoliday, StartingWeekday);

    /// <summary>
    /// A copy of this month with a different secondary name, keeping its place in the year. Null or
    /// blank means it has none.
    /// </summary>
    public CalendarMonth WithAltName(string? altName)
        => new(Number, Name, StartDay, EndDay, altName, IsHoliday, StartingWeekday);

    /// <inheritdoc />
    public override string ToString() => Name;
}
