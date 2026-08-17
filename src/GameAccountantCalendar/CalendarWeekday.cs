namespace GameAccountantCalendar;

/// <summary>One named day of the week. Mirrors the <c>calendars.gt_weekday</c> table.</summary>
public sealed class CalendarWeekday
{
    /// <summary>Creates a weekday.</summary>
    /// <param name="number">Position in the week, 1-based (<c>day_num</c>).</param>
    /// <param name="name">Display name (<c>day_name</c>).</param>
    public CalendarWeekday(int number, string name)
    {
        if (number < 1)
            throw new CalendarValidationException($"Weekday number must be 1 or greater, but was {number}.");
        if (string.IsNullOrWhiteSpace(name))
            throw new CalendarValidationException($"Weekday {number} must have a name.");

        Number = number;
        Name = name;
    }

    /// <summary>Position in the week, 1-based.</summary>
    public int Number { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
