using System;
using System.Globalization;

namespace Jezda.Common.Helpers;

public static class DateTimeOffsetHelper
{
    public static DateTime ParseToDateTime(this string date, string time, string cultureName = "sr-Latn-CS")
    {
        return DateTime.Parse($"{date} {time}", CultureInfo.CreateSpecificCulture(cultureName));
    }

    public static DateTime TryParseExact(this string date, string time)
    {
        var dateTimeString = $"{date} {time}";
        if (!DateTime.TryParseExact(dateTimeString, "dd.MM.yyyy. HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime result))
        {
            throw new ArgumentException($"Can not convert string {dateTimeString} into valid date time");
        }

        return result;
    }

    public static DateTimeOffset ParseToDateTimeOffset(this string date, string time = "00:00:00", string cultureName = "sr-Latn-CS")
    {
        return DateTimeOffset.Parse($"{date} {time}", CultureInfo.CreateSpecificCulture(cultureName));
    }

    public static DateTimeOffset TryParseExactOffset(this string dateTimeString, string time)
    {
        if (!string.IsNullOrEmpty(time) && !dateTimeString.Contains('Z') && !dateTimeString.Contains('+'))
        {
            dateTimeString = $"{dateTimeString.Split(' ')[0]} {time} +00:00";
        }

        // TODO: naci nacin kako da podrzimo vise formata
        string[] formats = {
                "d.MM.yyyy. HH:mm:ss zzz",
                "d.M.yyyy. HH:mm:ss zzz",
                "dd.MM.yyyy. HH:mm:ss zzz",
                "d.MM.yyyy. HH:mm:ss",
                "dd.M.yyyy. HH:mm:ss zzz",
                "dd.MM.yyyy. HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fffZ"
            };

        if (!DateTimeOffset.TryParseExact(dateTimeString, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset result))
        {
            throw new ArgumentException($"Cannot convert string '{dateTimeString}' into valid DateTimeOffset");
        }

        return result;
    }

    public static DateTimeOffset ParseToDateTimeOffsetUtc(this string date, string time = "00:00:00", string cultureName = "sr-Latn-CS")
    {
        return DateTimeOffset.Parse($"{date} {time}", CultureInfo.CreateSpecificCulture(cultureName)).ToUniversalTime();
    }
}
