namespace GameAccountantCalendar;

/// <summary>
/// A length of game time, counted in ticks — 100 to a turn, 10,000 to a round, 100,000 to a minute. Days and
/// hours are deliberately not properties here, because how many ticks those hold is a property of the
/// calendar; use <see cref="GameCalendar.Days"/>, <see cref="GameCalendar.Hours"/> and
/// <see cref="GameCalendar.Describe"/> to move between a span and a calendar's own units.
/// </summary>
/// <remarks>
/// Unlike a <see cref="GameDate"/>, which counts forward from its calendar's start year and is therefore
/// unsigned, a span is signed: it can run backwards.
/// </remarks>
public readonly struct GameTimeSpan : IEquatable<GameTimeSpan>, IComparable<GameTimeSpan>
{
    /// <summary>A span of no time at all.</summary>
    public static readonly GameTimeSpan Zero = new(0);

    /// <summary>Creates a span of the given number of ticks, which may be negative.</summary>
    public GameTimeSpan(long totalTicks) => TotalTicks = totalTicks;

    /// <summary>The length of the span in ticks. Negative for a span that runs backwards.</summary>
    public long TotalTicks { get; }

    /// <summary>The length of the span in whole turns, rounded towards zero.</summary>
    public long TotalTurns => TotalTicks / GameCalendar.TicksPerTurn;

    /// <summary>The length of the span in whole rounds, rounded towards zero.</summary>
    public long TotalRounds => TotalTicks / GameCalendar.TicksPerRound;

    /// <summary>The length of the span in whole minutes, rounded towards zero.</summary>
    public long TotalMinutes => TotalTicks / GameCalendar.TicksPerMinute;

    /// <summary>True when the span runs backwards in time.</summary>
    public bool IsNegative => TotalTicks < 0;

    /// <summary>The same length, always forwards.</summary>
    public GameTimeSpan Absolute => new(Math.Abs(TotalTicks));

    /// <summary>Creates a span of the given number of ticks.</summary>
    public static GameTimeSpan FromTicks(long ticks) => new(ticks);

    /// <summary>Creates a span of the given number of turns.</summary>
    public static GameTimeSpan FromTurns(long turns) => new(turns * GameCalendar.TicksPerTurn);

    /// <summary>Creates a span of the given number of rounds.</summary>
    public static GameTimeSpan FromRounds(long rounds) => new(rounds * GameCalendar.TicksPerRound);

    /// <summary>Creates a span of the given number of minutes.</summary>
    public static GameTimeSpan FromMinutes(long minutes) => new(minutes * GameCalendar.TicksPerMinute);

    /// <inheritdoc />
    public bool Equals(GameTimeSpan other) => TotalTicks == other.TotalTicks;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GameTimeSpan other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => TotalTicks.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(GameTimeSpan other) => TotalTicks.CompareTo(other.TotalTicks);

    /// <summary>Renders the span as a raw tick count. Use <see cref="GameCalendar.Describe"/> for prose.</summary>
    public override string ToString() => $"{TotalTicks} ticks";

#pragma warning disable CS1591
    public static GameTimeSpan operator +(GameTimeSpan left, GameTimeSpan right) => new(left.TotalTicks + right.TotalTicks);

    public static GameTimeSpan operator -(GameTimeSpan left, GameTimeSpan right) => new(left.TotalTicks - right.TotalTicks);

    public static GameTimeSpan operator -(GameTimeSpan value) => new(-value.TotalTicks);

    public static GameTimeSpan operator *(GameTimeSpan value, long factor) => new(value.TotalTicks * factor);

    public static GameTimeSpan operator *(long factor, GameTimeSpan value) => new(value.TotalTicks * factor);

    public static bool operator ==(GameTimeSpan left, GameTimeSpan right) => left.Equals(right);

    public static bool operator !=(GameTimeSpan left, GameTimeSpan right) => !left.Equals(right);

    public static bool operator <(GameTimeSpan left, GameTimeSpan right) => left.TotalTicks < right.TotalTicks;

    public static bool operator >(GameTimeSpan left, GameTimeSpan right) => left.TotalTicks > right.TotalTicks;

    public static bool operator <=(GameTimeSpan left, GameTimeSpan right) => left.TotalTicks <= right.TotalTicks;

    public static bool operator >=(GameTimeSpan left, GameTimeSpan right) => left.TotalTicks >= right.TotalTicks;
#pragma warning restore CS1591

    /// <summary>Named alternative to <c>operator +</c>.</summary>
    public GameTimeSpan Add(GameTimeSpan other) => this + other;

    /// <summary>Named alternative to <c>operator -</c>.</summary>
    public GameTimeSpan Subtract(GameTimeSpan other) => this - other;

    /// <summary>Named alternative to unary <c>operator -</c>.</summary>
    public GameTimeSpan Negate() => -this;

    /// <summary>Named alternative to <c>operator *</c>.</summary>
    public GameTimeSpan Multiply(long factor) => this * factor;
}
