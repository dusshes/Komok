using Komok.Models;
using Microsoft.EntityFrameworkCore;

namespace Komok.Data;

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestRecord> Tests => Set<TestRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestRecord>(entity =>
        {
            entity.ToTable("Test");
            entity.HasNoKey();
            entity.Property(row => row.Txt).HasColumnName("Txt").HasMaxLength(500);
        });
    }
}
