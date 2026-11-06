using Komok.Models;

namespace Komok.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string Messenger,
    string MessengerContact,
    string? PayoutDetails);

public sealed record LoginRequest(string Email, string Password, bool RememberMe = true);
public sealed record ConfirmEmailRequest(string Email, string Token);
public sealed record CreateListingRequest(int StoreId, string Title, string Description, decimal AskingPrice);
public sealed record BookingRequest(long ProductId, DateTimeOffset ArrivalAt, string? Name, string? Phone, string? Email);
public sealed record StoreUpsertRequest(
    string Name,
    string Address,
    string CurrencyCode,
    string TimeZoneId,
    int BookingDurationHours,
    IReadOnlyList<OpeningIntervalRequest> OpeningIntervals,
    IReadOnlyList<CommissionBandRequest> CommissionBands);
public sealed record OpeningIntervalRequest(DayOfWeek DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);
public sealed record CommissionBandRequest(decimal PriceThreshold, CommissionKind Kind, decimal Rate);
public sealed record ReviewListingRequest(
    string Action,
    string? Title,
    string? Description,
    decimal? AskingPrice,
    decimal? CommissionPercent);
public sealed record MarkSoldRequest(decimal SoldPrice);
public sealed record AssignAdminRequest(string Email, IReadOnlyList<int> StoreIds);
