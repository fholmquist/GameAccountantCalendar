using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameAccountantCalendar.Json;

/// <summary>
/// Reads and writes calendars as JSON whose fields carry the same names as the database columns,
/// so a document can be handed straight to a row mapper and back.
/// </summary>
public static class CalendarJson
{
    private static readonly CalendarJsonContext Indented = new(
        new JsonSerializerOptions(CalendarJsonContext.Default.Options) { WriteIndented = true });

    /// <summary>Renders a calendar as indented JSON.</summary>
    public static string Serialize(GameCalendar calendar)
        => JsonSerializer.Serialize(ToDocument(calendar), Indented.CalendarDocument);

    /// <summary>Reads a calendar back from JSON.</summary>
    /// <exception cref="CalendarValidationException">The document is missing or does not describe a usable year.</exception>
    public static GameCalendar Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        CalendarDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, CalendarJsonContext.Default.CalendarDocument);
        }
        catch (JsonException ex)
        {
            throw new CalendarValidationException("The calendar JSON is not well formed.", ex);
        }

        if (document is null)
            throw new CalendarValidationException("The calendar JSON was empty.");

        return FromDocument(document);
    }

    /// <summary>Projects a calendar onto the database-shaped document type.</summary>
    public static CalendarDocument ToDocument(GameCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return new CalendarDocument
        {
            Id = calendar.Id,
            CampaignId = calendar.CampaignId,
            Label = calendar.Label,
            Description = calendar.Description,
            Url = calendar.Url,
            WeekLength = calendar.WeekLength,
            NumMonths = calendar.MonthCount,
            NumHours = calendar.HoursInDay,
            NumMinutes = calendar.MinutesInHour,
            StartYear = calendar.StartYear,
            EpochWeekday = calendar.EpochWeekday == 0 ? null : calendar.EpochWeekday,
            HolidaysBreakWeekCycle = calendar.HolidaysBreakWeekCycle,
            Months = [.. calendar.Months.Select(m => new MonthDocument
            {
                MonthNum = m.Number,
                MonthName = m.Name,
                AltName = m.AltName,
                IsHoliday = m.IsHoliday,
                StartDay = m.StartDay,
                EndDay = m.EndDay,
                StartingWeekday = m.StartingWeekday,
            })],
            Weekdays = [.. calendar.Weekdays.Select(w => new WeekdayDocument
            {
                DayNum = w.Number,
                DayName = w.Name,
                AltName = w.AltName,
            })],
        };
    }

    /// <summary>Builds a calendar from the database-shaped document type.</summary>
    /// <exception cref="CalendarValidationException">The document does not describe a usable year.</exception>
    public static GameCalendar FromDocument(CalendarDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var weekdays = document.Weekdays
            .Select(w => new CalendarWeekday(w.DayNum, w.DayName, w.AltName))
            .ToList();

        if (document.WeekLength is int declared && declared != weekdays.Count)
        {
            throw new CalendarValidationException(
                $"Calendar '{document.Label}' declares a week_length of {declared} but lists {weekdays.Count} weekdays.");
        }

        var months = document.Months
            .Select(m => new CalendarMonth(
                m.MonthNum,
                m.MonthName,
                m.StartDay,
                m.EndDay,
                m.AltName,
                m.IsHoliday,
                m.StartingWeekday))
            .ToList();

        var calendar = new GameCalendar(
            document.Label,
            months,
            weekdays,
            document.NumHours ?? 24,
            document.NumMinutes ?? 60,
            document.StartYear ?? 1,
            document.Description,
            document.Url,
            document.EpochWeekday ?? 1,
            document.HolidaysBreakWeekCycle ?? true,
            document.Id,
            document.CampaignId);

        if (document.NumMonths is int expected && expected != calendar.MonthCount)
        {
            throw new CalendarValidationException(
                $"Calendar '{document.Label}' declares num_months as {expected} but lists {calendar.MonthCount} " +
                "ordinary months. Festivals are not counted.");
        }

        return calendar;
    }
}

/// <summary>Source-generated serialization metadata, so the library works under trimming and native AOT.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CalendarDocument))]
public sealed partial class CalendarJsonContext : JsonSerializerContext
{
}
