using Komok.Contracts;
using Komok.Data;
using Komok.Models;
using Komok.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Komok.Endpoints;

public static class ListingsApi
{
    public static IEndpointRouteBuilder MapListingsApi(this IEndpointRouteBuilder endpoints)
    {
        var seller = endpoints.MapGroup("/api/listings").RequireAuthorization().WithTags("Listings");
        seller.MapPost("/", CreateAsync);
        seller.MapGet("/mine", MineAsync);
        seller.MapPost("/{id:long}/delivery", MarkDeliveryAsync);
        seller.MapPost("/{id:long}/cancel", CancelAsync);

        var admin = endpoints.MapGroup("/api/admin/products").RequireAuthorization().WithTags("Product administration");
        admin.MapGet("/", ForStoreAsync);
        admin.MapPost("/{id:long}/review", ReviewAsync);
        admin.MapPost("/{id:long}/delivery-received", DeliveryReceivedAsync);
        admin.MapPost("/{id:long}/sold", MarkSoldAsync);
        admin.MapPost("/{id:long}/payout", MarkPayoutAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateListingRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var inputErrors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 160)
            inputErrors.Add(("title", "Укажите название длиной не более 160 символов."));
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 5000)
            inputErrors.Add(("description", "Укажите описание длиной не более 5000 символов."));
        if (request.AskingPrice <= 0)
            inputErrors.Add(("askingPrice", "Введите цену больше нуля."));
        if (inputErrors.Count > 0)
            return ValidationErrors.BadRequest(inputErrors.ToArray());

        var seller = await users.GetUserAsync(principal);
        if (seller is null) return Results.Unauthorized();
        var store = await db.Stores.Include(item => item.CommissionBands)
            .SingleOrDefaultAsync(item => item.Id == request.StoreId && item.IsActive, cancellationToken);
        if (store is null) return ValidationErrors.BadRequest(("storeId", "Выберите действующий магазин."));
        var band = store.CommissionBands.Where(item => item.PriceThreshold <= request.AskingPrice)
            .OrderByDescending(item => item.PriceThreshold).FirstOrDefault();
        if (band is null)
            return ValidationErrors.BadRequest(("askingPrice", "Цена товара ниже минимального порога выбранного магазина."));

        var product = new Product
        {
            StoreId = store.Id,
            SellerId = seller.Id,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            AskingPrice = request.AskingPrice,
            CommissionKind = band.Kind,
            CommissionRate = band.Rate,
            ListingStatus = ListingStatus.Submitted,
            InventoryStatus = InventoryStatus.NotReceived,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        QueueStatusEmail(db, seller, "Заявка принята", $"Заявка «{product.Title}» отправлена администратору магазина «{store.Name}».");
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/listings/{product.Id}", new { product.Id, Status = product.ListingStatus.ToString() });
    }

    private static async Task<IResult> MineAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var seller = await users.GetUserAsync(principal);
        if (seller is null) return Results.Unauthorized();
        var listings = await db.Products.AsNoTracking().Where(product => product.SellerId == seller.Id)
            .OrderByDescending(product => product.CreatedAt)
            .Select(product => new
            {
                product.Id,
                product.Title,
                product.Description,
                product.AskingPrice,
                product.StoreId,
                Store = product.Store.Name,
                product.ListingStatus,
                product.InventoryStatus,
                product.ReservedUntil,
                product.ArrivalAt,
                TimeZoneId = StoreHours.NormalizeUtcOffset(product.Store.TimeZoneId),
                product.SoldPrice,
                product.PayoutAmount,
                product.PayoutStatus,
                Currency = product.Store.CurrencyCode
            }).ToListAsync(cancellationToken);
        return Results.Ok(listings);
    }

    private static async Task<IResult> MarkDeliveryAsync(
        long id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var seller = await users.GetUserAsync(principal);
        if (seller is null) return Results.Unauthorized();
        var product = await db.Products.Include(item => item.Store)
            .SingleOrDefaultAsync(item => item.Id == id && item.SellerId == seller.Id, cancellationToken);
        if (product is null) return Results.NotFound();
        if (product.ListingStatus != ListingStatus.AwaitingDelivery)
            return Results.Conflict(new { message = "Доставку можно отметить только после согласования заявки." });
        product.ListingStatus = ListingStatus.Delivery;
        QueueStatusEmail(db, seller, "Товар передан для доставки", $"Вы отметили передачу товара «{product.Title}» в магазин «{product.Store.Name}».");
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { product.Id, Status = product.ListingStatus.ToString() });
    }

    private static async Task<IResult> CancelAsync(
        long id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var seller = await users.GetUserAsync(principal);
        if (seller is null) return Results.Unauthorized();
        var product = await db.Products.Include(item => item.Store)
            .SingleOrDefaultAsync(item => item.Id == id && item.SellerId == seller.Id, cancellationToken);
        if (product is null) return Results.NotFound();
        if (product.ListingStatus != ListingStatus.AwaitingDelivery)
            return Results.Conflict(new { message = "Отменить можно только заявку, ожидающую доставки." });
        product.ListingStatus = ListingStatus.Cancelled;
        QueueStatusEmail(db, seller, "Заявка отменена", $"Заявка «{product.Title}» отменена.");
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { product.Id, Status = product.ListingStatus.ToString() });
    }

    private static async Task<IResult> ForStoreAsync(
        int storeId,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var admin = await users.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();
        if (!await AdminApi.CanManageStoreAsync(principal, admin.Id, storeId, db, cancellationToken)) return Results.Forbid();
        var products = await db.Products.AsNoTracking().Where(item => item.StoreId == storeId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Description,
                item.AskingPrice,
                item.CommissionKind,
                item.CommissionRate,
                item.ListingStatus,
                item.InventoryStatus,
                item.CreatedAt,
                item.ReservedUntil,
                SellerEmail = item.Seller.Email,
                SellerMessenger = item.Seller.Messenger,
                SellerMessengerContact = item.Seller.MessengerContact,
                SellerPayoutDetails = item.Seller.PayoutDetails,
                item.ArrivalAt,
                item.BuyerName,
                item.BuyerMessenger,
                item.BuyerMessengerContact,
                TimeZoneId = StoreHours.NormalizeUtcOffset(item.Store.TimeZoneId),
                item.SoldPrice,
                item.PayoutAmount,
                item.PayoutStatus,
                item.Store.CurrencyCode
            }).ToListAsync(cancellationToken);
        return Results.Ok(products);
    }

    private static async Task<IResult> ReviewAsync(
        long id,
        ReviewListingRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var admin = await users.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();
        var product = await db.Products.Include(item => item.Store).ThenInclude(store => store.CommissionBands)
            .Include(item => item.Seller)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return Results.NotFound();
        if (!await AdminApi.CanManageStoreAsync(principal, admin.Id, product.StoreId, db, cancellationToken)) return Results.Forbid();
        if (product.ListingStatus is not (ListingStatus.Submitted or ListingStatus.NeedsRevision))
            return Results.Conflict(new { message = "Эта заявка уже рассмотрена." });

        switch (request.Action.ToLowerInvariant())
        {
            case "reject":
                product.ListingStatus = ListingStatus.Rejected;
                QueueStatusEmail(db, product.Seller, "Заявка отклонена", $"Заявка «{product.Title}» отклонена магазином «{product.Store.Name}».");
                break;
            case "revision":
                product.ListingStatus = ListingStatus.NeedsRevision;
                QueueStatusEmail(db, product.Seller, "Заявка возвращена на доработку", $"Администратор вернул заявку «{product.Title}» на доработку. Свяжитесь с магазином для уточнений.");
                break;
            case "approve":
                var reviewErrors = new List<(string Field, string Message)>();
                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 160)
                    reviewErrors.Add(("title", "Укажите название длиной не более 160 символов."));
                if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 5000)
                    reviewErrors.Add(("description", "Укажите описание длиной не более 5000 символов."));
                if (request.AskingPrice is null or <= 0)
                    reviewErrors.Add(("askingPrice", "Введите цену больше нуля."));
                if (reviewErrors.Count > 0)
                    return ValidationErrors.BadRequest(reviewErrors.ToArray());
                if (request.CommissionPercent is < 0 or > 100)
                    return ValidationErrors.BadRequest(("commissionPercent", "Процент комиссии должен быть от 0 до 100."));
                var band = product.Store.CommissionBands.Where(item => item.PriceThreshold <= request.AskingPrice)
                    .OrderByDescending(item => item.PriceThreshold).FirstOrDefault();
                if (band is null)
                    return ValidationErrors.BadRequest(("askingPrice", "Цена ниже минимального порога комиссии магазина."));
                product.Title = request.Title!.Trim();
                product.Description = request.Description!.Trim();
                product.AskingPrice = request.AskingPrice!.Value;
                product.CommissionKind = request.CommissionPercent.HasValue ? CommissionKind.Percent : band.Kind;
                product.CommissionRate = request.CommissionPercent ?? band.Rate;
                product.ListingStatus = ListingStatus.AwaitingDelivery;
                QueueStatusEmail(db, product.Seller, "Заявка одобрена — ожидается доставка",
                    $"Заявка «{product.Title}» одобрена. Передайте товар в магазин по адресу: {product.Store.Address}.");
                break;
            default:
                return Results.BadRequest(new { message = "Действие должно быть reject, revision или approve." });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { product.Id, Status = product.ListingStatus.ToString() });
    }

    private static async Task<IResult> DeliveryReceivedAsync(
        long id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var admin = await users.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();
        var product = await db.Products.Include(item => item.Seller).Include(item => item.Store)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return Results.NotFound();
        if (!await AdminApi.CanManageStoreAsync(principal, admin.Id, product.StoreId, db, cancellationToken)) return Results.Forbid();
        if (product.ListingStatus != ListingStatus.Delivery)
            return Results.Conflict(new { message = "Подтвердить получение можно после отметки продавца о доставке." });
        product.ListingStatus = ListingStatus.OnSale;
        product.InventoryStatus = InventoryStatus.Available;
        QueueStatusEmail(db, product.Seller, "Товар принят магазином", $"Товар «{product.Title}» принят и размещён на продаже.");
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { product.Id, Status = product.ListingStatus.ToString() });
    }

    private static async Task<IResult> MarkSoldAsync(
        long id,
        MarkSoldRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var admin = await users.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();
        var product = await db.Products.Include(item => item.Seller).Include(item => item.Store)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return Results.NotFound();
        if (!await AdminApi.CanManageStoreAsync(principal, admin.Id, product.StoreId, db, cancellationToken)) return Results.Forbid();
        if (product.ListingStatus != ListingStatus.OnSale || product.InventoryStatus == InventoryStatus.Sold)
            return Results.Conflict(new { message = "Продать можно только товар, принятый магазином и находящийся на продаже." });
        if (request.SoldPrice <= 0)
            return ValidationErrors.BadRequest(("soldPrice", "Введите цену продажи больше нуля."));

        var rate = product.CommissionRate ?? 0;
        var commission = product.CommissionKind == CommissionKind.Percent ? request.SoldPrice * rate / 100 : rate;
        if (commission > request.SoldPrice)
            return ValidationErrors.BadRequest(("soldPrice", "Цена продажи должна быть выше суммы комиссии."));
        product.InventoryStatus = InventoryStatus.Sold;
        product.ReservedUntil = null;
        product.SoldPrice = request.SoldPrice;
        product.CommissionAmount = decimal.Round(commission, 2, MidpointRounding.AwayFromZero);
        product.PayoutAmount = request.SoldPrice - product.CommissionAmount;
        product.PayoutStatus = PayoutStatus.Pending;
        QueueStatusEmail(db, product.Seller, "Товар продан", $"Товар «{product.Title}» продан. К выплате: {product.PayoutAmount:0.00} {product.Store.CurrencyCode}.");
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { product.Id, product.SoldPrice, product.CommissionAmount, product.PayoutAmount, product.PayoutStatus });
    }

    private static async Task<IResult> MarkPayoutAsync(
        long id,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var admin = await users.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();
        var product = await db.Products.Include(item => item.Seller).Include(item => item.Store)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null) return Results.NotFound();
        if (!await AdminApi.CanManageStoreAsync(principal, admin.Id, product.StoreId, db, cancellationToken)) return Results.Forbid();
        if (product.InventoryStatus != InventoryStatus.Sold || product.PayoutStatus != PayoutStatus.Pending)
            return Results.Conflict(new { message = "Отметить выплату можно после продажи товара." });
        product.PayoutStatus = PayoutStatus.Paid;
        QueueStatusEmail(db, product.Seller, "Выплата произведена", $"Выплата за товар «{product.Title}» произведена: {product.PayoutAmount:0.00} {product.Store.CurrencyCode}.");
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { product.Id, product.PayoutStatus });
    }

    private static void QueueStatusEmail(KomokDbContext db, ApplicationUser user, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;
        db.EmailOutbox.Add(new EmailOutboxMessage
        {
            Recipient = user.Email,
            Subject = subject,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
