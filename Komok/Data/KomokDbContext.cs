using Komok.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Komok.Data;

public sealed class KomokDbContext(DbContextOptions<KomokDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreOpeningInterval> StoreOpeningIntervals => Set<StoreOpeningInterval>();
    public DbSet<CommissionBand> CommissionBands => Set<CommissionBand>();
    public DbSet<StoreAdmin> StoreAdministrators => Set<StoreAdmin>();
    public DbSet<ProductCategory> Categories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<EmailOutboxMessage> EmailOutbox => Set<EmailOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Store>(entity =>
        {
            entity.Property(store => store.Name).HasMaxLength(160).IsRequired();
            entity.Property(store => store.Address).HasMaxLength(500).IsRequired();
            entity.Property(store => store.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(store => store.TimeZoneId).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<StoreOpeningInterval>(entity =>
        {
            entity.HasOne(interval => interval.Store)
                .WithMany(store => store.OpeningIntervals)
                .HasForeignKey(interval => interval.StoreId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(interval => new { interval.StoreId, interval.DayOfWeek, interval.OpensAt });
        });

        modelBuilder.Entity<CommissionBand>(entity =>
        {
            entity.Property(band => band.PriceThreshold).HasPrecision(12, 2);
            entity.Property(band => band.Rate).HasPrecision(12, 2);
            entity.HasOne(band => band.Store)
                .WithMany(store => store.CommissionBands)
                .HasForeignKey(band => band.StoreId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(band => new { band.StoreId, band.PriceThreshold }).IsUnique();
        });

        modelBuilder.Entity<StoreAdmin>(entity =>
        {
            entity.HasKey(link => new { link.StoreId, link.UserId });
            entity.HasOne(link => link.Store).WithMany(store => store.Administrators)
                .HasForeignKey(link => link.StoreId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(link => link.User).WithMany()
                .HasForeignKey(link => link.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("Categories");
            entity.Property(category => category.Name).HasMaxLength(120).IsRequired();
            entity.Property(category => category.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(category => category.IsActive).HasDefaultValue(true);
            entity.HasIndex(category => category.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(product => product.Title).HasMaxLength(160).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(5000).IsRequired();
            entity.Property(product => product.BuyerMessenger).HasMaxLength(32);
            entity.Property(product => product.BuyerMessengerContact).HasMaxLength(200);
            entity.Property(product => product.AskingPrice).HasPrecision(12, 2);
            entity.Property(product => product.CommissionRate).HasPrecision(12, 2);
            entity.Property(product => product.SoldPrice).HasPrecision(12, 2);
            entity.Property(product => product.CommissionAmount).HasPrecision(12, 2);
            entity.Property(product => product.PayoutAmount).HasPrecision(12, 2);
            entity.HasOne(product => product.Store).WithMany()
                .HasForeignKey(product => product.StoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(product => product.Category).WithMany()
                .HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(product => product.Seller).WithMany()
                .HasForeignKey(product => product.SellerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(product => new { product.StoreId, product.ListingStatus, product.InventoryStatus });
            entity.HasIndex(product => product.CategoryId);
            entity.HasIndex(product => product.SellerId);
        });

        modelBuilder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.Property(message => message.Recipient).HasMaxLength(320).IsRequired();
            entity.Property(message => message.Subject).HasMaxLength(300).IsRequired();
            entity.Property(message => message.Body).IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(2000);
            entity.HasIndex(message => new { message.SentAt, message.NextAttemptAt, message.CreatedAt });
        });
    }
}
