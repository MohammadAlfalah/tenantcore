using Microsoft.EntityFrameworkCore;
using TenantCore.Api.Domain.Common;
using TenantCore.Api.Domain.Entities;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Api.Infrastructure.Data;

/// <summary>
/// The application's EF Core database context. It is the single chokepoint that enforces
/// tenant isolation for the entire system via:
///   • Global query filters on every <see cref="ITenantScoped"/> entity (reads are scoped).
///   • An override of SaveChanges that stamps and validates TenantId (writes are scoped).
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Read by the global query filters. Referencing this instance member (rather than a captured
    /// local) is what makes EF Core re-evaluate the tenant on every query instead of baking in the
    /// first value it ever saw.
    /// </summary>
    private Guid? CurrentTenantId => _tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---- Tenant -------------------------------------------------------------------------
        b.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
            e.Property(t => t.Slug).IsRequired().HasMaxLength(120);
            e.HasIndex(t => t.Slug).IsUnique();
        });

        // ---- User ---------------------------------------------------------------------------
        b.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

            // Email is globally unique => a person belongs to exactly one tenant.
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.TenantId);

            e.HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Project ------------------------------------------------------------------------
        b.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(2000);
            e.HasIndex(p => p.TenantId);

            e.HasOne(p => p.Tenant)
                .WithMany(t => t.Projects)
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (not cascade) to avoid multiple cascade paths into User.
            e.HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- ProjectTask --------------------------------------------------------------------
        b.Entity<ProjectTask>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(300);
            e.Property(t => t.Description).HasMaxLength(4000);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(t => t.TenantId);
            e.HasIndex(t => t.ProjectId);

            e.HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unassigning by deleting a member should null the assignee, not delete the task.
            e.HasOne(t => t.Assignee)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssigneeUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- RefreshToken -------------------------------------------------------------------
        b.Entity<RefreshToken>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Token).IsRequired().HasMaxLength(512);
            e.HasIndex(r => r.Token).IsUnique();
            e.HasIndex(r => r.UserId);
            e.Ignore(r => r.IsActive);

            e.HasOne(r => r.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Global tenant query filters ----------------------------------------------------
        // Applied to every entity that implements ITenantScoped. Comparing against the nullable
        // CurrentTenantId means that when no tenant is in scope, the filter matches zero rows.
        b.Entity<User>().HasQueryFilter(u => u.TenantId == CurrentTenantId);
        b.Entity<Project>().HasQueryFilter(p => p.TenantId == CurrentTenantId);
        b.Entity<ProjectTask>().HasQueryFilter(t => t.TenantId == CurrentTenantId);
        b.Entity<RefreshToken>().HasQueryFilter(r => r.TenantId == CurrentTenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyTenantRules();
        return base.SaveChanges();
    }

    /// <summary>
    /// Stamps TenantId on new tenant-scoped rows and refuses to persist any tenant-scoped row whose
    /// TenantId does not match the current tenant. The match check is skipped when there is no tenant
    /// in scope (e.g. during registration, which creates the very first tenant + admin user and sets
    /// TenantId explicitly).
    /// </summary>
    private void ApplyTenantRules()
    {
        var currentTenant = _tenantContext.TenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.TenantId == Guid.Empty && currentTenant.HasValue)
                    entry.Entity.TenantId = currentTenant.Value;
            }

            if (entry.State is EntityState.Added or EntityState.Modified
                && currentTenant.HasValue
                && entry.Entity.TenantId != currentTenant.Value)
            {
                throw new InvalidOperationException(
                    "Cross-tenant write blocked: an entity's TenantId does not match the current tenant.");
            }
        }

        // Keep ProjectTask.UpdatedAt fresh on any modification.
        foreach (var entry in ChangeTracker.Entries<ProjectTask>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
