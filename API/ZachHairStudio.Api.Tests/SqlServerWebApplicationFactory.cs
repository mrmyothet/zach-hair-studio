using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ZachHairStudio.Shared.Db;

namespace ZachHairStudio.Api.Tests;

// Real SQL Server LocalDB fixture. Unlike CustomWebApplicationFactory (InMemory, which
// enforces no unique indexes), this migrates the actual AddBookingCore schema so the
// unfiltered unique index (SC4 double-booking) and datetimeoffset round-trip (SC5) are
// exercised against real SQL Server semantics. Uses a per-run unique database dropped on dispose.
public class SqlServerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database=ZachHairStudioTests-{Guid.NewGuid()};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

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
        });
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
