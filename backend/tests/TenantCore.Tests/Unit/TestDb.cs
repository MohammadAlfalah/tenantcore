using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TenantCore.Api.Infrastructure.Data;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Tests.Unit;

/// <summary>
/// Helpers for spinning up isolated in-memory <see cref="AppDbContext"/> instances that share one
/// underlying store, so we can simulate multiple "requests" (each with its own tenant context)
/// hitting the same database — exactly how cross-tenant access would be attempted in production.
/// </summary>
public static class TestDb
{
    /// <summary>Creates a fresh in-memory store and returns a factory for contexts bound to a given tenant.</summary>
    public static Func<ITenantContext, AppDbContext> NewStore()
    {
        var root = new InMemoryDatabaseRoot();
        var dbName = Guid.NewGuid().ToString();

        return tenantContext =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName, root)
                .Options;
            return new AppDbContext(options, tenantContext);
        };
    }

    public static ITenantContext TenantFor(Guid tenantId, Guid? userId = null)
    {
        var ctx = new TenantContext();
        ctx.SetContext(tenantId, userId ?? Guid.NewGuid());
        return ctx;
    }
}
