using Xunit;

namespace GameAccountantCalendar.Tests;

/// <summary>The sub-minute units: 10 rounds to a minute, 100 turns to a round, 100 ticks to a turn.</summary>
public class CombatTimeTests
{
    private static readonly GameCalendar Calendar = SampleCalendars.CommonReckoning;

    [Fact]
    public void TheUnitsNestTenHundredHundred()
    {
        Assert.Equal(100, GameCalendar.TicksPerTurn);
        Assert.Equal(100, GameCalendar.TurnsPerRound);
        Assert.Equal(10, GameCalendar.RoundsPerMinute);
        Assert.Equal(10_000, GameCalendar.TicksPerRound);
        Assert.Equal(100_000, GameCalendar.TicksPerMinute);
    }

    [Fact]
    public void EachUnitRollsIntoTheNext()
    {
        var start = Calendar.Date(1999, 1, 1, 0, 0);

        Assert.Equal(start.AddTurns(1), start.AddTicks(GameCalendar.TicksPerTurn));
        Assert.Equal(start.AddRounds(1), start.AddTurns(GameCalendar.TurnsPerRound));
        Assert.Equal(start.AddMinutes(1), start.AddRounds(GameCalendar.RoundsPerMinute));
        Assert.Equal(start.AddHours(1), start.AddMinutes(Calendar.MinutesInHour));
        Assert.Equal(start.AddDays(1), start.AddHours(Calendar.HoursInDay));
    }

    [Fact]
    public void TicksCarryUpThroughTurnsAndRounds()
    {
        var end = Calendar.Date(1999, 1, 1, 6, 30, 9, 99, 99);
        var next = end.AddTicks(1);

        Assert.Equal((6, 31, 0, 0, 0), (next.Hour, next.Minute, next.Round, next.Turn, next.Tick));
        Assert.Equal(end, next.AddTicks(-1));
    }

    [Fact]
    public void ATurnRollsIntoTheNextRound()
    {
        var lastTurn = Calendar.Date(1999, 1, 1, 6, 30, 3, 99, 0);
        var next = lastTurn.AddTurns(1);

        Assert.Equal(4, next.Round);
        Assert.Equal(0, next.Turn);
    }

    [Fact]
    public void CombatFieldsReadBackExactly()
    {
        var date = Calendar.Date(1999, 7, 15, 6, 30, 7, 42, 3);

        Assert.Equal(7, date.Round);
        Assert.Equal(42, date.Turn);
        Assert.Equal(3, date.Tick);
        Assert.Equal((6 * 60) + 30, date.MinuteOfDay);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 100, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 100)]
    public void CombatFieldsAreRangeChecked(int round, int turn, int tick)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = Calendar.Date(1999, 1, 1, 0, 0, round, turn, tick));

    [Fact]
    public void SpansDescribeThemselvesDownToTheTick()
    {
        Assert.Equal("0 ticks", Calendar.Describe(GameTimeSpan.Zero));
        Assert.Equal("1 tick", Calendar.Describe(Calendar.Ticks(1)));
        Assert.Equal("1 turn", Calendar.Describe(Calendar.Turns(1)));
        Assert.Equal("3 rounds, 2 turns, 1 tick", Calendar.Describe(Calendar.Rounds(3) + Calendar.Turns(2) + Calendar.Ticks(1)));
        Assert.Equal("1 minute", Calendar.Describe(Calendar.Rounds(10)));
        Assert.Equal("1 day, 2 hours, 5 minutes", Calendar.Describe(Calendar.Days(1) + Calendar.Hours(2) + Calendar.Minutes(5)));
        Assert.Equal("-3 rounds", Calendar.Describe(Calendar.Rounds(-3)));
    }

    [Fact]
    public void SpansConvertBetweenUnits()
    {
        var span = GameTimeSpan.FromMinutes(2);

        Assert.Equal(200_000, span.TotalTicks);
        Assert.Equal(2_000, span.TotalTurns);
        Assert.Equal(20, span.TotalRounds);
        Assert.Equal(2, span.TotalMinutes);
        Assert.Equal(GameTimeSpan.FromRounds(20), span);
        Assert.Equal(GameTimeSpan.FromTurns(2_000), span);
        Assert.Equal(GameTimeSpan.FromTicks(200_000), span);
    }

    [Fact]
    public void AnInitiativeOrderFitsInsideOneRound()
    {
        // Twelve combatants each taking a turn, then the round ticks over.
        var roundStart = Calendar.Date(1999, 1, 1, 12, 0);
        var afterEveryone = roundStart;
        for (int combatant = 0; combatant < 12; combatant++)
            afterEveryone = afterEveryone.AddTurns(1);

        Assert.Equal(0, afterEveryone.Round);
        Assert.Equal(12, afterEveryone.Turn);
        Assert.Equal(roundStart.AddRounds(1), roundStart.AddTurns(GameCalendar.TurnsPerRound));
    }
}
