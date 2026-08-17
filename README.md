# GameAccountantCalendar

A C# library for fantasy game calendars: arbitrary months, week lengths, hours in a day, and a tick
resolution fine enough to place a single combatant's action inside a round.

It does dates, arithmetic and formatting. It has no persistence dependencies — the model is plain
classes you can map onto EF Core, Dapper, JSON or anything else — but its shape follows the
`calendars` schema of the Game Accountant database (`gt_calendar`, `gt_month`, `gt_weekday`) so the
two line up without translation.

```
dotnet build GameAccountantCalendar.slnx
dotnet test
```

Target framework is `net10.0`.

## The model

| Type | What it is | Database table |
| --- | --- | --- |
| `GameCalendar` | The shape of a year: months, weekdays, hours, minutes, start year | `calendars.gt_calendar` |
| `CalendarMonth` | One stretch of days — an ordinary month or an intercalary festival | `calendars.gt_month` |
| `CalendarWeekday` | One named day of the week | `calendars.gt_weekday` |
| `GameDate` | A moment: a calendar plus an unsigned tick count | `events.game_event.game_time_start` |
| `GameTimeSpan` | A signed length of time, in ticks | — |

A calendar is immutable and thread-safe once built, and is the factory for every date and span
measured against it.

## How time is counted

Time is a single unsigned 64-bit count of **ticks** since the first instant of the calendar's
`StartYear`. That is exactly the value the `bigint game_time_start` column stores, and
`GameDate.ToInt64()` hands it over.

Below a minute sit the units combat runs on:

| Unit | Holds | Ticks |
| --- | --- | --- |
| Tick | — | 1 |
| Turn | 100 ticks | 100 |
| Round | 100 turns | 10,000 |
| Minute | 10 rounds | 100,000 |

Above a minute, the calendar decides: `MinutesInHour`, `HoursInDay`, and a year made of months that
tile a fixed number of days.

`StartYear` is what keeps the count unsigned. Reckoning begins there, so tick 0 is the first instant
of that year and nothing earlier is representable — set it below the earliest date your campaign
needs to record. The range above it is generous: a calendar can reckon a million years past its start
year, and the whole span still fits inside `long.MaxValue`, so a tick round-trips through a signed
`bigint` column without loss.

## Building a calendar

```csharp
var calendar = new GameCalendarBuilder("Common Reckoning")
    .WithDescription("A plain twelve-month year with five festival days.")
    .WithStartYear(1)
    .WithDay(hours: 24, minutesPerHour: 60)
    .WithWeekdays("Sunsday", "Moonsday", "Fireday", "Watersday", "Woodsday", "Ironsday", "Starsday")
    .AddMonth("Frostwane", days: 30, altName: "Deep Winter")
    .AddMonth("Thawtide", days: 30)
    .AddFestival("Firstplanting")
    .AddMonth("Seedfall", days: 30)
    .Build();
```

The builder works out each month's day-of-year range from the lengths you give it. Building
validates the definition and throws `CalendarValidationException` with a description of what is
wrong — months that overlap or leave a gap, weekday numbers with a hole in them, a week length that
disagrees with the weekday list.

`SampleCalendars.CommonReckoning` is the worked example above, filled out to a full 365-day year.

## Renaming

A calendar is immutable, so renaming returns a new one with the year's shape untouched — the same day
ranges, festivals, weekday pins, start year and identity. Only the words change.

```csharp
var revolutionary = SampleCalendars.CommonReckoning
    .WithLabel("Revolutionary Calendar")
    .WithMonthNames("Nivose", "Pluviose", "Ventose", "Germinal", "Floreal", "Prairial",
                    "Messidor", "Thermidor", "Fructidor", "Vendemiaire", "Brumaire", "Frimaire")
    .WithFestivalNames("Sansculottides", "Vertu", "Genie", "Travail", "Opinion")
    .WithWeekdayNames("Primus", "Secundus", "Tertius", "Quartus", "Quintus", "Sextus", "Septimus");
```

| Method | Takes | Leaves alone |
| --- | --- | --- |
| `WithLabel` | any non-blank string | everything else |
| `WithMonthNames` | `MonthCount` names (12 above) | the festivals |
| `WithFestivalNames` | `FestivalCount` names (5 above) | the ordinary months |
| `WithWeekdayNames` | `WeekLength` names (7 above) | months and festivals |

Months and festivals are renamed independently and in either order — neither call disturbs the
other's entries. `WithMonthNames` also accepts `Months.Count` names (17 for the sample), which
renames every entry in year order, festivals included; it tells the two forms apart by array length.

Any other count throws `CalendarValidationException` naming the counts it would have accepted. Where
a calendar has no festivals, `MonthCount` and `Months.Count` coincide and `WithFestivalNames` takes
an empty array.

Dates don't carry across on their own, because a `GameDate` belongs to the exact calendar that built
it. A tick means the same moment on both, so move one over with `renamed.FromTicks(date.Ticks)`.

## Dates

```csharp
var date = calendar.Date(1999, month: 1, day: 14, hour: 6, minute: 30);

date.Year;          // 1999
date.Month.Name;    // "Frostwane"
date.Day;           // 14
date.DayOfYear;     // 14
date.Weekday?.Name; // "Moonsday"
date.Ticks;         // ulong, since the start year
date.ToInt64();     // the same, for a bigint column

calendar.FromTicks(stored);           // back from a persisted tick
calendar.TryParse("1999-01-14T06:30_07:42:03", out var parsed);
```

Combat-scale fields sit alongside the calendar ones:

```csharp
var initiative = calendar.Date(1999, 1, 14, 6, 30, round: 7, turn: 42, tick: 3);

initiative.Round;   // 7
initiative.Turn;    // 42
initiative.Tick;    // 3
```

## Arithmetic

```csharp
date.AddTicks(1);
date.AddTurns(1);      // the next combatant acts
date.AddRounds(1);     // the top of the next round
date.AddMinutes(10);
date.AddHours(8);      // a long rest
date.AddDays(3);
date.AddWeeks(2);
date.AddMonths(1);
date.AddYears(1);

var travel = later - date;              // GameTimeSpan
calendar.Describe(travel);              // "12 days, 6 hours"
date.DaysUntil(later);                  // 12
date.StartOfDay;                        // midnight
date.StartOfMonth;
date.StartOfYear;
date + calendar.Days(3) + calendar.Hours(4);
```

Dates compare and sort with the usual operators, and implement `IComparable<GameDate>`. Comparing
dates from two different calendars throws, since a tick sits at a different place in each.

Two rules are worth knowing:

- **`AddDays` crosses festivals** like any other day; **`AddMonths` steps over them**, counting only
  ordinary months. Where the target month is shorter the day is clamped, so month arithmetic is not
  always reversible — the same caveat `DateTime.AddMonths` carries.
- **`AddYears` is exact** in both directions, because every year in a calendar is the same length.

## Formatting

`GameDate` implements `IFormattable`. Standard formats are a single character:

| Format | Output |
| --- | --- |
| `d` | `14 Frostwane 1999` |
| `D` | `Moonsday, 14 Frostwane 1999` |
| `t` | `6:30` |
| `T` | `6:30_07:42:03` |
| `f` | `14 Frostwane 1999 6:30` |
| `F` | `Moonsday, 14 Frostwane 1999 6:30` — the default |
| `y` | `Frostwane 1999` |
| `n` | `1999-01-14` |
| `o` | `1999-01-14T06:30_07:42:03` — round-trips through `TryParse` |

Custom patterns are built from specifiers, and anything unrecognised is copied through:

| Specifier | Meaning |
| --- | --- |
| `yyyy` `y` | Year, zero-padded to the number of `y`s |
| `MMMM` `MMM` `MM` `M` | Month name, alternate name, padded number, number |
| `dddd` `ddd` `dd` `d` | Weekday name, short weekday name, padded day, day |
| `DDD` `D` | Day of the year, padded and plain |
| `HH` `H` `mm` `m` | Hour and minute |
| `RR` `R` | Round of the minute |
| `TT` `T` | Turn of the round |
| `KK` `K` | Tick of the turn |

The underscore in the round-trip format divides the wall clock from the combat clock, and is an
ordinary literal. Escape a specifier with `\`, wrap literal text in `'` or `"`, and prefix a lone
specifier with `%` so it is not read as a standard format — the literal `T` is written `\T`.

```csharp
date.ToString("dddd, d MMMM yyyy");   // "Moonsday, 14 Frostwane 1999"
date.ToString("'day' d 'of' MMMM");   // "day 14 of Frostwane"
date.ToString(@"y-MM-dd\THH:mm");      // "1999-01-14T06:30"
```

Two touches are built into the standard date formats: a single-day festival renders without a day
number (`Firstplanting 1999`, not `1 Firstplanting 1999`), and a day with no weekday drops it along
with its comma.

## Festivals and the week

A `CalendarMonth` with `IsHoliday` set is an intercalary festival — a stretch of days sitting between
two months. By default such days have no weekday at all and do not advance the week, which is how
intercalary days conventionally work. `GameDate.Weekday` is therefore nullable.

Pass `holidaysBreakWeekCycle: false` (or `builder.WithHolidaysInWeekCycle()`) to run festivals
through the cycle like ordinary days instead.

A month can also pin its own first weekday with `StartingWeekday`, which re-anchors the cycle from
that point on and holds in every year. When any month pins, `EpochWeekday` no longer applies, since
the pins fix the cycle instead.

## JSON

`CalendarJson` reads and writes documents whose field names are the database column names, so a
document lines up with the rows it came from:

```csharp
string json = CalendarJson.Serialize(calendar);
var restored = CalendarJson.Deserialize(json);

CalendarDocument document = CalendarJson.ToDocument(calendar);   // for a row mapper
GameCalendar back = CalendarJson.FromDocument(document);
```

```json
{
  "label": "Common Reckoning",
  "week_length": 7,
  "num_months": 12,
  "num_hours": 24,
  "num_minutes": 60,
  "start_year": 1,
  "months": [
    { "month_num": 1, "month_name": "Frostwane", "alt_name": "Deep Winter", "is_holiday": false, "start_day": 1, "end_day": 30 }
  ],
  "weekdays": [{ "day_num": 1, "day_name": "Sunsday" }]
}
```

`week_length` and `num_months` are checked against the lists they describe rather than trusted, so a
document that disagrees with itself fails loudly. `start_year`, `epoch_weekday` and
`holidays_break_week_cycle` are library fields with no column behind them.

Serialization is source-generated, so the library works under trimming and native AOT.

## What it deliberately does not do

- **No leap years.** Months are defined by fixed day-of-year ranges, so every year is the same
  length. This matches the schema being modelled; a calendar with a Shieldmeet-style leap day would
  need a year-length rule the schema has no room for.
- **No events, moons, seasons or weather.** Scheduling belongs to whatever owns `game_event`; this
  library gives it the tick to hang events on.
- **No dates before `StartYear`.** That is the price of an unsigned count.

## Licence

MIT.
