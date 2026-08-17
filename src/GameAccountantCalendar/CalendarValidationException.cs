namespace GameAccountantCalendar;

/// <summary>
/// Thrown when a calendar definition does not describe a usable year — months that overlap or
/// leave gaps, a week length that disagrees with the weekday list, and so on.
/// </summary>
public sealed class CalendarValidationException : Exception
{
    /// <summary>Creates the exception with a description of what is wrong with the definition.</summary>
    public CalendarValidationException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with a description and an underlying cause.</summary>
    public CalendarValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
