using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Identity;
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

        modelBuilder.Entity<Stylist>(entity =>
        {
            entity.Property(e => e.Slug).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(150);

            entity.HasIndex(e => e.Slug).IsUnique();

            // Seeded from landing-page/lib/data.ts `team` array (owner-editable content).
            entity.HasData(
                new Stylist { Id = 1, Slug = "mr-zachary", Name = "Mr. Zachary", IsActive = true, DisplayOrder = 1 },
                new Stylist { Id = 2, Slug = "aria-chen", Name = "Aria Chen", IsActive = true, DisplayOrder = 2 },
                new Stylist { Id = 3, Slug = "marcus-lee", Name = "Marcus Lee", IsActive = true, DisplayOrder = 3 },
                new Stylist { Id = 4, Slug = "sofia-reyes", Name = "Sofia Reyes", IsActive = true, DisplayOrder = 4 });
        });

        modelBuilder.Entity<StylistWorkingHours>(entity =>
        {
            // Owner-reviewable placeholder default schedule (Tue-Sat 09:00-18:00 per active stylist).
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
                new StylistWorkingHours { Id = 20, StylistId = 4, DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) });
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
