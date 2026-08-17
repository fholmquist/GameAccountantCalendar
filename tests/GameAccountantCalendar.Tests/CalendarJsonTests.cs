using GameAccountantCalendar.Json;
using Xunit;

namespace GameAccountantCalendar.Tests;

public class CalendarJsonTests
{
    [Fact]
    public void ACalendarSurvivesARoundTrip()
    {
        var original = SampleCalendars.CommonReckoning;
        var restored = CalendarJson.Deserialize(CalendarJson.Serialize(original));

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Label, restored.Label);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.DaysInYear, restored.DaysInYear);
        Assert.Equal(original.MonthCount, restored.MonthCount);
        Assert.Equal(original.WeekLength, restored.WeekLength);
        Assert.Equal(original.HoursInDay, restored.HoursInDay);
        Assert.Equal(original.MinutesInHour, restored.MinutesInHour);
        Assert.Equal(original.StartYear, restored.StartYear);
        Assert.Equal(original.EpochWeekday, restored.EpochWeekday);
        Assert.Equal(
            original.Months.Select(m => (m.Number, m.Name, m.AltName, m.IsHoliday, m.StartDay, m.EndDay)),
            restored.Months.Select(m => (m.Number, m.Name, m.AltName, m.IsHoliday, m.StartDay, m.EndDay)));
        Assert.Equal(
            original.Weekdays.Select(w => (w.Number, w.Name)),
            restored.Weekdays.Select(w => (w.Number, w.Name)));
    }

    [Fact]
    public void DatesLandOnTheSameTickAfterARoundTrip()
    {
        var original = SampleCalendars.CommonReckoning;
        var restored = CalendarJson.Deserialize(CalendarJson.Serialize(original));

        var before = original.Date(1999, 7, 15, 6, 30, 7, 42, 3);
        var after = restored.Date(1999, 7, 15, 6, 30, 7, 42, 3);

        Assert.Equal(before.Ticks, after.Ticks);
        Assert.Equal(before.ToString("o"), after.ToString("o"));
        Assert.Equal(before.Weekday?.Name, after.Weekday?.Name);
    }

    [Fact]
    public void FieldNamesMatchTheDatabaseColumns()
    {
        string json = CalendarJson.Serialize(SampleCalendars.CommonReckoning);

        foreach (string column in new[]
                 {
                     "\"label\"", "\"description\"", "\"week_length\"", "\"num_months\"",
                     "\"num_hours\"", "\"num_minutes\"", "\"month_num\"", "\"month_name\"",
                     "\"alt_name\"", "\"is_holiday\"", "\"start_day\"", "\"end_day\"",
                     "\"day_num\"", "\"day_name\"", "\"start_year\"",
                 })
        {
            Assert.Contains(column, json);
        }
    }

    [Fact]
    public void MonthCountIsCheckedAgainstTheMonthsListed()
    {
        var document = CalendarJson.ToDocument(SampleCalendars.CommonReckoning);
        document.NumMonths = 13;

        var error = Assert.Throws<CalendarValidationException>(() => _ = CalendarJson.FromDocument(document));
        Assert.Contains("num_months", error.Message);
    }

    [Fact]
    public void WeekLengthIsCheckedAgainstTheWeekdaysListed()
    {
        var document = CalendarJson.ToDocument(SampleCalendars.CommonReckoning);
        document.WeekLength = 8;

        var error = Assert.Throws<CalendarValidationException>(() => _ = CalendarJson.FromDocument(document));
        Assert.Contains("week_length", error.Message);
    }

    [Fact]
    public void ADocumentCanBeWrittenByHand()
    {
        const string json = """
            {
              "label": "Two Moons",
              "week_length": 4,
              "num_hours": 20,
              "num_minutes": 50,
              "start_year": 700,
              "months": [
                { "month_num": 1, "month_name": "Rise", "start_day": 1, "end_day": 20 },
                { "month_num": 2, "month_name": "Turning", "is_holiday": true, "start_day": 21, "end_day": 21 },
                { "month_num": 3, "month_name": "Fall", "start_day": 22, "end_day": 41 }
              ],
              "weekdays": [
                { "day_num": 1, "day_name": "First" },
                { "day_num": 2, "day_name": "Second" },
                { "day_num": 3, "day_name": "Third" },
                { "day_num": 4, "day_name": "Fourth" }
              ]
            }
            """;

        var calendar = CalendarJson.Deserialize(json);

        Assert.Equal("Two Moons", calendar.Label);
        Assert.Equal(41, calendar.DaysInYear);
        Assert.Equal(2, calendar.MonthCount);
        Assert.Equal(700, calendar.StartYear);
        Assert.Equal(1_000, calendar.MinutesInDay);
        Assert.Null(calendar.Date(700, 2, 1).Weekday);
        Assert.Equal(0UL, calendar.Date(700, 1, 1).Ticks);
    }

    [Fact]
    public void BadJsonIsReportedAsAValidationFailure()
    {
        var error = Assert.Throws<CalendarValidationException>(() => _ = CalendarJson.Deserialize("{ not json"));
        Assert.Contains("well formed", error.Message);
    }
}
