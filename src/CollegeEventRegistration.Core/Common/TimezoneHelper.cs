namespace CollegeEventRegistration.Core.Common;

/// <summary>
/// Helper for handling consistent UTC storage and Indian Standard Time (Asia/Kolkata, UTC+05:30) display conversions.
/// </summary>
public static class TimezoneHelper
{
    private static readonly Lazy<TimeZoneInfo> KolkataTimeZoneLazy = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback to custom UTC+05:30 if neither is found
                return TimeZoneInfo.CreateCustomTimeZone(
                    "IST",
                    TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(30)),
                    "Indian Standard Time",
                    "Indian Standard Time");
            }
        }
    });

    public static TimeZoneInfo KolkataTimeZone => KolkataTimeZoneLazy.Value;

    /// <summary>
    /// Converts a UTC DateTime to Indian Standard Time (Asia/Kolkata).
    /// </summary>
    public static DateTime ToKolkataTime(this DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
            : utcDateTime.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(utc, KolkataTimeZone);
    }

    /// <summary>
    /// Converts an Indian Standard Time DateTime to UTC DateTime for consistent database storage.
    /// </summary>
    public static DateTime ToUtcFromKolkata(this DateTime kolkataDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(kolkataDateTime, KolkataTimeZone);
    }

    /// <summary>
    /// Formats a UTC DateTime for Indian Standard Time display (e.g. "15 Nov 2026, 10:30 AM IST").
    /// </summary>
    public static string FormatKolkata(this DateTime utcDateTime, string format = "dd MMM yyyy, hh:mm tt")
    {
        return $"{utcDateTime.ToKolkataTime().ToString(format)} IST";
    }
}
