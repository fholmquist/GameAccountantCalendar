using System.Text;

namespace GameAccountantCalendar;

/// <summary>
/// The shape of a world's year: its months, its weekdays, how many hours and minutes a day holds, and
/// which year sits at the start of reckoning. A calendar is immutable and thread-safe once built, and is
/// the factory for every <see cref="GameDate"/> and <see cref="GameTimeSpan"/> measured against it.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the <c>calendars.gt_calendar</c> table. Time is counted in <em>ticks</em> — the smallest unit
/// the game tracks — as an unsigned 64-bit count since the first instant of <see cref="StartYear"/>.
/// Below a minute the units are the ones combat runs on:
/// </para>
/// <list type="bullet">
///   <item><description>10 rounds to a minute (<see cref="RoundsPerMinute"/>)</description></item>
///   <item><description>100 turns to a round (<see cref="TurnsPerRound"/>)</description></item>
///   <item><description>100 ticks to a turn (<see cref="TicksPerTurn"/>)</description></item>
/// </list>
/// <para>
/// which gives <see cref="TicksPerMinute"/> = 100,000. Because reckoning starts at <see cref="StartYear"/>
/// rather than at year 1, a tick count is never negative and always fits in a <see cref="ulong"/>; it also
/// stays inside <see cref="long.MaxValue"/> so it round-trips through the signed <c>bigint game_time_start</c>
/// column, which is what <see cref="GameDate.ToInt64"/> is for.
/// </para>
/// <para>
/// Every year has the same length, because months are defined by fixed day-of-year ranges. There is no
/// leap-year mechanism, which matches the schema this models.
/// </para>
/// </remarks>
public sealed class GameCalendar
{
    /// <summary>Ticks in one turn.</summary>
    public const int TicksPerTurn = 100;

    /// <summary>Turns in one round.</summary>
    public const int TurnsPerRound = 100;

    /// <summary>Rounds in one minute.</summary>
    public const int RoundsPerMinute = 10;

    /// <summary>Ticks in one round: 10,000.</summary>
    public const int TicksPerRound = TurnsPerRound * TicksPerTurn;

    /// <summary>Ticks in one minute: 100,000.</summary>
    public const int TicksPerMinute = RoundsPerMinute * TicksPerRound;

    /// <summary>The furthest any calendar will reckon past its <see cref="StartYear"/>.</summary>
    private const long YearHorizon = 1_000_000;

    private readonly CalendarMonth[] _months;
    private readonly CalendarWeekday[] _weekdays;
    private readonly int[] _countedMonths;
    private readonly WeekAnchor[] _anchors;
    private readonly bool _weekIsPinned;
    private readonly int _yearAdvance;
    private readonly int _pinnedYearStart;

    /// <summary>Creates a calendar from its months and weekdays.</summary>
    /// <param name="label">Short name of the calendar (<c>label</c>).</param>
    /// <param name="months">
    /// The months and intercalary festivals of a year. They are sorted by <see cref="CalendarMonth.Number"/>
    /// and must tile day 1 through the last day of the year with no gaps and no overlaps.
    /// </param>
    /// <param name="weekdays">
    /// The named days of the week, in order. Pass null or an empty list for a calendar with no week at all,
    /// in which case every <see cref="GameDate.Weekday"/> is null.
    /// </param>
    /// <param name="hoursInDay">Hours in a day (<c>num_hours</c>).</param>
    /// <param name="minutesInHour">Minutes in an hour (<c>num_minutes</c>).</param>
    /// <param name="startYear">
    /// The year at tick 0 — the earliest moment this calendar can express. Set it below the earliest date
    /// your campaign needs to record, since there is no room underneath it.
    /// </param>
    /// <param name="description">Longer description (<c>description</c>).</param>
    /// <param name="url">Reference link for the calendar (<c>url</c>).</param>
    /// <param name="epochWeekday">
    /// The weekday of <see cref="StartYear"/>, day 1, 1-based. Ignored when any month pins its own
    /// <see cref="CalendarMonth.StartingWeekday"/>, since those pins fix the cycle instead.
    /// </param>
    /// <param name="holidaysBreakWeekCycle">
    /// When true (the default), days in a holiday month have no weekday and do not advance the week —
    /// the usual treatment for intercalary festivals. When false, holidays run through the cycle like
    /// any other day.
    /// </param>
    /// <param name="id">Stable identity for the calendar (<c>id</c>). A new Guid is generated when omitted.</param>
    /// <param name="campaignId">The campaign this calendar belongs to (<c>campaign_id</c>).</param>
    /// <exception cref="CalendarValidationException">The definition does not describe a usable year.</exception>
    public GameCalendar(
        string label,
        IEnumerable<CalendarMonth> months,
        IEnumerable<CalendarWeekday>? weekdays = null,
        int hoursInDay = 24,
        int minutesInHour = 60,
        int startYear = 1,
        string? description = null,
        string? url = null,
        int epochWeekday = 1,
        bool holidaysBreakWeekCycle = true,
        Guid? id = null,
        Guid? campaignId = null)
    {
        ArgumentNullException.ThrowIfNull(months);

        if (string.IsNullOrWhiteSpace(label))
            throw new CalendarValidationException("A calendar must have a label.");
        if (hoursInDay < 1)
            throw new CalendarValidationException($"A day must have at least one hour, but '{label}' declares {hoursInDay}.");
        if (minutesInHour < 1)
            throw new CalendarValidationException($"An hour must have at least one minute, but '{label}' declares {minutesInHour}.");

        _months = months.OrderBy(m => m.Number).ToArray();
        _weekdays = weekdays?.OrderBy(w => w.Number).ToArray() ?? [];

        ValidateMonths(label, _months);
        ValidateWeekdays(label, _weekdays);

        Label = label;
        Description = description;
        Url = url;
        HoursInDay = hoursInDay;
        MinutesInHour = minutesInHour;
        StartYear = startYear;
        HolidaysBreakWeekCycle = holidaysBreakWeekCycle;
        Id = id ?? Guid.NewGuid();
        CampaignId = campaignId;
        DaysInYear = _months[^1].EndDay;
        Months = _months;
        Weekdays = _weekdays;
        _countedMonths = Enumerable.Range(0, _months.Length).Where(i => !_months[i].IsHoliday).ToArray();

        try
        {
            TicksPerHour = checked((long)minutesInHour * TicksPerMinute);
            TicksPerDay = checked(TicksPerHour * hoursInDay);
            TicksPerYear = checked(TicksPerDay * DaysInYear);
        }
        catch (OverflowException ex)
        {
            throw new CalendarValidationException(
                $"A year of calendar '{label}' is too long to count in ticks. " +
                $"{DaysInYear} days of {hoursInDay} hours of {minutesInHour} minutes overflows a 64-bit count.",
                ex);
        }

        long horizon = Math.Min(YearHorizon, (long.MaxValue / TicksPerYear) - 1);
        if (horizon < 1)
        {
            throw new CalendarValidationException(
                $"A year of calendar '{label}' is too long to reckon more than one of them in ticks.");
        }

        MaxYear = (int)Math.Min((long)startYear + horizon, int.MaxValue - 1);
        MaxTicks = (ulong)((((long)MaxYear - startYear + 1) * TicksPerYear) - 1);

        if (_weekdays.Length == 0)
        {
            EpochWeekday = 0;
            _anchors = [];
        }
        else
        {
            if (epochWeekday < 1 || epochWeekday > _weekdays.Length)
            {
                throw new CalendarValidationException(
                    $"Calendar '{label}' sets an epoch weekday of {epochWeekday}, outside its week of {_weekdays.Length} days.");
            }

            EpochWeekday = epochWeekday;
            foreach (var month in _months)
            {
                if (month.StartingWeekday is int sw && sw > _weekdays.Length)
                {
                    throw new CalendarValidationException(
                        $"Month '{month.Name}' pins a starting weekday of {sw}, outside the week of {_weekdays.Length} days.");
                }
            }

            _anchors = BuildWeekAnchors(out _weekIsPinned, out _yearAdvance, out _pinnedYearStart);
        }
    }

    /// <summary>Stable identity for this calendar.</summary>
    public Guid Id { get; }

    /// <summary>The campaign this calendar belongs to, when it is scoped to one.</summary>
    public Guid? CampaignId { get; }

    /// <summary>Short name of the calendar.</summary>
    public string Label { get; }

    /// <summary>Longer description, or null.</summary>
    public string? Description { get; }

    /// <summary>Reference link, or null.</summary>
    public string? Url { get; }

    /// <summary>Every month and festival of a year, ordered by <see cref="CalendarMonth.Number"/>.</summary>
    public IReadOnlyList<CalendarMonth> Months { get; }

    /// <summary>The named days of the week, in order. Empty for a calendar with no week.</summary>
    public IReadOnlyList<CalendarWeekday> Weekdays { get; }

    /// <summary>Days in a week, or 0 when the calendar has no week.</summary>
    public int WeekLength => _weekdays.Length;

    /// <summary>Hours in a day.</summary>
    public int HoursInDay { get; }

    /// <summary>Minutes in an hour.</summary>
    public int MinutesInHour { get; }

    /// <summary>The year at tick 0, and the earliest year this calendar can express.</summary>
    public int StartYear { get; }

    /// <summary>The latest year this calendar can express.</summary>
    public int MaxYear { get; }

    /// <summary>The weekday of <see cref="StartYear"/>, day 1, 1-based; 0 when the calendar has no week.</summary>
    public int EpochWeekday { get; }

    /// <summary>Whether holiday months sit outside the weekday cycle.</summary>
    public bool HolidaysBreakWeekCycle { get; }

    /// <summary>Total days in a year. Every year is the same length.</summary>
    public int DaysInYear { get; }

    /// <summary>Ordinary months in a year, not counting intercalary festivals (<c>num_months</c>).</summary>
    public int MonthCount => _countedMonths.Length;

    /// <summary>Minutes in a day.</summary>
    public int MinutesInDay => HoursInDay * MinutesInHour;

    /// <summary>Ticks in an hour.</summary>
    public long TicksPerHour { get; }

    /// <summary>Ticks in a day.</summary>
    public long TicksPerDay { get; }

    /// <summary>Ticks in a year.</summary>
    public long TicksPerYear { get; }

    /// <summary>The largest tick value this calendar can represent.</summary>
    public ulong MaxTicks { get; }

    /// <summary>The first instant of <see cref="StartYear"/>, which is tick 0.</summary>
    public GameDate Epoch => new(this, 0);

    /// <summary>Builds a date from calendar fields.</summary>
    /// <param name="year">Year, from <see cref="StartYear"/> to <see cref="MaxYear"/>.</param>
    /// <param name="month">Month number, 1-based, counting intercalary festivals.</param>
    /// <param name="day">Day within that month, 1-based.</param>
    /// <param name="hour">Hour of day, from 0.</param>
    /// <param name="minute">Minute of hour, from 0.</param>
    /// <param name="round">Round of the minute, 0 to 9.</param>
    /// <param name="turn">Turn of the round, 0 to 99.</param>
    /// <param name="tick">Tick of the turn, 0 to 99.</param>
    /// <exception cref="ArgumentOutOfRangeException">A field falls outside what this calendar allows.</exception>
    public GameDate Date(int year, int month, int day, int hour = 0, int minute = 0, int round = 0, int turn = 0, int tick = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, _months.Length);

        var target = _months[month - 1];
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(day, target.Length);

        return DateFromDayOfYear(year, target.StartDay + day - 1, hour, minute, round, turn, tick);
    }

    /// <summary>Builds a date from a day of the year rather than a month and day.</summary>
    /// <param name="year">Year, from <see cref="StartYear"/> to <see cref="MaxYear"/>.</param>
    /// <param name="dayOfYear">Day of the year, 1-based.</param>
    /// <param name="hour">Hour of day, from 0.</param>
    /// <param name="minute">Minute of hour, from 0.</param>
    /// <param name="round">Round of the minute, 0 to 9.</param>
    /// <param name="turn">Turn of the round, 0 to 99.</param>
    /// <param name="tick">Tick of the turn, 0 to 99.</param>
    public GameDate DateFromDayOfYear(int year, int dayOfYear, int hour = 0, int minute = 0, int round = 0, int turn = 0, int tick = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, StartYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, MaxYear);
        ArgumentOutOfRangeException.ThrowIfLessThan(dayOfYear, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dayOfYear, DaysInYear);
        ArgumentOutOfRangeException.ThrowIfNegative(hour);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(hour, HoursInDay);
        ArgumentOutOfRangeException.ThrowIfNegative(minute);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minute, MinutesInHour);
        ArgumentOutOfRangeException.ThrowIfNegative(round);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(round, RoundsPerMinute);
        ArgumentOutOfRangeException.ThrowIfNegative(turn);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(turn, TurnsPerRound);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tick, TicksPerTurn);

        ulong dayIndex = ((ulong)(year - StartYear) * (uint)DaysInYear) + (uint)(dayOfYear - 1);
        ulong ticks = (dayIndex * (ulong)TicksPerDay)
            + ((ulong)hour * (ulong)TicksPerHour)
            + ((ulong)minute * TicksPerMinute)
            + ((ulong)round * TicksPerRound)
            + ((ulong)turn * TicksPerTurn)
            + (uint)tick;

        return new GameDate(this, ticks);
    }

    /// <summary>Reads a date back from a raw tick count, such as a stored <c>game_time_start</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The tick falls beyond <see cref="MaxTicks"/>.</exception>
    public GameDate FromTicks(ulong ticks)
    {
        if (ticks > MaxTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticks), ticks,
                $"Calendar '{Label}' reckons ticks 0 to {MaxTicks} (years {StartYear} to {MaxYear}).");
        }

        return new GameDate(this, ticks);
    }

    /// <summary>Reads a date back from a signed tick count, as stored in a <c>bigint</c> column.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The tick is negative or beyond <see cref="MaxTicks"/>.</exception>
    public GameDate FromTicks(long ticks)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticks), ticks,
                $"Calendar '{Label}' starts reckoning at year {StartYear}, so a tick count is never negative.");
        }

        return FromTicks((ulong)ticks);
    }

    /// <summary>The month with the given 1-based number, counting intercalary festivals.</summary>
    public CalendarMonth Month(int number)
    {
        int index = number - 1;
        if (index < 0 || index >= _months.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number), number, $"Calendar '{Label}' has {_months.Length} months and festivals.");
        }

        return _months[index];
    }

    /// <summary>Finds a month by name, matching <see cref="CalendarMonth.AltName"/> too, or null if there is none.</summary>
    public CalendarMonth? FindMonth(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _months.FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.AltName, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Finds a weekday by name, or null if there is none.</summary>
    public CalendarWeekday? FindWeekday(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _weekdays.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A span of whole ticks, the smallest unit there is.</summary>
    public GameTimeSpan Ticks(long count) => new(count);

    /// <summary>A span of whole turns.</summary>
    public GameTimeSpan Turns(long count) => new(count * TicksPerTurn);

    /// <summary>A span of whole rounds.</summary>
    public GameTimeSpan Rounds(long count) => new(count * TicksPerRound);

    /// <summary>A span of whole minutes.</summary>
    public GameTimeSpan Minutes(long count) => new(count * TicksPerMinute);

    /// <summary>A span of whole hours, in this calendar's minutes.</summary>
    public GameTimeSpan Hours(long count) => new(count * TicksPerHour);

    /// <summary>A span of whole days, in this calendar's hours.</summary>
    public GameTimeSpan Days(long count) => new(count * TicksPerDay);

    /// <summary>A span of whole weeks, in this calendar's days.</summary>
    /// <exception cref="InvalidOperationException">The calendar has no week.</exception>
    public GameTimeSpan Weeks(long count)
    {
        if (WeekLength == 0)
            throw new InvalidOperationException($"Calendar '{Label}' has no weekdays, so it has no weeks.");
        return new GameTimeSpan(count * WeekLength * TicksPerDay);
    }

    /// <summary>A span of whole years, in this calendar's days.</summary>
    public GameTimeSpan Years(long count) => new(count * TicksPerYear);

    /// <summary>
    /// Renders a span in this calendar's own units, largest first and skipping the empty ones — for
    /// example <c>"2 days, 3 hours, 15 minutes"</c> or <c>"3 rounds, 2 ticks"</c>. Returns
    /// <c>"0 ticks"</c> for an empty span.
    /// </summary>
    public string Describe(GameTimeSpan span)
    {
        long total = span.TotalTicks;
        if (total == 0)
            return "0 ticks";

        var text = new StringBuilder();
        if (total < 0)
            text.Append('-');

        long remaining = Math.Abs(total);
        AppendUnit(text, Split(ref remaining, TicksPerDay), "day");
        AppendUnit(text, Split(ref remaining, TicksPerHour), "hour");
        AppendUnit(text, Split(ref remaining, TicksPerMinute), "minute");
        AppendUnit(text, Split(ref remaining, TicksPerRound), "round");
        AppendUnit(text, Split(ref remaining, TicksPerTurn), "turn");
        AppendUnit(text, remaining, "tick");
        return text.ToString();

        static long Split(ref long remaining, long unit)
        {
            long count = remaining / unit;
            remaining -= count * unit;
            return count;
        }

        static void AppendUnit(StringBuilder text, long value, string unit)
        {
            if (value == 0)
                return;
            if (text.Length > 0 && text[^1] != '-')
                text.Append(", ");
            text.Append(value).Append(' ').Append(unit);
            if (value != 1)
                text.Append('s');
        }
    }

    /// <summary>
    /// Parses a date written in this calendar's round-trip form,
    /// <c>yyyy-MM-ddTHH:mm_RR:TT:KK</c> — the underscore separating the wall clock from the combat
    /// clock. Everything after the day is optional, so <c>1999-01-14</c>, <c>1999-01-14T06:30</c> and
    /// the full <c>1999-01-14T06:30_07:42:03</c> are all accepted, and a space may stand in for the
    /// <c>T</c>.
    /// </summary>
    /// <returns>True when <paramref name="text"/> was a well-formed date in this calendar.</returns>
    public bool TryParse(string? text, out GameDate date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var span = text.AsSpan().Trim();
        int split = span.IndexOfAny(' ', 'T', 't');
        var datePart = split < 0 ? span : span[..split];
        var timePart = split < 0 ? [] : span[(split + 1)..].Trim();

        Span<int> ymd = stackalloc int[3];
        Span<int> clock = stackalloc int[2];
        Span<int> beat = stackalloc int[3];
        if (!SplitInto(datePart, '-', ymd, required: 3))
            return false;

        if (!timePart.IsEmpty)
        {
            int underscore = timePart.IndexOf('_');
            var clockPart = underscore < 0 ? timePart : timePart[..underscore];
            if (!SplitInto(clockPart, ':', clock, required: 2))
                return false;

            // An underscore promises a combat clock after it, so an empty tail is malformed.
            if (underscore >= 0 && !SplitInto(timePart[(underscore + 1)..], ':', beat, required: 1))
                return false;
        }

        (int year, int month, int day) = (ymd[0], ymd[1], ymd[2]);
        if (year < StartYear || year > MaxYear ||
            month < 1 || month > _months.Length ||
            day < 1 || day > _months[month - 1].Length ||
            clock[0] < 0 || clock[0] >= HoursInDay ||
            clock[1] < 0 || clock[1] >= MinutesInHour ||
            beat[0] < 0 || beat[0] >= RoundsPerMinute ||
            beat[1] < 0 || beat[1] >= TurnsPerRound ||
            beat[2] < 0 || beat[2] >= TicksPerTurn)
        {
            return false;
        }

        date = Date(year, month, day, clock[0], clock[1], beat[0], beat[1], beat[2]);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Label;

    /// <summary>Index into <see cref="Months"/> of the month covering a 1-based day of the year.</summary>
    internal int MonthIndexForDayOfYear(int dayOfYear)
    {
        int low = 0;
        int high = _months.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            var month = _months[mid];
            if (dayOfYear < month.StartDay)
                high = mid - 1;
            else if (dayOfYear > month.EndDay)
                low = mid + 1;
            else
                return mid;
        }

        throw new ArgumentOutOfRangeException(
            nameof(dayOfYear), dayOfYear, $"Calendar '{Label}' has {DaysInYear} days in a year.");
    }

    internal CalendarMonth MonthAt(int index) => _months[index];

    /// <summary>The weekday of a day, or null when the day sits outside the weekday cycle.</summary>
    internal CalendarWeekday? WeekdayFor(long yearIndex, int monthIndex, int day)
    {
        if (_weekdays.Length == 0)
            return null;

        var anchor = _anchors[monthIndex];
        if (anchor.Kind == WeekAnchorKind.None)
            return null;

        int start = anchor.Kind == WeekAnchorKind.Absolute
            ? anchor.Value
            : Mod(StartOfYearPosition(yearIndex) + anchor.Value, _weekdays.Length);

        return _weekdays[Mod((long)start + day - 1, _weekdays.Length)];
    }

    /// <summary>Index of the next non-holiday month at or after <paramref name="index"/>, wrapping into the next year.</summary>
    internal int NextCountedMonth(int index, out int yearsForward)
    {
        yearsForward = 0;
        for (int i = index; i < _months.Length; i++)
        {
            if (!_months[i].IsHoliday)
                return i;
        }

        yearsForward = 1;
        return _countedMonths[0];
    }

    /// <summary>Walks <paramref name="steps"/> ordinary months from a counted month, wrapping across years.</summary>
    internal int StepCountedMonths(int index, int steps, out int yearsMoved)
    {
        int position = Array.IndexOf(_countedMonths, index);
        if (position < 0)
            throw new InvalidOperationException("StepCountedMonths must start from an ordinary month.");

        long absolute = (long)position + steps;
        yearsMoved = checked((int)FloorDiv(absolute, _countedMonths.Length));
        return _countedMonths[Mod(absolute, _countedMonths.Length)];
    }

    private int StartOfYearPosition(long yearIndex)
        => _weekIsPinned
            ? _pinnedYearStart
            : Mod((EpochWeekday - 1) + (yearIndex * _yearAdvance), _weekdays.Length);

    /// <summary>
    /// Works out, once, where each month's first day sits in the weekday cycle. A month's anchor is
    /// relative to the start of the year until some month pins its own starting weekday; from that pin on,
    /// positions are absolute and no longer depend on which year it is.
    /// </summary>
    private WeekAnchor[] BuildWeekAnchors(out bool isPinned, out int yearAdvance, out int pinnedYearStart)
    {
        int week = _weekdays.Length;
        var anchors = new WeekAnchor[_months.Length];
        bool absolute = false;
        int position = 0;
        int offset = 0;

        for (int i = 0; i < _months.Length; i++)
        {
            var month = _months[i];
            bool outsideCycle = month.IsHoliday && HolidaysBreakWeekCycle;

            if (month.StartingWeekday is int pin)
            {
                absolute = true;
                position = pin - 1;
                anchors[i] = WeekAnchor.At(position);
                if (!outsideCycle)
                    position = Mod((long)position + month.Length, week);
                continue;
            }

            if (outsideCycle)
            {
                anchors[i] = WeekAnchor.None;
                continue;
            }

            if (absolute)
            {
                anchors[i] = WeekAnchor.At(position);
                position = Mod((long)position + month.Length, week);
            }
            else
            {
                anchors[i] = WeekAnchor.After(offset);
                offset += month.Length;
            }
        }

        isPinned = absolute;
        yearAdvance = absolute ? 0 : Mod(offset, week);
        pinnedYearStart = absolute ? position : 0;
        return anchors;
    }

    private static bool SplitInto(ReadOnlySpan<char> text, char separator, Span<int> fields, int required)
    {
        int count = 0;
        while (!text.IsEmpty && count < fields.Length)
        {
            int next = text.IndexOf(separator);
            var piece = next < 0 ? text : text[..next];
            if (!int.TryParse(piece, out fields[count]))
                return false;
            count++;
            if (next < 0)
                return count >= required;
            text = text[(next + 1)..];
        }

        return text.IsEmpty && count >= required;
    }

    private static void ValidateMonths(string label, CalendarMonth[] months)
    {
        if (months.Length == 0)
            throw new CalendarValidationException($"Calendar '{label}' must have at least one month.");
        if (months.All(m => m.IsHoliday))
            throw new CalendarValidationException($"Calendar '{label}' has only festivals and no ordinary months.");

        int expectedStart = 1;
        for (int i = 0; i < months.Length; i++)
        {
            var month = months[i];
            if (month.Number != i + 1)
            {
                throw new CalendarValidationException(
                    $"Calendar '{label}' numbers its months {string.Join(", ", months.Select(m => m.Number))}; " +
                    "they must run 1, 2, 3 and so on with no gaps or duplicates.");
            }

            if (month.StartDay != expectedStart)
            {
                throw new CalendarValidationException(
                    $"Month '{month.Name}' starts on day {month.StartDay} of the year, but the previous month " +
                    $"leaves off at day {expectedStart - 1}. Months must tile the year with no gaps or overlaps.");
            }

            expectedStart = month.EndDay + 1;
        }
    }

    private static void ValidateWeekdays(string label, CalendarWeekday[] weekdays)
    {
        for (int i = 0; i < weekdays.Length; i++)
        {
            if (weekdays[i].Number != i + 1)
            {
                throw new CalendarValidationException(
                    $"Calendar '{label}' numbers its weekdays {string.Join(", ", weekdays.Select(w => w.Number))}; " +
                    "they must run 1, 2, 3 and so on with no gaps or duplicates.");
            }
        }
    }

    internal static int Mod(long value, int modulus)
    {
        long remainder = value % modulus;
        return (int)(remainder < 0 ? remainder + modulus : remainder);
    }

    internal static long FloorDiv(long dividend, long divisor)
    {
        long quotient = dividend / divisor;
        if (dividend % divisor != 0 && (dividend < 0) != (divisor < 0))
            quotient--;
        return quotient;
    }

    private enum WeekAnchorKind : byte
    {
        None,
        Relative,
        Absolute,
    }

    private readonly struct WeekAnchor
    {
        private WeekAnchor(WeekAnchorKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }

        public WeekAnchorKind Kind { get; }

        public int Value { get; }

        public static WeekAnchor None => new(WeekAnchorKind.None, 0);

        public static WeekAnchor At(int position) => new(WeekAnchorKind.Absolute, position);

        public static WeekAnchor After(int daysIntoYear) => new(WeekAnchorKind.Relative, daysIntoYear);
    }
}
