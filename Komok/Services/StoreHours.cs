using Komok.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Komok.Services;

public static class StoreHours
{
    public static TimeZoneInfo GetTimeZone(Store store)
    {
        var offset = ParseUtcOffset(store.TimeZoneId);
        if (offset is null)
        {
            // Old stores may still have an IANA/Windows time-zone ID. Keep their
            // current base offset, but stop applying seasonal clock changes.
            offset = TimeZoneInfo.FindSystemTimeZoneById(store.TimeZoneId).BaseUtcOffset;
        }

        var id = FormatUtcOffset(offset.Value);
        return TimeZoneInfo.CreateCustomTimeZone(id, offset.Value, id, id);
    }

    public static bool IsValidUtcOffset(string value) => ParseUtcOffset(value) is not null;

    public static string NormalizeUtcOffset(string value)
    {
        var offset = ParseUtcOffset(value) ?? TimeZoneInfo.FindSystemTimeZoneById(value).BaseUtcOffset;
        return FormatUtcOffset(offset);
    }

    private static TimeSpan? ParseUtcOffset(string value)
    {
        var match = Regex.Match(value, @"^(?<sign>[+-])(?<hours>\d{2}):(?<minutes>\d{2})$");
        if (!match.Success
            || !int.TryParse(match.Groups["hours"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(match.Groups["minutes"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || minutes >= 60)
            return null;
        var offset = TimeSpan.FromMinutes((hours * 60 + minutes) * (match.Groups["sign"].Value == "-" ? -1 : 1));
        var totalMinutes = (int)offset.TotalMinutes;
        return totalMinutes is >= -720 and <= 840 && totalMinutes % 15 == 0 ? offset : null;
    }

    private static string FormatUtcOffset(TimeSpan offset) =>
        $"{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs((int)offset.TotalHours):00}:{Math.Abs(offset.Minutes):00}";

    public static bool IsOpenAt(Store store, DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, GetTimeZone(store));
        var time = TimeOnly.FromDateTime(local.DateTime);
        return store.OpeningIntervals.Any(interval =>
            interval.DayOfWeek == local.DayOfWeek && time >= interval.OpensAt && time < interval.ClosesAt);
    }

    public static DateTimeOffset AddWorkingHours(Store store, DateTimeOffset start, int hours)
    {
        if (hours <= 0)
            throw new ArgumentOutOfRangeException(nameof(hours));

        var zone = GetTimeZone(store);
        var localCursor = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        var remaining = TimeSpan.FromHours(hours);

        for (var day = 0; day < 3660; day++)
        {
            var date = localCursor.Date.AddDays(day == 0 ? 0 : 1);
            var intervals = store.OpeningIntervals
                .Where(interval => interval.DayOfWeek == date.DayOfWeek)
                .OrderBy(interval => interval.OpensAt);

            foreach (var interval in intervals)
            {
                var open = date.Add(interval.OpensAt.ToTimeSpan());
                var close = date.Add(interval.ClosesAt.ToTimeSpan());
                var availableFrom = day == 0 && localCursor > open ? localCursor : open;
                if (availableFrom >= close)
                    continue;

                var available = close - availableFrom;
                if (remaining <= available)
                {
                    var result = DateTime.SpecifyKind(availableFrom.Add(remaining), DateTimeKind.Unspecified);
                    return new DateTimeOffset(result, zone.GetUtcOffset(result));
                }

                remaining -= available;
            }
        }

        throw new InvalidOperationException("The store has no usable opening intervals.");
    }
}
