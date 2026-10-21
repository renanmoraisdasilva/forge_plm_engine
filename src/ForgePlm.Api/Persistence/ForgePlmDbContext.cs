using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Components;
using ForgePlm.Domain.EngineeringChanges;
using ForgePlm.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Persistence;

/// <summary>
/// The single DbContext for the application (spec §4). There is no repository layer and no
/// unit-of-work abstraction: application services use this context directly, and EF Core's
/// SaveChanges is the transaction boundary for approve and release.
/// </summary>
public class ForgePlmDbContext(DbContextOptions<ForgePlmDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Component> Components => Set<Component>();

    public DbSet<BomRevision> BomRevisions => Set<BomRevision>();

    public DbSet<BomLine> BomLines => Set<BomLine>();

    public DbSet<EngineeringChangeOrder> EngineeringChangeOrders => Set<EngineeringChangeOrder>();

    public DbSet<EngineeringChangeLine> EngineeringChangeLines => Set<EngineeringChangeLine>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ForgePlmDbContext).Assembly);
        PersistenceModel.ApplySnakeCaseNaming(modelBuilder);
    }
}
