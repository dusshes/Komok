using Komok.Contracts;
using Komok.Data;
using Komok.Models;
using Komok.Services;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Globalization;

namespace Komok.Endpoints;

public static class CatalogApi
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        api.MapGet("/stores", GetStoresAsync).WithName("GetStores");
        api.MapGet("/catalog", GetCatalogAsync).WithName("GetCatalog");
        api.MapGet("/products/{productId:long}/photos/{photoId:long}", GetCatalogPhotoAsync).WithName("GetCatalogPhoto");
        api.MapGet("/products/{productId:long}/booking-options", GetBookingOptionsAsync).WithName("GetBookingOptions");
        api.MapPost("/bookings", CreateBookingAsync).WithName("CreateBooking");
        return endpoints;
    }

    private static async Task<IResult> GetStoresAsync(KomokDbContext db, CancellationToken cancellationToken)
    {
        var stores = await db.Stores.AsNoTracking()
            .Where(store => store.IsActive)
            .OrderBy(store => store.Name)
            .Select(store => new
            {
                store.Id,
                store.Name,
                store.CurrencyCode,
                store.TimeZoneId,
                CommissionBands = store.CommissionBands.OrderBy(band => band.PriceThreshold)
                    .Select(band => new { band.PriceThreshold, band.Kind, band.Rate })
                    .ToList(),
                Intervals = store.OpeningIntervals
                    .Select(interval => new { DayOfWeek = (int)interval.DayOfWeek, interval.OpensAt, interval.ClosesAt })
            })
            .ToListAsync(cancellationToken);
        return Results.Ok(stores.Select(store => new
        {
            store.Id,
            store.Name,
            store.CurrencyCode,
            TimeZoneId = StoreHours.NormalizeUtcOffset(store.TimeZoneId),
            OpeningIntervals = store.Intervals,
            store.CommissionBands
        }));
    }

    private static async Task<IResult> GetCatalogAsync(int storeId, KomokDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Stores.AnyAsync(store => store.Id == storeId && store.IsActive, cancellationToken))
            return Results.NotFound(new { message = "Магазин не найден." });
        var now = DateTimeOffset.UtcNow;
        await db.Products
            .Where(product => product.StoreId == storeId
                && product.InventoryStatus == InventoryStatus.Reserved
                && product.ReservedUntil <= now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(product => product.InventoryStatus, InventoryStatus.Available)
                .SetProperty(product => product.ReservedUntil, (DateTimeOffset?)null)
                .SetProperty(product => product.ArrivalAt, (DateTimeOffset?)null)
                .SetProperty(product => product.BuyerName, (string?)null)
                .SetProperty(product => product.BuyerMessenger, (string?)null)
                .SetProperty(product => product.BuyerMessengerContact, (string?)null)
                .SetProperty(product => product.BuyerPhone, (string?)null)
                .SetProperty(product => product.BuyerEmail, (string?)null), cancellationToken);

        var categories = await db.Categories.AsNoTracking()
            .Where(category => category.IsActive || db.Products.Any(product => product.CategoryId == category.Id
                && product.StoreId == storeId && product.ListingStatus == ListingStatus.OnSale))
            .OrderBy(category => category.Name)
            .Select(category => new { category.Id, category.Name })
            .ToListAsync(cancellationToken);

        var products = await db.Products.AsNoTracking()
            .Where(product => product.StoreId == storeId && product.ListingStatus == ListingStatus.OnSale)
            .Select(product => new
            {
                product.Id,
                product.CategoryId,
                Category = product.Category.Name,
                product.Title,
                product.Description,
                product.AskingPrice,
                product.PublishedAt,
                product.Store.CurrencyCode,
                Status = product.InventoryStatus.ToString(),
                product.ReservedUntil,
                Photos = product.Photos.OrderBy(photo => photo.SortOrder)
                    .Select(photo => new
                    {
                        photo.Id,
                        ThumbnailUrl = $"/api/products/{product.Id}/photos/{photo.Id}?size=thumbnail",
                        MediumUrl = $"/api/products/{product.Id}/photos/{photo.Id}?size=medium",
                        OriginalUrl = $"/api/products/{product.Id}/photos/{photo.Id}?size=original"
                    })
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new { categories, products });
    }

    private static async Task<IResult> GetCatalogPhotoAsync(
        long productId,
        long photoId,
        string? size,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var photo = await db.ProductPhotos.AsNoTracking()
            .Where(item => item.Id == photoId && item.ProductId == productId
                && item.Product.ListingStatus == ListingStatus.OnSale)
            .Select(item => new { item.ContentType, item.Data })
            .SingleOrDefaultAsync(cancellationToken);
        if (photo is null) return Results.NotFound();
        if (!TryGetPhotoSize(size, out var width, out var height)) return Results.BadRequest(new { message = "Неизвестный размер фотографии." });
        if (width is null) return Results.File(photo.Data, photo.ContentType);

        using var image = Image.Load(photo.Data);
        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(width.Value, height!.Value),
            Mode = ResizeMode.Max
        }));
        await using var output = new MemoryStream();
        image.Save(output, GetEncoder(photo.ContentType));
        return Results.File(output.ToArray(), photo.ContentType);
    }

    private static bool TryGetPhotoSize(string? size, out int? width, out int? height)
    {
        switch (size?.ToLowerInvariant())
        {
            case null:
            case "original":
                width = null;
                height = null;
                return true;
            case "thumbnail":
                width = 480;
                height = 360;
                return true;
            case "medium":
                width = 960;
                height = 720;
                return true;
            default:
                width = null;
                height = null;
                return false;
        }
    }

    private static IImageEncoder GetEncoder(string contentType) => contentType switch
    {
        "image/png" => new PngEncoder(),
        "image/webp" => new WebpEncoder(),
        _ => new JpegEncoder()
    };

    private static async Task<IResult> GetBookingOptionsAsync(
        long productId,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .Include(item => item.Store).ThenInclude(store => store.OpeningIntervals)
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        if (product is null || !product.Store.IsActive || product.ListingStatus != ListingStatus.OnSale)
            return Results.NotFound(new { message = "Товар не найден." });

        var now = DateTimeOffset.UtcNow;
        if (product.InventoryStatus == InventoryStatus.Sold)
            return Results.Conflict(new { message = "Товар уже продан." });
        if (product.InventoryStatus == InventoryStatus.Reserved && product.ReservedUntil > now)
            return Results.Conflict(new { message = "Товар уже забронирован до указанного времени." });
        if (product.Store.OpeningIntervals.Count == 0)
            return Results.Ok(new { address = product.Store.Address, slots = Array.Empty<object>() });

        try
        {
            var expiresAt = StoreHours.AddWorkingHours(product.Store, now, product.Store.BookingDurationHours);
            var zone = StoreHours.GetTimeZone(product.Store);
            var firstDate = TimeZoneInfo.ConvertTime(now, zone).Date;
            var lastDate = TimeZoneInfo.ConvertTime(expiresAt, zone).Date;
            var slots = new List<object>();
            for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                foreach (var interval in product.Store.OpeningIntervals.Where(item => item.DayOfWeek == date.DayOfWeek))
                {
                    var open = date.Add(interval.OpensAt.ToTimeSpan());
                    var close = date.Add(interval.ClosesAt.ToTimeSpan());
                    for (var local = open; local < close; local = local.AddMinutes(30))
                    {
                        var offset = zone.GetUtcOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
                        var arrivalAt = new DateTimeOffset(local, offset);
                        if (arrivalAt <= now || arrivalAt > expiresAt)
                            continue;
                        slots.Add(new
                        {
                            value = arrivalAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                            label = arrivalAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"))
                        });
                    }
                }
            }

            return Results.Ok(new { address = product.Store.Address, slots });
        }
        catch (TimeZoneNotFoundException)
        {
            return Results.Problem("Для магазина настроен неизвестный часовой пояс.", statusCode: 500);
        }
    }

    private static async Task<IResult> CreateBookingAsync(
        BookingRequest request,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var inputErrors = new List<(string Field, string Message)>();
        var arrivalAtUtc = request.ArrivalAt.ToUniversalTime();
        if (arrivalAtUtc <= DateTimeOffset.UtcNow)
            inputErrors.Add(("arrivalAt", "Выберите доступное время визита."));
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 160)
            inputErrors.Add(("name", "Укажите имя длиной не более 160 символов."));
        if (!new[] { "Telegram", "WhatsApp", "MAX", "Zalo" }.Contains(request.Messenger, StringComparer.Ordinal))
            inputErrors.Add(("messenger", "Выберите мессенджер из списка."));
        if (string.IsNullOrWhiteSpace(request.MessengerContact) || request.MessengerContact.Length > 200)
            inputErrors.Add(("messengerContact", "Укажите контакт в мессенджере длиной не более 200 символов."));
        if (inputErrors.Count > 0)
            return ValidationErrors.BadRequest(inputErrors.ToArray());

        var product = await db.Products.Include(item => item.Store)
            .ThenInclude(store => store.OpeningIntervals)
            .SingleOrDefaultAsync(item => item.Id == request.ProductId, cancellationToken);
        if (product is null || !product.Store.IsActive || product.ListingStatus != ListingStatus.OnSale)
            return Results.NotFound(new { message = "Товар не найден." });
        if (product.InventoryStatus == InventoryStatus.Sold)
            return Results.Conflict(new { message = "Товар уже продан." });

        var now = DateTimeOffset.UtcNow;
        if (product.InventoryStatus == InventoryStatus.Reserved && product.ReservedUntil > now)
            return Results.Conflict(new { message = "Товар уже забронирован до указанного времени." });

        if (product.Store.OpeningIntervals.Count == 0)
            return Results.Conflict(new { message = "Магазин пока не настроил расписание работы." });

        DateTimeOffset expiresAt;
        try
        {
            if (!StoreHours.IsOpenAt(product.Store, arrivalAtUtc))
                return ValidationErrors.BadRequest(("arrivalAt", "Выберите доступное время визита."));
            expiresAt = StoreHours.AddWorkingHours(product.Store, now, product.Store.BookingDurationHours).ToUniversalTime();
        }
        catch (TimeZoneNotFoundException)
        {
            return Results.Problem("Для магазина настроен неизвестный часовой пояс.", statusCode: 500);
        }

        if (arrivalAtUtc > expiresAt || !IsSlotBoundary(product.Store, arrivalAtUtc))
            return ValidationErrors.BadRequest(("arrivalAt", "Выберите доступное время визита."));

        var updated = await db.Products
            .Where(item => item.Id == product.Id && item.ListingStatus == ListingStatus.OnSale
                && (item.InventoryStatus == InventoryStatus.Available
                    || (item.InventoryStatus == InventoryStatus.Reserved && item.ReservedUntil <= now)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.InventoryStatus, InventoryStatus.Reserved)
                .SetProperty(item => item.ReservedUntil, expiresAt)
                .SetProperty(item => item.ArrivalAt, arrivalAtUtc)
                .SetProperty(item => item.BuyerName, request.Name.Trim())
                .SetProperty(item => item.BuyerMessenger, request.Messenger)
                .SetProperty(item => item.BuyerMessengerContact, request.MessengerContact.Trim()), cancellationToken);

        return updated == 0
            ? Results.Conflict(new { message = "Товар уже забронирован другим покупателем." })
            : Results.Ok(new { productId = product.Id, expiresAt });
    }

    private static bool IsSlotBoundary(Store store, DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, StoreHours.GetTimeZone(store));
        var time = TimeOnly.FromDateTime(local.DateTime);
        return store.OpeningIntervals.Any(interval =>
        {
            if (interval.DayOfWeek != local.DayOfWeek || time < interval.OpensAt || time >= interval.ClosesAt)
                return false;
            var minutesFromOpen = (int)(time.ToTimeSpan() - interval.OpensAt.ToTimeSpan()).TotalMinutes;
            return minutesFromOpen % 30 == 0;
        });
    }
}
