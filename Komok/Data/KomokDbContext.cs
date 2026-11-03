using Komok.Models;
using Microsoft.EntityFrameworkCore;

namespace Komok.Data;

public sealed class KomokDbContext(DbContextOptions<KomokDbContext> options) : DbContext(options)
{
    public DbSet<TextRecord> Texts => Set<TextRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TextRecord>(entity =>
        {
            entity.ToTable("Text");
            entity.HasNoKey();
            entity.Property(row => row.Txt).HasColumnName("Txt").HasMaxLength(500);
        });
    }
}