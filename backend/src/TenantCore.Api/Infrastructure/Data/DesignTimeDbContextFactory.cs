using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Api.Infrastructure.Data;

/// <summary>
/// Used only by the EF Core CLI tools (e.g. <c>dotnet ef migrations add</c>). It builds an
/// <see cref="AppDbContext"/> without booting the whole web app, so migrations never try to connect
/// to or migrate a live database at design time. The connection string is a placeholder — it is only
/// used to pick the Npgsql provider, never opened during scaffolding.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=tenantcore;Username=tenantcore;Password=tenantcore";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // A design-time tenant context with no tenant — migrations don't read tenant-scoped data.
        return new AppDbContext(options, new TenantContext());
    }
}
