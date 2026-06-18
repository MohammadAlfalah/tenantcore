using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenantCore.Api.Infrastructure.Data;

namespace TenantCore.Tests.Integration;

/// <summary>
/// Boots the real API in-process but swaps PostgreSQL for a uniquely-named in-memory database so the
/// full HTTP + auth + tenant-isolation pipeline is exercised without any external dependencies.
/// </summary>
public sealed class TenantCoreWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Drop inherited sources (the API's appsettings.json, env vars) so the test owns the
            // configuration outright. This guarantees the JWT secret used to SIGN tokens is identical
            // to the one used to VALIDATE them — otherwise source ordering can make them diverge.
            config.Sources.Clear();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Program.cs requires these to be present; the connection string is never opened
                // because the DbContext below is replaced with the in-memory provider.
                ["ConnectionStrings:Default"] = "Host=test;Database=test;Username=test;Password=test",
                ["Jwt:Secret"] = "integration_test_signing_secret_at_least_32_chars_long",
                ["Jwt:Issuer"] = "TenantCore",
                ["Jwt:Audience"] = "TenantCore",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the Npgsql-backed AppDbContext (and EF Core 9+ option-configuration) registrations.
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                || d.ServiceType == typeof(AppDbContext)
                || (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true
                    && d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}
