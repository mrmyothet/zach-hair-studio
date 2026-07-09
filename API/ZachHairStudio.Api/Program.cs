using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers();
builder.Services.AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>();
builder.Services.AddScoped<ServicesService>();
builder.Services.AddScoped<StylistsService>();
builder.Services.Configure<SalonOptions>(builder.Configuration.GetSection("Salon"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}

/// <summary>
/// Salon-wide configuration bound from the "Salon" section (appsettings.json).
/// The IANA timezone id is the single source of truth every DateTimeOffset
/// conversion in the booking domain resolves against (D-16).
/// </summary>
public class SalonOptions
{
    public string IanaTimeZoneId { get; set; } = "America/New_York";
}
