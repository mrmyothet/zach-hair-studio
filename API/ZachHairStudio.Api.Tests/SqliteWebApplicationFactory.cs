using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ZachHairStudio.Api.Tests.Features.Payments;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Payments;

namespace ZachHairStudio.Api.Tests;

/// <summary>
/// Relational test host for paths that require <c>ExecuteUpdateAsync</c> (checkout stock).
/// Keeps the default <see cref="CustomWebApplicationFactory"/> on InMemory for unrelated suites.
/// </summary>
public class SqliteWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Synthetic webhook secret for StripeWebhookTests ConstructEvent fixtures.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:WebhookSecret"] = StripeWebhookTests.TestWebhookSecret,
                ["Stripe:SecretKey"] = "sk_test_unused_in_testing_fake_provider",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<BookingDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<BookingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BookingDbContext>>();

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<BookingDbContext>(options =>
                options.UseSqlite(_connection));

            services.RemoveAll<IPaymentProvider>();
            services.AddScoped<IPaymentProvider, FakePaymentProvider>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        dbContext.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
