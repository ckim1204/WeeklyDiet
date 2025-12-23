using System.Globalization;

namespace WeeklyDiet.Api.Utilities;

public static class DateHelpers
{
    public static (int Year, int WeekNumber) GetCurrentIsoWeek(DateTime? now = null)
    {
        var date = now ?? DateTime.Now;
        return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
    }

    public static (int Year, int WeekNumber) GetUpcomingIsoWeek(DateTime? now = null)
    {
        var date = (now ?? DateTime.Now).Date.AddDays(7);
        return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
    }

    public static DateTime GetWeekStartDate(int year, int isoWeek) =>
        ISOWeek.ToDateTime(year, isoWeek, DayOfWeek.Monday).Date;

    public static int NormalizeDayOfWeek(DateTime date) =>
        ((int)date.DayOfWeek + 6) % 7 + 1;
}
