using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Products;
using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

namespace ZachHairStudio.Shared.Db;

// Identity tables (AspNetUsers/AspNetRoles/etc.) live in this same schema/migration history
// as Appointments (D-02, D-12) — int keys, consistent with every other entity's int Id.
public class BookingDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Stylist> Stylists => Set<Stylist>();

    public DbSet<StylistWorkingHours> StylistWorkingHours => Set<StylistWorkingHours>();

    public DbSet<StylistTimeOff> StylistTimeOff => Set<StylistTimeOff>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity needs its model configured before ours — ASP.NET Core convention when
        // inheriting IdentityDbContext.
        base.OnModelCreating(modelBuilder);

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
                    ImageUrl = "/uploads/services/precision-cut.jpg",
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
                    ImageUrl = "/uploads/services/color-and-highlights.jpg",
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
                    ImageUrl = "/uploads/services/blowout-and-styling.jpg",
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
                    ImageUrl = "/uploads/services/keratin-treatment.jpg",
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
                    ImageUrl = "/uploads/services/scalp-treatment.jpg",
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
                    ImageUrl = "/uploads/services/full-glam-package.jpg",
                    IsActive = true,
                    DisplayOrder = 6,
                });
        });

        // Owner-reviewable placeholder catalog (D-17) — plausible stylist-recommended
        // add-ons pairing with the existing seeded services below.
        modelBuilder.Entity<Product>(entity =>
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
                new Product
                {
                    Id = 1,
                    Slug = "leave-in-repair-serum",
                    Name = "Leave-In Repair Serum",
                    ShortDescription = "A lightweight leave-in serum that locks in smoothness after a keratin service.",
                    LongDescription = "A lightweight leave-in serum formulated to extend the smoothing effects of a keratin treatment between salon visits. Applies to damp or dry hair to reduce frizz and add shine without weighing hair down.",
                    Category = "Hair Care",
                    Price = 24.00m,
                    Stock = 40,
                    ImageUrl = null,
                    IsActive = true,
                },
                new Product
                {
                    Id = 2,
                    Slug = "color-safe-shampoo",
                    Name = "Color-Safe Shampoo",
                    ShortDescription = "A sulfate-free shampoo that protects vibrant color and highlights from fading.",
                    LongDescription = "A sulfate-free, color-safe shampoo designed to preserve tone and shine after a color or highlight service. Gently cleanses without stripping the color molecules that give fresh color its vibrancy.",
                    Category = "Hair Care",
                    Price = 18.00m,
                    Stock = 60,
                    ImageUrl = null,
                    IsActive = true,
                },
                new Product
                {
                    Id = 3,
                    Slug = "color-safe-conditioner",
                    Name = "Color-Safe Conditioner",
                    ShortDescription = "A nourishing conditioner that pairs with our color-safe shampoo to extend color life.",
                    LongDescription = "A nourishing, color-safe conditioner formulated to pair with the color-safe shampoo. Softens and detangles while helping lock in color vibrancy between coloring appointments.",
                    Category = "Hair Care",
                    Price = 19.00m,
                    Stock = 55,
                    ImageUrl = null,
                    IsActive = true,
                },
                new Product
                {
                    Id = 4,
                    Slug = "texturizing-styling-cream",
                    Name = "Texturizing Styling Cream",
                    ShortDescription = "A flexible-hold cream for defined texture and movement after a blowout.",
                    LongDescription = "A flexible-hold styling cream that adds definition and texture without stiffness, perfect for extending a fresh blowout or building volume for an event-ready look.",
                    Category = "Styling",
                    Price = 22.00m,
                    Stock = 0,
                    ImageUrl = null,
                    IsActive = true,
                },
                new Product
                {
                    Id = 5,
                    Slug = "heat-protectant-spray",
                    Name = "Heat Protectant Spray",
                    ShortDescription = "A lightweight spray that shields hair from heat styling damage.",
                    LongDescription = "A lightweight, non-greasy spray applied before blow-drying or hot tools to shield hair from heat damage, helping styled looks last longer between salon visits.",
                    Category = "Styling",
                    Price = 16.00m,
                    Stock = 50,
                    ImageUrl = null,
                    IsActive = true,
                },
                new Product
                {
                    Id = 6,
                    Slug = "revitalizing-scalp-oil",
                    Name = "Revitalizing Scalp Oil",
                    ShortDescription = "A soothing scalp oil that extends the benefits of an in-salon scalp treatment.",
                    LongDescription = "A soothing, lightweight scalp oil blended to extend the hydrating benefits of an in-salon scalp treatment. Massage into the scalp between visits to support comfort and a healthier hair environment.",
                    Category = "Treatments",
                    Price = 28.00m,
                    Stock = 30,
                    ImageUrl = null,
                    IsActive = true,
                },
                new Product
                {
                    Id = 7,
                    Slug = "discontinued-styling-wax",
                    Name = "Discontinued Styling Wax",
                    ShortDescription = "A retired matte styling wax, kept only to exercise the inactive-product path.",
                    LongDescription = "A retired matte styling wax no longer sold in the studio. Present only so the inactive-product 404 and enumeration-safety paths have a real seeded row to exercise.",
                    Category = "Styling",
                    Price = 15.00m,
                    Stock = 0,
                    ImageUrl = null,
                    IsActive = false,
                });
        });

        // Explicit join entity (D-11, RESEARCH Pattern 2) — seedable via HasData, unlike
        // EF Core's implicit shadow join table (RESEARCH Pitfall 2). Curated per D-12;
        // precision-cut (1) and full-glam-package (6) are deliberately left unlinked to
        // exercise the "no recommended products" empty state.
        modelBuilder.Entity<Service>()
            .HasMany<Product>()
            .WithMany()
            .UsingEntity<ServiceRecommendedProduct>(
                j => j.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId),
                j => j.HasOne<Service>().WithMany().HasForeignKey(x => x.ServiceId),
                j =>
                {
                    j.HasKey(x => new { x.ServiceId, x.ProductId });
                    j.HasData(
                        new ServiceRecommendedProduct { ServiceId = 4, ProductId = 1 }, // keratin-treatment -> leave-in-repair-serum
                        new ServiceRecommendedProduct { ServiceId = 2, ProductId = 2 }, // color-and-highlights -> color-safe-shampoo
                        new ServiceRecommendedProduct { ServiceId = 2, ProductId = 3 }, // color-and-highlights -> color-safe-conditioner
                        new ServiceRecommendedProduct { ServiceId = 3, ProductId = 4 }, // blowout-and-styling -> texturizing-styling-cream
                        new ServiceRecommendedProduct { ServiceId = 3, ProductId = 5 }, // blowout-and-styling -> heat-protectant-spray
                        new ServiceRecommendedProduct { ServiceId = 5, ProductId = 6 }); // scalp-treatment -> revitalizing-scalp-oil
                });

        modelBuilder.Entity<Stylist>(entity =>
        {
            entity.Property(e => e.Slug).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(150);

            entity.HasIndex(e => e.Slug).IsUnique();

            // Seeded from landing-page/lib/data.ts `team` array (owner-editable content).
            entity.HasData(
                new Stylist { Id = 1, Slug = "zin-min", Name = "Zin Min", IsActive = true, DisplayOrder = 1 },
                new Stylist { Id = 2, Slug = "may-yoon", Name = "May Yoon", IsActive = true, DisplayOrder = 2 },
                new Stylist { Id = 3, Slug = "thiri-cho", Name = "Thiri Cho", IsActive = true, DisplayOrder = 3 },
                new Stylist { Id = 4, Slug = "sai-min-htet", Name = "Sai Min Htet", IsActive = true, DisplayOrder = 4 });
        });

        modelBuilder.Entity<StylistWorkingHours>(entity =>
        {
            // Owner-reviewable placeholder default schedule (every day 09:00-18:00 per active stylist).
            // The salon opens seven days a week — owner-directed 2026-07-23, replacing the earlier Tue-Sat default.
            // Mirrors the seed-price precedent from Phase 1 (D-15) — flag for owner review, not a final schedule.
            entity.HasData(
                new StylistWorkingHours { Id = 1, StylistId = 1, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 2, StylistId = 1, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 3, StylistId = 1, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 4, StylistId = 1, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 5, StylistId = 1, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 6, StylistId = 2, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 7, StylistId = 2, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 8, StylistId = 2, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 9, StylistId = 2, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 10, StylistId = 2, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 11, StylistId = 3, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 12, StylistId = 3, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 13, StylistId = 3, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 14, StylistId = 3, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 15, StylistId = 3, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 16, StylistId = 4, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 17, StylistId = 4, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 18, StylistId = 4, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 19, StylistId = 4, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 20, StylistId = 4, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 21, StylistId = 1, DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 22, StylistId = 1, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 23, StylistId = 2, DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 24, StylistId = 2, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 25, StylistId = 3, DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 26, StylistId = 3, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 27, StylistId = 4, DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
                new StylistWorkingHours { Id = 28, StylistId = 4, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) });
        });

        modelBuilder.Entity<StylistTimeOff>(entity =>
        {
            entity.Property(e => e.Reason).HasMaxLength(200);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(e => e.Status)
                  .HasConversion<string>()
                  .HasMaxLength(50);

            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.StatusChangedBy).HasMaxLength(100);

            entity.HasMany(a => a.Slots)
                  .WithOne(s => s.Appointment)
                  .HasForeignKey(s => s.AppointmentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppointmentSlot>(entity =>
        {
            // The unfiltered unique index IS the SC4/BOOK-04 double-booking guarantee (D-03, D-04) —
            // it must have NO HasFilter().
            entity.HasIndex(s => new { s.StylistId, s.SlotStart }).IsUnique();
            entity.Property(s => s.SlotStart).HasColumnType("datetimeoffset(0)");
        });
    }
}
