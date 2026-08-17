namespace GameAccountantCalendar;

/// <summary>
/// A moment in a world's history: a <see cref="GameCalendar"/> together with a tick count, counting from
/// the first instant of the calendar's <see cref="GameCalendar.StartYear"/>. The tick is the value you
/// persist — it is what the <c>bigint game_time_start</c> column on a game event holds, via
/// <see cref="ToInt64"/>.
/// </summary>
/// <remarks>
/// <para>
/// A date carries combat-scale precision as well as calendar-scale: below the minute sit the
/// <see cref="Round"/> (10 to a minute), the <see cref="Turn"/> (100 to a round) and the
/// <see cref="Tick"/> (100 to a turn).
/// </para>
/// <para>
/// Dates are only comparable within one calendar. Comparing dates from two different calendars throws,
/// since a tick sits at a different place in each.
/// </para>
/// </remarks>
public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>, IFormattable
{
    private readonly GameCalendar? _calendar;

    internal GameDate(GameCalendar calendar, ulong ticks)
    {
        _calendar = calendar;
        Ticks = ticks;
    }

    /// <summary>Ticks since the first instant of the calendar's start year. Never negative.</summary>
    public ulong Ticks { get; }

    /// <summary>The calendar this date is measured against.</summary>
    /// <exception cref="InvalidOperationException">The date is <c>default(GameDate)</c> and has no calendar.</exception>
    public GameCalendar Calendar => _calendar
        ?? throw new InvalidOperationException(
            "This GameDate has no calendar. Build dates with GameCalendar.Date or GameCalendar.FromTicks.");

    /// <summary>True when this is <c>default(GameDate)</c>, with no calendar behind it.</summary>
    public bool IsEmpty => _calendar is null;

    /// <summary>The year, never earlier than the calendar's <see cref="GameCalendar.StartYear"/>.</summary>
    public int Year => Decompose().Year;

    /// <summary>The month or festival this day falls in.</summary>
    public CalendarMonth Month => Calendar.MonthAt(Decompose().MonthIndex);

    /// <summary>Number of the month or festival this day falls in, 1-based.</summary>
    public int MonthNumber => Decompose().MonthIndex + 1;

    /// <summary>Day within the month, 1-based.</summary>
    public int Day => Decompose().Day;

    /// <summary>Day within the year, 1-based.</summary>
    public int DayOfYear => Decompose().DayOfYear;

    /// <summary>Hour of the day, from 0.</summary>
    public int Hour => (int)(TickOfDay / Calendar.TicksPerHour);

    /// <summary>Minute of the hour, from 0.</summary>
    public int Minute => (int)(TickOfDay % Calendar.TicksPerHour / GameCalendar.TicksPerMinute);

    /// <summary>Round of the minute, 0 to 9.</summary>
    public int Round => (int)(Ticks % GameCalendar.TicksPerMinute / GameCalendar.TicksPerRound);

    /// <summary>Turn of the round, 0 to 99.</summary>
    public int Turn => (int)(Ticks % GameCalendar.TicksPerRound / GameCalendar.TicksPerTurn);

    /// <summary>Tick of the turn, 0 to 99.</summary>
    public int Tick => (int)(Ticks % GameCalendar.TicksPerTurn);

    /// <summary>Ticks elapsed since midnight.</summary>
    public long TickOfDay => (long)(Ticks % (ulong)Calendar.TicksPerDay);

    /// <summary>Minutes elapsed since midnight, rounded down.</summary>
    public long MinuteOfDay => TickOfDay / GameCalendar.TicksPerMinute;

    /// <summary>
    /// The weekday, or null when the day sits outside the weekday cycle — an intercalary festival on a
    /// calendar where <see cref="GameCalendar.HolidaysBreakWeekCycle"/> is set, or a calendar with no week.
    /// </summary>
    public CalendarWeekday? Weekday
    {
        get
        {
            var parts = Decompose();
            return Calendar.WeekdayFor(parts.YearIndex, parts.MonthIndex, parts.Day);
        }
    }

    /// <summary>True when this day falls in an intercalary festival rather than an ordinary month.</summary>
    public bool IsHoliday => Month.IsHoliday;

    /// <summary>Midnight on this day.</summary>
    public GameDate StartOfDay => new(Calendar, Ticks - (ulong)TickOfDay);

    /// <summary>The last tick of this day.</summary>
    public GameDate EndOfDay => StartOfDay.AddTicks(Calendar.TicksPerDay - 1);

    /// <summary>The first tick of this minute.</summary>
    public GameDate StartOfMinute => new(Calendar, Ticks - (Ticks % GameCalendar.TicksPerMinute));

    /// <summary>Midnight on the first day of this month.</summary>
    public GameDate StartOfMonth
    {
        get
        {
            var parts = Decompose();
            return Calendar.Date(parts.Year, parts.MonthIndex + 1, 1);
        }
    }

    /// <summary>Midnight on the first day of this year.</summary>
    public GameDate StartOfYear => Calendar.DateFromDayOfYear(Decompose().Year, 1);

    /// <summary>The tick count as a signed integer, for storing in a <c>bigint</c> column.</summary>
    public long ToInt64() => (long)Ticks;

    /// <summary>Moves forward by whole ticks. Pass a negative count to move back.</summary>
    public GameDate AddTicks(long ticks) => Shift(ticks);

    /// <summary>Moves forward by whole turns.</summary>
    public GameDate AddTurns(long turns) => Shift(turns * GameCalendar.TicksPerTurn);

    /// <summary>Moves forward by whole rounds.</summary>
    public GameDate AddRounds(long rounds) => Shift(rounds * GameCalendar.TicksPerRound);

    /// <summary>Moves forward by whole minutes.</summary>
    public GameDate AddMinutes(long minutes) => Shift(minutes * GameCalendar.TicksPerMinute);

    /// <summary>Moves forward by whole hours.</summary>
    public GameDate AddHours(long hours) => Shift(hours * Calendar.TicksPerHour);

    /// <summary>Moves forward by whole days, crossing festivals like any other day.</summary>
    public GameDate AddDays(long days) => Shift(days * Calendar.TicksPerDay);

    /// <summary>Moves forward by whole weeks.</summary>
    /// <exception cref="InvalidOperationException">The calendar has no week.</exception>
    public GameDate AddWeeks(long weeks) => Add(Calendar.Weeks(weeks));

    /// <summary>Moves forward by the given span.</summary>
    public GameDate Add(GameTimeSpan span) => Shift(span.TotalTicks);

    /// <summary>
    /// Moves forward by whole months, keeping the day of the month and the time of day.
    /// </summary>
    /// <remarks>
    /// Only ordinary months count as a step; intercalary festivals are passed over. Where the target month
    /// is shorter than the current one the day is clamped to its last day, so month arithmetic is not
    /// always reversible. A date that itself falls on a festival is first moved to day 1 of the next
    /// ordinary month, and the step is counted from there.
    /// </remarks>
    public GameDate AddMonths(int months)
    {
        var calendar = Calendar;
        var parts = Decompose();
        int monthIndex = parts.MonthIndex;
        int year = parts.Year;
        int day = parts.Day;

        if (calendar.MonthAt(monthIndex).IsHoliday)
        {
            monthIndex = calendar.NextCountedMonth(monthIndex, out int wrapped);
            year += wrapped;
            day = 1;
        }

        int target = calendar.StepCountedMonths(monthIndex, months, out int yearsMoved);
        year += yearsMoved;
        int clamped = Math.Min(day, calendar.MonthAt(target).Length);
        return calendar.Date(year, target + 1, clamped, Hour, Minute, Round, Turn, Tick);
    }

    /// <summary>
    /// Moves forward by whole years, keeping the month, day and time of day. Exact in both directions,
    /// because every year in a calendar is the same length.
    /// </summary>
    public GameDate AddYears(int years)
    {
        var calendar = Calendar;
        var parts = Decompose();
        return calendar.DateFromDayOfYear(
            checked(parts.Year + years), parts.DayOfYear, Hour, Minute, Round, Turn, Tick);
    }

    /// <summary>Moves back by the given span.</summary>
    public GameDate Subtract(GameTimeSpan span) => Shift(-span.TotalTicks);

    /// <summary>The span from <paramref name="other"/> to this date. Negative when this date is earlier.</summary>
    /// <exception cref="ArgumentException">The two dates use different calendars.</exception>
    public GameTimeSpan Subtract(GameDate other)
    {
        RequireSameCalendar(other);
        return new GameTimeSpan((long)Ticks - (long)other.Ticks);
    }

    /// <summary>Whole days from this date to <paramref name="other"/>, counting midnight boundaries crossed.</summary>
    /// <exception cref="ArgumentException">The two dates use different calendars.</exception>
    public long DaysUntil(GameDate other)
    {
        RequireSameCalendar(other);
        ulong perDay = (ulong)Calendar.TicksPerDay;
        return (long)(other.Ticks / perDay) - (long)(Ticks / perDay);
    }

    /// <summary>The same day at a different time.</summary>
    public GameDate WithTime(int hour, int minute, int round = 0, int turn = 0, int tick = 0)
    {
        var parts = Decompose();
        return Calendar.DateFromDayOfYear(parts.Year, parts.DayOfYear, hour, minute, round, turn, tick);
    }

    /// <inheritdoc />
    public bool Equals(GameDate other)
        => Ticks == other.Ticks && ReferenceEquals(_calendar, other._calendar);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GameDate other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Ticks, _calendar?.Id);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The two dates use different calendars.</exception>
    public int CompareTo(GameDate other)
    {
        RequireSameCalendar(other);
        return Ticks.CompareTo(other.Ticks);
    }

    /// <summary>Renders the date in the default full format, for example <c>"Moonsday, 14 Frostwane 1999 6:30"</c>.</summary>
    public override string ToString() => ToString(null, null);

    /// <summary>Renders the date with a standard or custom format string.</summary>
    /// <param name="format">
    /// A standard specifier (<c>d D t T f F n o y</c>) or a custom pattern built from
    /// <c>yyyy MMMM MM dddd dd DDD HH mm RR TT KK</c>. Null or empty means <c>"F"</c>.
    /// </param>
    public string ToString(string? format) => ToString(format, null);

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
        => IsEmpty ? string.Empty : GameDateFormatter.Format(this, format, formatProvider);

    internal DateParts Decompose()
    {
        var calendar = Calendar;
        ulong dayIndex = Ticks / (ulong)calendar.TicksPerDay;
        ulong yearIndex = dayIndex / (uint)calendar.DaysInYear;
        int dayOfYear = (int)(dayIndex - (yearIndex * (uint)calendar.DaysInYear)) + 1;
        int monthIndex = calendar.MonthIndexForDayOfYear(dayOfYear);
        int day = dayOfYear - calendar.MonthAt(monthIndex).StartDay + 1;
        return new DateParts(calendar.StartYear + (int)yearIndex, (long)yearIndex, monthIndex, day, dayOfYear);
    }

    private GameDate Shift(long ticks)
    {
        var calendar = Calendar;
        if (ticks < 0 && (ulong)(-ticks) > Ticks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticks), ticks,
                $"Moving back {-ticks} ticks lands before year {calendar.StartYear}, where calendar " +
                $"'{calendar.Label}' begins reckoning.");
        }

        ulong shifted = ticks < 0 ? Ticks - (ulong)(-ticks) : Ticks + (ulong)ticks;
        if (ticks > 0 && (shifted < Ticks || shifted > calendar.MaxTicks))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticks), ticks,
                $"Moving on {ticks} ticks passes year {calendar.MaxYear}, the furthest calendar " +
                $"'{calendar.Label}' reckons.");
        }

        return new GameDate(calendar, shifted);
    }

    private void RequireSameCalendar(GameDate other)
    {
        if (!ReferenceEquals(_calendar, other._calendar))
        {
            throw new ArgumentException(
                $"Cannot compare a date on '{_calendar?.Label ?? "(none)"}' with one on " +
                $"'{other._calendar?.Label ?? "(none)"}'. A tick sits at a different place on each calendar.",
                nameof(other));
        }
    }

#pragma warning disable CS1591
    public static GameDate operator +(GameDate date, GameTimeSpan span) => date.Add(span);

    public static GameDate operator -(GameDate date, GameTimeSpan span) => date.Subtract(span);

    public static GameTimeSpan operator -(GameDate left, GameDate right) => left.Subtract(right);

    public static bool operator ==(GameDate left, GameDate right) => left.Equals(right);

    public static bool operator !=(GameDate left, GameDate right) => !left.Equals(right);

    public static bool operator <(GameDate left, GameDate right) => left.CompareTo(right) < 0;

    public static bool operator >(GameDate left, GameDate right) => left.CompareTo(right) > 0;

    public static bool operator <=(GameDate left, GameDate right) => left.CompareTo(right) <= 0;

    public static bool operator >=(GameDate left, GameDate right) => left.CompareTo(right) >= 0;
#pragma warning restore CS1591

    internal readonly record struct DateParts(int Year, long YearIndex, int MonthIndex, int Day, int DayOfYear);
}
