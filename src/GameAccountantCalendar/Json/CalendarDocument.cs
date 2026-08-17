using System.Text.Json.Serialization;

namespace GameAccountantCalendar.Json;

/// <summary>
/// The wire shape of a calendar. Property names match the <c>calendars.gt_calendar</c>,
/// <c>gt_month</c> and <c>gt_weekday</c> columns so a JSON document lines up with the database rows
/// it came from.
/// </summary>
public sealed class CalendarDocument
{
    /// <summary><c>gt_calendar.id</c></summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    /// <summary><c>gt_calendar.campaign_id</c></summary>
    [JsonPropertyName("campaign_id")]
    public Guid? CampaignId { get; set; }

    /// <summary><c>gt_calendar.label</c></summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary><c>gt_calendar.description</c></summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary><c>gt_calendar.url</c></summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary><c>gt_calendar.week_length</c>. Must agree with the number of weekdays when present.</summary>
    [JsonPropertyName("week_length")]
    public int? WeekLength { get; set; }

    /// <summary><c>gt_calendar.num_months</c>, counting ordinary months only. Checked when present.</summary>
    [JsonPropertyName("num_months")]
    public int? NumMonths { get; set; }

    /// <summary><c>gt_calendar.num_hours</c>. Defaults to 24.</summary>
    [JsonPropertyName("num_hours")]
    public int? NumHours { get; set; }

    /// <summary><c>gt_calendar.num_minutes</c>. Defaults to 60.</summary>
    [JsonPropertyName("num_minutes")]
    public int? NumMinutes { get; set; }

    /// <summary>The year at tick 0. Not a database column; defaults to 1.</summary>
    [JsonPropertyName("start_year")]
    public int? StartYear { get; set; }

    /// <summary>Weekday of the start year's first day. Not a database column; defaults to 1.</summary>
    [JsonPropertyName("epoch_weekday")]
    public int? EpochWeekday { get; set; }

    /// <summary>Whether festivals sit outside the week. Not a database column; defaults to true.</summary>
    [JsonPropertyName("holidays_break_week_cycle")]
    public bool? HolidaysBreakWeekCycle { get; set; }

    /// <summary>The months and festivals of a year, from <c>gt_month</c>.</summary>
    [JsonPropertyName("months")]
    public List<MonthDocument> Months { get; set; } = [];

    /// <summary>The days of the week, from <c>gt_weekday</c>.</summary>
    [JsonPropertyName("weekdays")]
    public List<WeekdayDocument> Weekdays { get; set; } = [];
}

/// <summary>The wire shape of one <c>calendars.gt_month</c> row.</summary>
public sealed class MonthDocument
{
    /// <summary><c>gt_month.month_num</c></summary>
    [JsonPropertyName("month_num")]
    public int MonthNum { get; set; }

    /// <summary><c>gt_month.month_name</c></summary>
    [JsonPropertyName("month_name")]
    public string MonthName { get; set; } = string.Empty;

    /// <summary><c>gt_month.alt_name</c></summary>
    [JsonPropertyName("alt_name")]
    public string? AltName { get; set; }

    /// <summary><c>gt_month.is_holiday</c></summary>
    [JsonPropertyName("is_holiday")]
    public bool IsHoliday { get; set; }

    /// <summary><c>gt_month.start_day</c></summary>
    [JsonPropertyName("start_day")]
    public int StartDay { get; set; }

    /// <summary><c>gt_month.end_day</c></summary>
    [JsonPropertyName("end_day")]
    public int EndDay { get; set; }

    /// <summary><c>gt_month.starting_weekday</c></summary>
    [JsonPropertyName("starting_weekday")]
    public int? StartingWeekday { get; set; }
}

/// <summary>The wire shape of one <c>calendars.gt_weekday</c> row.</summary>
public sealed class WeekdayDocument
{
    /// <summary><c>gt_weekday.day_num</c></summary>
    [JsonPropertyName("day_num")]
    public int DayNum { get; set; }

    /// <summary><c>gt_weekday.day_name</c></summary>
    [JsonPropertyName("day_name")]
    public string DayName { get; set; } = string.Empty;
}
