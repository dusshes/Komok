using Komok.Contracts;
using Komok.Data;
using Komok.Models;
using Komok.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace Komok.Endpoints;

public static class CatalogApi
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        api.MapGet("/stores", GetStoresAsync).WithName("GetStores");
        api.MapGet("/catalog", GetCatalogAsync).WithName("GetCatalog");
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
                store.Address,
                store.TimeZoneId,
                OpeningIntervals = store.OpeningIntervals
                    .Select(interval => new { DayOfWeek = (int)interval.DayOfWeek, interval.OpensAt, interval.ClosesAt })
            })
            .ToListAsync(cancellationToken);
        return Results.Ok(stores);
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
                .SetProperty(product => product.BuyerPhone, (string?)null)
                .SetProperty(product => product.BuyerEmail, (string?)null), cancellationToken);

        var products = await db.Products.AsNoTracking()
            .Where(product => product.StoreId == storeId && product.ListingStatus == ListingStatus.OnSale)
            .OrderByDescending(product => product.CreatedAt)
            .Select(product => new
            {
                product.Id,
                product.Title,
                product.Description,
                product.AskingPrice,
                product.Store.CurrencyCode,
                Status = product.InventoryStatus.ToString(),
                product.ReservedUntil
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(products);
    }

    private static async Task<IResult> CreateBookingAsync(
        BookingRequest request,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var inputErrors = new List<(string Field, string Message)>();
        if (request.ArrivalAt <= DateTimeOffset.UtcNow)
            inputErrors.Add(("arrivalTime", "Выберите время прибытия в будущем."));
        if (request.Name?.Length > 160)
            inputErrors.Add(("name", "Имя должно содержать не более 160 символов."));
        if (request.Phone?.Length > 40)
            inputErrors.Add(("phone", "Телефон должен содержать не более 40 символов."));
        if (request.Email?.Length > 320)
            inputErrors.Add(("email", "Email должен содержать не более 320 символов."));
        else if (!string.IsNullOrWhiteSpace(request.Email) && !MailAddress.TryCreate(request.Email, out _))
            inputErrors.Add(("email", "Введите email в корректном формате."));
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
            if (!StoreHours.IsOpenAt(product.Store, request.ArrivalAt))
                return ValidationErrors.BadRequest(("arrivalTime", "Выберите время в рабочие часы магазина."));
            expiresAt = StoreHours.AddWorkingHours(product.Store, now, product.Store.BookingDurationHours);
        }
        catch (TimeZoneNotFoundException)
        {
            return Results.Problem("Для магазина настроен неизвестный часовой пояс.", statusCode: 500);
        }

        if (request.ArrivalAt > expiresAt)
            return ValidationErrors.BadRequest(("arrivalTime", "Выберите время не позже окончания срока брони."));

        var updated = await db.Products
            .Where(item => item.Id == product.Id && item.ListingStatus == ListingStatus.OnSale
                && (item.InventoryStatus == InventoryStatus.Available
                    || (item.InventoryStatus == InventoryStatus.Reserved && item.ReservedUntil <= now)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.InventoryStatus, InventoryStatus.Reserved)
                .SetProperty(item => item.ReservedUntil, expiresAt)
                .SetProperty(item => item.ArrivalAt, request.ArrivalAt)
                .SetProperty(item => item.BuyerName, request.Name)
                .SetProperty(item => item.BuyerPhone, request.Phone)
                .SetProperty(item => item.BuyerEmail, request.Email), cancellationToken);

        return updated == 0
            ? Results.Conflict(new { message = "Товар уже забронирован другим покупателем." })
            : Results.Ok(new { productId = product.Id, expiresAt });
    }
}
