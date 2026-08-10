using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Payments;

namespace ZachHairStudio.Api.Tests;

// Real SQL Server fixture. Unlike CustomWebApplicationFactory (InMemory, which
// enforces no unique indexes), this migrates the actual AddBookingCore schema so the
// unfiltered unique index (SC4 double-booking) and datetimeoffset round-trip (SC5) are
// exercised against real SQL Server semantics. Uses a per-run unique database dropped on dispose.
//
// Connection override (Linux / Azure SQL / Docker): prefer
// ConnectionStrings__DefaultConnection or TEST_SQLSERVER_CONNECTION; else LocalDB default.
public class SqlServerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public SqlServerWebApplicationFactory()
    {
        var overrideConnection =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION");

        _connectionString = !string.IsNullOrWhiteSpace(overrideConnection)
            ? AppendDatabaseName(overrideConnection!, $"ZachHairStudioTests-{Guid.NewGuid()}")
            : $"Server=(localdb)\\MSSQLLocalDB;Database=ZachHairStudioTests-{Guid.NewGuid()};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<BookingDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<BookingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BookingDbContext>>();

            services.AddDbContext<BookingDbContext>(options =>
                options.UseSqlServer(_connectionString));

            // Checkout / stock concurrency must never hit a real Stripe provider.
            services.RemoveAll<IPaymentProvider>();
            services.AddScoped<IPaymentProvider, FakePaymentProvider>();
        });
    }

    private static string AppendDatabaseName(string connectionString, string databaseName)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName,
        };
        return builder.ConnectionString;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        // Migrate (NOT EnsureCreated) so the real AddBookingCore migration — including the
        // unfiltered unique index that guarantees no double-booking — is what gets applied.
        dbContext.Database.Migrate();

        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        // Drop the throwaway LocalDB database BEFORE the host's service provider is disposed,
        // so runs never accumulate orphaned databases.
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
