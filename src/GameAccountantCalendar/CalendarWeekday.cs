namespace GameAccountantCalendar;

/// <summary>One named day of the week. Mirrors the <c>calendars.gt_weekday</c> table.</summary>
public sealed class CalendarWeekday
{
    /// <summary>Creates a weekday.</summary>
    /// <param name="number">Position in the week, 1-based (<c>day_num</c>).</param>
    /// <param name="name">Display name (<c>day_name</c>).</param>
    /// <param name="altName">
    /// Secondary name — a short or common form, rendered by <c>ddd</c>. Unlike the rest of a weekday
    /// this has no column behind it in <c>gt_weekday</c>; it is the library's own. Null or blank means
    /// the weekday has none.
    /// </param>
    public CalendarWeekday(int number, string name, string? altName = null)
    {
        if (number < 1)
            throw new CalendarValidationException($"Weekday number must be 1 or greater, but was {number}.");
        if (string.IsNullOrWhiteSpace(name))
            throw new CalendarValidationException($"Weekday {number} must have a name.");

        Number = number;
        Name = name;
        // A weekday either has a secondary name or it does not; a blank one is the latter, so that
        // ddd falls back to shortening the primary name rather than rendering nothing.
        AltName = string.IsNullOrWhiteSpace(altName) ? null : altName;
    }

    /// <summary>Position in the week, 1-based.</summary>
    public int Number { get; }

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Secondary name, or null when the weekday only has one.</summary>
    public string? AltName { get; }

    /// <summary>A copy of this weekday under a different name, keeping its place in the week.</summary>
    public CalendarWeekday WithName(string name) => new(Number, name, AltName);

    /// <summary>
    /// A copy of this weekday with a different secondary name, keeping its place in the week. Null or
    /// blank means it has none.
    /// </summary>
    public CalendarWeekday WithAltName(string? altName) => new(Number, Name, altName);

    /// <inheritdoc />
    public override string ToString() => Name;
}
