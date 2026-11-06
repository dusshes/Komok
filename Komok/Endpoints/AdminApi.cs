using System.Text.RegularExpressions;
using System.Security.Claims;
using Komok.Contracts;
using Komok.Data;
using Komok.Models;
using Komok.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Komok.Endpoints;

public static class AdminApi
{
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").RequireAuthorization().WithTags("Administration");
        admin.MapGet("/stores", GetStoresAsync);
        admin.MapPost("/stores", CreateStoreAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.SuperAdmin));
        admin.MapPut("/stores/{storeId:int}", UpdateStoreAsync);
        admin.MapPost("/users/assign-admin", AssignAdminAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.SuperAdmin));
        admin.MapGet("/users", GetUsersAsync)
            .RequireAuthorization(policy => policy.RequireRole(Roles.SuperAdmin));
        return endpoints;
    }

    private static async Task<IResult> GetStoresAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var query = db.Stores.AsNoTracking().AsQueryable();
        if (!principal.IsInRole(Roles.SuperAdmin))
        {
            var assignedIds = db.StoreAdministrators.Where(link => link.UserId == user.Id).Select(link => link.StoreId);
            query = query.Where(store => assignedIds.Contains(store.Id));
        }

        var stores = await query.OrderBy(store => store.Name).Select(store => new
        {
            store.Id,
            store.Name,
            store.Address,
            store.CurrencyCode,
            store.TimeZoneId,
            store.BookingDurationHours,
            store.IsActive,
            OpeningIntervals = store.OpeningIntervals.OrderBy(interval => interval.DayOfWeek).ThenBy(interval => interval.OpensAt)
                .Select(interval => new { interval.Id, DayOfWeek = (int)interval.DayOfWeek, interval.OpensAt, interval.ClosesAt }),
            CommissionBands = store.CommissionBands.OrderBy(band => band.PriceThreshold)
                .Select(band => new { band.Id, band.PriceThreshold, band.Kind, band.Rate })
        }).ToListAsync(cancellationToken);
        return Results.Ok(stores);
    }

    private static async Task<IResult> CreateStoreAsync(
        StoreUpsertRequest request,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = ValidateStore(request);
        if (validation.Count > 0) return ValidationErrors.BadRequest(validation);
        var store = new Store
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            TimeZoneId = request.TimeZoneId,
            BookingDurationHours = request.BookingDurationHours,
            OpeningIntervals = request.OpeningIntervals.Select(interval => new StoreOpeningInterval
            {
                DayOfWeek = interval.DayOfWeek,
                OpensAt = interval.OpensAt,
                ClosesAt = interval.ClosesAt
            }).ToList(),
            CommissionBands = request.CommissionBands.Select(band => new CommissionBand
            {
                PriceThreshold = band.PriceThreshold,
                Kind = band.Kind,
                Rate = band.Rate
            }).ToList()
        };
        db.Stores.Add(store);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/admin/stores/{store.Id}", new { store.Id });
    }

    private static async Task<IResult> UpdateStoreAsync(
        int storeId,
        StoreUpsertRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (!await CanManageStoreAsync(principal, user.Id, storeId, db, cancellationToken)) return Results.Forbid();
        var validation = ValidateStore(request);
        if (validation.Count > 0) return ValidationErrors.BadRequest(validation);

        var store = await db.Stores.Include(item => item.OpeningIntervals).Include(item => item.CommissionBands)
            .SingleOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        if (store is null) return Results.NotFound();
        store.Name = request.Name.Trim();
        store.Address = request.Address.Trim();
        store.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        store.TimeZoneId = request.TimeZoneId;
        store.BookingDurationHours = request.BookingDurationHours;
        store.OpeningIntervals.Clear();
        store.OpeningIntervals.AddRange(request.OpeningIntervals.Select(interval => new StoreOpeningInterval
        {
            StoreId = storeId,
            DayOfWeek = interval.DayOfWeek,
            OpensAt = interval.OpensAt,
            ClosesAt = interval.ClosesAt
        }));
        store.CommissionBands.Clear();
        store.CommissionBands.AddRange(request.CommissionBands.Select(band => new CommissionBand
        {
            StoreId = storeId,
            PriceThreshold = band.PriceThreshold,
            Kind = band.Kind,
            Rate = band.Rate
        }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AssignAdminAsync(
        AssignAdminRequest request,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return ValidationErrors.BadRequest(("email", "Укажите email зарегистрированного пользователя."));
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return ValidationErrors.BadRequest(("email", "Пользователь с таким email не зарегистрирован."));
        if (request.StoreIds is null)
            return ValidationErrors.BadRequest(("storeIds", "Выберите магазины для администратора."));
        var storeIds = request.StoreIds.Distinct().ToArray();
        if (storeIds.Length > 0 && await db.Stores.CountAsync(store => storeIds.Contains(store.Id), cancellationToken) != storeIds.Length)
            return ValidationErrors.BadRequest(("storeIds", "Выберите существующие магазины."));

        if (!await roles.RoleExistsAsync(Roles.StoreAdmin))
            await roles.CreateAsync(new IdentityRole(Roles.StoreAdmin));
        if (!await users.IsInRoleAsync(user, Roles.StoreAdmin))
            await users.AddToRoleAsync(user, Roles.StoreAdmin);

        var existing = await db.StoreAdministrators.Where(link => link.UserId == user.Id).ToListAsync(cancellationToken);
        db.StoreAdministrators.RemoveRange(existing);
        db.StoreAdministrators.AddRange(storeIds.Select(storeId => new StoreAdmin { UserId = user.Id, StoreId = storeId }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { user.Email, StoreIds = storeIds });
    }

    private static async Task<IResult> GetUsersAsync(UserManager<ApplicationUser> users, CancellationToken cancellationToken)
    {
        var allUsers = await users.Users.AsNoTracking().OrderBy(user => user.Email)
            .Select(user => new { user.Email, user.EmailConfirmed, user.Messenger, user.MessengerContact })
            .ToListAsync(cancellationToken);
        return Results.Ok(allUsers);
    }

    internal static async Task<bool> CanManageStoreAsync(
        ClaimsPrincipal principal, string userId, int storeId, KomokDbContext db, CancellationToken cancellationToken) =>
        principal.IsInRole(Roles.SuperAdmin)
        || await db.StoreAdministrators.AnyAsync(link => link.UserId == userId && link.StoreId == storeId, cancellationToken);

    private static Dictionary<string, string[]> ValidateStore(StoreUpsertRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
                errors[field] = messages = [];
            messages.Add(message);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 160)
            Add("name", "Укажите название длиной не более 160 символов.");
        if (string.IsNullOrWhiteSpace(request.Address) || request.Address.Length > 500)
            Add("address", "Укажите адрес передачи товара длиной не более 500 символов.");
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || !Regex.IsMatch(request.CurrencyCode, "^[A-Za-z]{3}$"))
            Add("currencyCode", "Введите трёхбуквенный код валюты, например RUB или EUR.");
        if (request.BookingDurationHours is < 1 or > 720)
            Add("bookingDurationHours", "Укажите длительность от 1 до 720 рабочих часов.");
        if (string.IsNullOrWhiteSpace(request.TimeZoneId))
            Add("timeZoneId", "Укажите часовой пояс.");
        else
        {
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
            catch (TimeZoneNotFoundException) { Add("timeZoneId", "Выберите существующий часовой пояс."); }
            catch (InvalidTimeZoneException) { Add("timeZoneId", "Выберите корректный часовой пояс."); }
        }

        if (request.OpeningIntervals is null)
            Add("openingIntervals", "Укажите рабочие интервалы магазина.");
        else for (var index = 0; index < request.OpeningIntervals.Count; index++)
        {
            var interval = request.OpeningIntervals[index];
            if (interval.OpensAt >= interval.ClosesAt)
                Add($"openingIntervals[{index}].closesAt", "Время закрытия должно быть позже времени открытия.");
            if (!Enum.IsDefined(interval.DayOfWeek))
                Add($"openingIntervals[{index}].dayOfWeek", "Выберите день недели.");
        }
        var orderedIntervals = (request.OpeningIntervals ?? []).Select((interval, index) => (interval, index))
            .GroupBy(item => item.interval.DayOfWeek);
        foreach (var group in orderedIntervals)
        {
            var ordered = group.OrderBy(item => item.interval.OpensAt).ToArray();
            for (var index = 1; index < ordered.Length; index++)
                if (ordered[index - 1].interval.ClosesAt > ordered[index].interval.OpensAt)
                    Add($"openingIntervals[{ordered[index].index}].opensAt", "Рабочие интервалы одного дня не должны пересекаться.");
        }
        if (request.CommissionBands is null || request.CommissionBands.Count == 0)
            Add("commissionBands", "Добавьте хотя бы один порог комиссии.");
        var commissionBands = request.CommissionBands ?? [];
        for (var index = 0; index < commissionBands.Count; index++)
        {
            var band = commissionBands[index];
            if (band.PriceThreshold < 0)
                Add($"commissionBands[{index}].priceThreshold", "Порог цены не может быть отрицательным.");
            if (!Enum.IsDefined(band.Kind))
                Add($"commissionBands[{index}].kind", "Выберите тип комиссии.");
            if (band.Rate < 0 || (band.Kind == CommissionKind.Percent && band.Rate > 100))
                Add($"commissionBands[{index}].rate", band.Kind == CommissionKind.Percent
                    ? "Процент комиссии должен быть от 0 до 100."
                    : "Сумма комиссии не может быть отрицательной.");
        }
        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }
}
