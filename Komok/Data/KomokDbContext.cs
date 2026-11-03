using Komok.Models;
using Microsoft.EntityFrameworkCore;

namespace Komok.Data;

public sealed class KomokDbContext(DbContextOptions<KomokDbContext> options) : DbContext(options)
{
    public DbSet<TextRecord> Texts => Set<TextRecord>();
    public DbSet<Text2Record> Text2Records => Set<Text2Record>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TextRecord>(entity =>
        {
            entity.ToTable("Text");
            entity.HasNoKey();
            entity.Property(row => row.Txt).HasColumnName("Txt").HasMaxLength(500);
        });

        modelBuilder.Entity<Text2Record>(entity =>
        {
            entity.ToTable("Text2");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedOnAdd();
            entity.Property(row => row.Fio).HasColumnName("fio").HasMaxLength(500).IsRequired();
            entity.HasData(
                new Text2Record { Id = 1, Fio = "fio1" },
                new Text2Record { Id = 2, Fio = "fio2" },
                new Text2Record { Id = 3, Fio = "fio3" });
        });
    }
}
