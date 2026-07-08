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

        modelBuilder.Entity<Service>(entity =>
        {
            entity.Property(e => e.Slug).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.ShortDescription).HasMaxLength(200);
            entity.Property(e => e.LongDescription).HasMaxLength(2000);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasIndex(e => e.Slug).IsUnique();

            entity.HasData(
                new Service
                {
                    Id = 1,
                    Slug = "precision-cut",
                    Name = "Precision Cut",
                    ShortDescription = "Tailored haircuts designed to complement your face shape and lifestyle perfectly.",
                    LongDescription = "A tailored cut shaped around your face, texture, and daily styling routine. Includes a consultation and finishing touches so your hair leaves polished and easy to maintain.",
                    Category = "Cuts",
                    DurationMinutes = 45,
                    Price = 35m,
                    ImageUrl = null,
                    IsActive = true,
                    DisplayOrder = 1,
                },
                new Service
                {
                    Id = 2,
                    Slug = "color-and-highlights",
                    Name = "Color & Highlights",
                    ShortDescription = "Vibrant color treatments and natural-looking highlights using premium products.",
                    LongDescription = "Dimensional color and highlight work customized to your skin tone, cut, and maintenance goals. The service uses premium products for glossy color, soft grow-out, and a salon-fresh finish.",
                    Category = "Color",
                    DurationMinutes = 90,
                    Price = 80m,
                    ImageUrl = null,
                    IsActive = true,
                    DisplayOrder = 2,
                },
                new Service
                {
                    Id = 3,
                    Slug = "blowout-and-styling",
                    Name = "Blowout & Styling",
                    ShortDescription = "Professional blowouts and styling for any occasion — weddings, events, or everyday glam.",
                    LongDescription = "A smooth professional blowout or styled finish tailored to the occasion, from everyday polish to event-ready volume. Ideal when you want shine, movement, and a finished look without a full cut or color service.",
                    Category = "Styling",
                    DurationMinutes = 45,
                    Price = 55m,
                    ImageUrl = null,
                    IsActive = true,
                    DisplayOrder = 3,
                },
                new Service
                {
                    Id = 4,
                    Slug = "keratin-treatment",
                    Name = "Keratin Treatment",
                    ShortDescription = "Smoothing treatments that eliminate frizz and add lasting shine and manageability.",
                    LongDescription = "A smoothing treatment designed to reduce frizz, increase shine, and make daily styling easier. Best for clients looking for a longer-lasting sleek finish and improved manageability between salon visits.",
                    Category = "Treatments",
                    DurationMinutes = 120,
                    Price = 120m,
                    ImageUrl = null,
                    IsActive = true,
                    DisplayOrder = 4,
                },
                new Service
                {
                    Id = 5,
                    Slug = "scalp-treatment",
                    Name = "Scalp Treatment",
                    ShortDescription = "Revitalizing scalp therapies to promote health, hydration, and hair growth.",
                    LongDescription = "A restorative scalp-focused service that refreshes, hydrates, and supports a healthier hair environment. Recommended for clients wanting comfort, balance, and a relaxing reset between larger services.",
                    Category = "Treatments",
                    DurationMinutes = 40,
                    Price = 65m,
                    ImageUrl = null,
                    IsActive = true,
                    DisplayOrder = 5,
                },
                new Service
                {
                    Id = 6,
                    Slug = "full-glam-package",
                    Name = "Full Glam Package",
                    ShortDescription = "Cut + Color + Blowout + Scalp treatment. The complete studio experience in one visit.",
                    LongDescription = "The full studio transformation package combining a precision cut, color service, blowout, and scalp treatment. Built for clients who want the complete Zach Hair Studio experience in one coordinated visit.",
                    Category = "Styling",
                    DurationMinutes = 210,
                    Price = 199m,
                    ImageUrl = null,
                    IsActive = true,
                    DisplayOrder = 6,
                });
        });

        base.OnModelCreating(modelBuilder);
    }
}
