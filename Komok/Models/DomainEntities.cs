using Microsoft.AspNetCore.Identity;

namespace Komok.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string? Name { get; set; }
    public string Messenger { get; set; } = "";
    public string MessengerContact { get; set; } = "";
    public string PayoutDetails { get; set; } = "";
}

public sealed class Store
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string CurrencyCode { get; set; }
    public string TimeZoneId { get; set; } = "+03:00";
    public int BookingDurationHours { get; set; } = 24;
    public bool IsActive { get; set; } = true;
    public List<StoreOpeningInterval> OpeningIntervals { get; set; } = [];
    public List<CommissionBand> CommissionBands { get; set; } = [];
    public List<StoreAdmin> Administrators { get; set; } = [];
}

public sealed class StoreOpeningInterval
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public Store Store { get; set; } = null!;
}

public sealed class CommissionBand
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public decimal PriceThreshold { get; set; }
    public CommissionKind Kind { get; set; }
    public decimal Rate { get; set; }
    public Store Store { get; set; } = null!;
}

public sealed class StoreAdmin
{
    public int StoreId { get; set; }
    public string UserId { get; set; } = "";
    public Store Store { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

public sealed class ProductCategory
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Product
{
    public long Id { get; set; }
    public int StoreId { get; set; }
    public int CategoryId { get; set; }
    public string SellerId { get; set; } = "";
    public required string Title { get; set; }
    public required string Description { get; set; }
    public decimal AskingPrice { get; set; }
    public decimal? CommissionRate { get; set; }
    public CommissionKind CommissionKind { get; set; }
    public ListingStatus ListingStatus { get; set; } = ListingStatus.Submitted;
    public InventoryStatus InventoryStatus { get; set; } = InventoryStatus.NotReceived;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ReservedUntil { get; set; }
    public DateTimeOffset? ArrivalAt { get; set; }
    public string? BuyerName { get; set; }
    public string? BuyerMessenger { get; set; }
    public string? BuyerMessengerContact { get; set; }
    public string? BuyerPhone { get; set; }
    public string? BuyerEmail { get; set; }
    public decimal? SoldPrice { get; set; }
    public decimal? CommissionAmount { get; set; }
    public decimal? PayoutAmount { get; set; }
    public PayoutStatus PayoutStatus { get; set; } = PayoutStatus.NotDue;
    public List<ProductPhoto> Photos { get; set; } = [];
    public Store Store { get; set; } = null!;
    public ProductCategory Category { get; set; } = null!;
    public ApplicationUser Seller { get; set; } = null!;
}

public sealed class ProductPhoto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public int SortOrder { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Data { get; set; }
    public Product Product { get; set; } = null!;
}

public sealed class EmailOutboxMessage
{
    public long Id { get; set; }
    public required string Recipient { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public enum CommissionKind { Percent, Fixed }
public enum ListingStatus { Submitted, NeedsRevision, Rejected, AwaitingDelivery, Delivery, OnSale, Cancelled }
public enum InventoryStatus { NotReceived, Available, Reserved, Sold }
public enum PayoutStatus { NotDue, Pending, Paid }
