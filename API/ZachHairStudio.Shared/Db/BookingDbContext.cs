using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Features.Bookings;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Shared.Db;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(e => e.Status)
                  .HasConversion<string>()
                  .HasMaxLength(50);

            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Service).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(1000);
        });

        base.OnModelCreating(modelBuilder);
    }
}
