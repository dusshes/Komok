using Komok.Models;

namespace Komok.Services;

public static class StoreHours
{
    public static TimeZoneInfo GetTimeZone(Store store) => TimeZoneInfo.FindSystemTimeZoneById(store.TimeZoneId);

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
