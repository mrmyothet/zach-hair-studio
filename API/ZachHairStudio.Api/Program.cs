using System.Net.Http.Headers;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ZachHairStudio.Api.Mcp;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Appointments;
using ZachHairStudio.Shared.Features.Availability;
using ZachHairStudio.Shared.Features.Identity;
using ZachHairStudio.Shared.Features.Services;
using ZachHairStudio.Shared.Features.Stylists;

var builder = WebApplication.CreateBuilder(args);

// The default host registers user secrets only in Development, but D-12 requires the
// RESEND_API_KEY to resolve in the Testing environment too (real sends, no fake sender).
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// AllowAnyOrigin() already admits the dashboard origin — bearer tokens don't need
// AllowCredentials(), so no CORS change is required for the dashboard to authenticate
// (RESEARCH Pitfall 2); production lockdown is Phase 8 (LAUNCH-02).
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Status values round-trip as strings elsewhere (AppointmentResponseDto.Status), so
// accept AppointmentStatus request fields (e.g. AppointmentStatusUpdateDto.NewStatus)
// as their string names too, rather than requiring the client to send the raw int.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddValidatorsFromAssemblyContaining<ServiceCreateDtoValidator>();
builder.Services.AddScoped<ServicesService>();
builder.Services.AddScoped<StylistsService>();
builder.Services.Configure<SalonOptions>(builder.Configuration.GetSection("Salon"));
// Bridge IOptions<SalonOptions> -> plain SalonOptions so Shared-project services
// (SlotService) can depend on it directly without referencing Microsoft.Extensions.Options.
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SalonOptions>>().Value);
builder.Services.AddScoped<SlotService>();

// Stateless HTTP transport shares the ASP.NET Core per-request DI scope, which is what
// lets the scoped SlotService (and its scoped BookingDbContext) resolve correctly per
// tool call. Explicit WithTools<ScheduleTools>() (not assembly-wide discovery) keeps the
// unauthenticated /mcp surface limited to exactly this one read-only tool (mitigates T-Q04).
builder.Services.AddMcpServer()
    .WithHttpTransport(options => { options.Stateless = true; })
    .WithTools<ScheduleTools>();

// Resend confirmation email (D-09/D-10/D-11). FromEmail is a non-secret appsettings
// value; the API key is read from RESEND_API_KEY (user-secrets/env, D-13) — never a
// tracked file. The bearer token is set on the typed HttpClient only.
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection("Resend"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ResendOptions>>().Value);
builder.Services.AddScoped<AppointmentsService>();
builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["RESEND_API_KEY"]);
});

// Staff auth (D-01/D-02/D-03). JWT signing key is read once from configuration
// (user-secrets/env, D-13-style) — never a tracked appsettings value, and never
// regenerated per-process (RESEARCH Pitfall 5) so outstanding ~12h tokens survive a restart.
// Fail fast when the signing key is missing or too weak (HS256 needs >=256 bits) —
// an empty JwtOptions.SigningKey default would otherwise only surface on the first
// authenticated request. ValidateOnStart (not an eager read here) so the check runs
// after ALL config sources apply, including WebApplicationFactory test overrides.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.SigningKey)
            && Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
        "Jwt:SigningKey is missing or shorter than 32 bytes (256 bits, the HS256 minimum). "
        + "Set it via 'dotnet user-secrets set \"Jwt:SigningKey\" \"<random value of 32+ chars>\"' "
        + "or the Jwt__SigningKey environment variable (D-13 — never a tracked appsettings value).")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtOptions>>().Value);
builder.Services.AddScoped<JwtTokenService>();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>()
    .AddEntityFrameworkStores<BookingDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        // Default to JwtBearer for both authenticate + challenge so an unauthenticated
        // [Authorize] hit returns a 401 JSON challenge, never the Identity cookie redirect.
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Bound lazily (at first JwtBearerOptions resolution, post-start) so test hosts'
        // late-injected Jwt:* config is honored — an eager read here would capture the
        // dev user-secrets key before WebApplicationFactory overrides apply.
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    db.Database.Migrate();

    // Owner seed (D-04) — tests seed their own users, so this is skipped in Testing.
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await IdentitySeeder.SeedAsync(roleManager, userManager, config);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMcp("/mcp");

app.Run();

public partial class Program
{
}
