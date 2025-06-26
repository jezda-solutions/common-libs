namespace JezdaSolutions.Core.Extensions;

public static class DateTimeExtensions
{
    public static int DaysInYear(this DateOnly date) =>
        DateTime.IsLeapYear(date.Year) ? 366 : 365;

    public static DateTime ToDateTime(this DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue);
}