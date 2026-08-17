using System.Globalization;
using System.Text;

namespace GameAccountantCalendar;

/// <summary>
/// Turns a <see cref="GameDate"/> into text. This is the only place date prose is written; everything
/// that renders a date goes through <see cref="GameDate.ToString(string?, IFormatProvider?)"/>.
/// </summary>
/// <remarks>
/// <para>Standard formats, each a single character:</para>
/// <list type="table">
///   <item><term>d</term><description>short date — <c>14 Frostwane 1492</c></description></item>
///   <item><term>D</term><description>long date — <c>Starsday, 14 Frostwane 1492</c></description></item>
///   <item><term>t</term><description>time — <c>6:30</c></description></item>
///   <item><term>T</term><description>time down to the tick — <c>6:30:00:00:00</c></description></item>
///   <item><term>f</term><description>short date and time — <c>14 Frostwane 1492 6:30</c></description></item>
///   <item><term>F</term><description>long date and time, the default — <c>Starsday, 14 Frostwane 1492 6:30</c></description></item>
///   <item><term>y</term><description>month and year — <c>Frostwane 1492</c></description></item>
///   <item><term>n</term><description>numeric date — <c>1492-01-14</c></description></item>
///   <item><term>o</term><description>round-trip, readable by <see cref="GameCalendar.TryParse"/> — <c>1492-01-14T06:30:00:00:00</c></description></item>
/// </list>
/// <para>
/// A single-day festival renders without a day number in the date formats, so an intercalary day reads
/// <c>Firstplanting 1492</c> rather than <c>1 Firstplanting 1492</c>. Where a day has no weekday the long
/// formats drop it along with its comma.
/// </para>
/// <para>Custom patterns are built from these specifiers; anything else is copied through literally:</para>
/// <list type="table">
///   <item><term>yyyy y</term><description>year, zero-padded to the number of <c>y</c>s</description></item>
///   <item><term>MMMM MMM MM M</term><description>month name, alternate name, padded number, number</description></item>
///   <item><term>dddd ddd dd d</term><description>weekday name, short weekday name, padded day, day</description></item>
///   <item><term>DDD D</term><description>day of the year, padded and plain</description></item>
///   <item><term>HH H mm m</term><description>hour and minute, padded and plain</description></item>
///   <item><term>RR R</term><description>round of the minute, 0 to 9</description></item>
///   <item><term>TT T</term><description>turn of the round, 0 to 99</description></item>
///   <item><term>KK K</term><description>tick of the turn, 0 to 99</description></item>
/// </list>
/// <para>
/// Use <c>\</c> to escape a specifier — the literal <c>T</c> in the round-trip pattern is written
/// <c>\T</c> — wrap literal text in <c>'</c> or <c>"</c>, and prefix a lone specifier with <c>%</c> so it
/// is not read as a standard format.
/// </para>
/// </remarks>
public static class GameDateFormatter
{
    /// <summary>The round-trip pattern, <c>yyyy-MM-ddTHH:mm:RR:TT:KK</c>, with the literal T escaped.</summary>
    public const string RoundTripPattern = @"y-MM-dd\THH:mm:RR:TT:KK";

    /// <summary>Renders a date. Null or empty <paramref name="format"/> means the default, <c>"F"</c>.</summary>
    /// <exception cref="FormatException">The format string is not well formed.</exception>
    public static string Format(GameDate date, string? format, IFormatProvider? formatProvider = null)
    {
        if (date.IsEmpty)
            return string.Empty;

        var culture = formatProvider as CultureInfo ?? CultureInfo.CurrentCulture;
        if (string.IsNullOrEmpty(format))
            return Standard(date, 'F', culture);

        if (format.Length == 1)
            return Standard(date, format[0], culture);

        if (format.Length == 2 && format[0] == '%')
            return Custom(date, format[1..], culture);

        return Custom(date, format, culture);
    }

    private static string Standard(GameDate date, char specifier, CultureInfo culture)
    {
        return specifier switch
        {
            'd' => ShortDate(date, culture),
            'D' => LongDate(date, culture),
            't' => Time(date, culture),
            'T' => Custom(date, "H:mm:RR:TT:KK", culture),
            'f' => $"{ShortDate(date, culture)} {Time(date, culture)}",
            'F' => $"{LongDate(date, culture)} {Time(date, culture)}",
            'y' or 'Y' => $"{date.Month.Name} {Number(date.Year, 1, culture)}",
            'n' => Custom(date, "y-MM-dd", culture),
            'o' or 'O' or 'r' or 'R' => Custom(date, RoundTripPattern, culture),
            _ => Custom(date, specifier.ToString(), culture),
        };
    }

    private static string ShortDate(GameDate date, CultureInfo culture)
    {
        var month = date.Month;
        string year = Number(date.Year, 1, culture);
        return month is { IsHoliday: true, Length: 1 }
            ? $"{month.Name} {year}"
            : $"{Number(date.Day, 1, culture)} {month.Name} {year}";
    }

    private static string LongDate(GameDate date, CultureInfo culture)
    {
        string tail = ShortDate(date, culture);
        var weekday = date.Weekday;
        return weekday is null ? tail : $"{weekday.Name}, {tail}";
    }

    private static string Time(GameDate date, CultureInfo culture)
        => $"{Number(date.Hour, 1, culture)}:{Number(date.Minute, PadWidth(date.Calendar.MinutesInHour - 1), culture)}";

    private static string Custom(GameDate date, string format, CultureInfo culture)
    {
        var calendar = date.Calendar;
        var parts = date.Decompose();
        var month = calendar.MonthAt(parts.MonthIndex);
        var weekday = date.Weekday;
        var text = new StringBuilder(format.Length + 16);

        for (int i = 0; i < format.Length;)
        {
            char c = format[i];
            switch (c)
            {
                case '\\':
                    if (i + 1 >= format.Length)
                        throw new FormatException("A format string cannot end with a lone '\\'.");
                    text.Append(format[i + 1]);
                    i += 2;
                    continue;

                case '%':
                    // A no-op marker that lets a single specifier stand on its own.
                    if (i + 1 >= format.Length)
                        throw new FormatException("A format string cannot end with a lone '%'.");
                    i++;
                    continue;

                case '\'':
                case '"':
                {
                    int close = format.IndexOf(c, i + 1);
                    if (close < 0)
                        throw new FormatException($"The literal opened with {c} at position {i} is never closed.");
                    text.Append(format, i + 1, close - i - 1);
                    i = close + 1;
                    continue;
                }
            }

            int run = RunLength(format, i);
            switch (c)
            {
                case 'y':
                    text.Append(Number(parts.Year, run, culture));
                    break;

                case 'M':
                    text.Append(run switch
                    {
                        >= 4 => month.Name,
                        3 => month.AltName ?? Shorten(month.Name),
                        _ => Number(parts.MonthIndex + 1, run, culture),
                    });
                    break;

                case 'd':
                    text.Append(run switch
                    {
                        >= 4 => weekday?.Name ?? string.Empty,
                        3 => weekday is null ? string.Empty : Shorten(weekday.Name),
                        _ => Number(parts.Day, run, culture),
                    });
                    break;

                case 'D':
                    text.Append(Number(
                        parts.DayOfYear,
                        run >= 3 ? PadWidth(calendar.DaysInYear) : run,
                        culture));
                    break;

                case 'H' or 'h':
                    text.Append(Number(date.Hour, run, culture));
                    break;

                case 'm':
                    text.Append(Number(date.Minute, run, culture));
                    break;

                case 'R':
                    text.Append(Number(date.Round, run, culture));
                    break;

                case 'T':
                    text.Append(Number(date.Turn, run, culture));
                    break;

                case 'K' or 'k':
                    text.Append(Number(date.Tick, run, culture));
                    break;

                default:
                    text.Append(c, run);
                    break;
            }

            i += run;
        }

        return text.ToString();
    }

    private static int RunLength(string format, int start)
    {
        char c = format[start];
        int end = start;
        while (end < format.Length && format[end] == c)
            end++;
        return end - start;
    }

    private static string Number(int value, int width, CultureInfo culture)
    {
        if (value < 0)
            return "-" + Math.Abs((long)value).ToString(culture).PadLeft(width, '0');
        return value.ToString(culture).PadLeft(width, '0');
    }

    private static string Shorten(string name) => name.Length <= 3 ? name : name[..3];

    private static int PadWidth(int largestValue)
    {
        int width = 1;
        for (int limit = 10; limit <= largestValue && width < 10; limit *= 10)
            width++;
        return width;
    }
}
