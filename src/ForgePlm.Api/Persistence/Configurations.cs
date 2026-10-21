using System.Text;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Components;
using ForgePlm.Domain.EngineeringChanges;
using ForgePlm.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace ForgePlm.Api.Persistence;

/// <summary>
/// Schema decisions that carry a rule behind them. Anything not named here is left to EF
/// Core's conventions on purpose: an explicit mapping for every column would be noise, and
/// the constraints that matter are the ones a domain rule depends on.
/// </summary>
internal static class PersistenceModel
{
    /// <summary>Snake_case names throughout, so the schema stays readable in `psql` (ADR-001).</summary>
    public static void ApplySnakeCaseNaming(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            entityType.SetTableName(ToSnakeCase(entityType.GetTableName()!));

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()!));
            }

            foreach (var key in entityType.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            foreach (var index in entityType.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }
        }
    }

    /// <summary>
    /// `SourceEcoId` -> `source_eco_id`, `EcoNumber` -> `eco_number`. A leading run of capitals
    /// is treated as one word (`ID` -> `id`), and a capital after a capital ends the acronym.
    /// </summary>
    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (!char.IsUpper(c))
            {
                builder.Append(c);
                continue;
            }

            var previousIsLower = i > 0 && char.IsLower(name[i - 1]);
            var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

            if (i > 0 && (previousIsLower || nextIsLower))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.PartNumber).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(p => p.Category).HasMaxLength(Product.MaxCategoryLength).IsRequired();
        builder.Property(p => p.LifecycleStatus).HasConversion<string>().HasMaxLength(20).IsRequired();

        // §5.1: unique, case-insensitively. Enforced by the database, not by an application check.
        builder.HasIndex(p => p.PartNumber).IsUnique().HasDatabaseName("ux_products_part_number");

        // ADR-004: the optimistic-concurrency token behind approve/release conflicts.
        //
        // Npgsql EF Core 10 removed the `UseXminAsConcurrencyToken()` convenience extension
        // that earlier versions shipped. Its implementation was exactly these three calls, so
        // they are made explicit here rather than the concurrency defence being dropped:
        //   - `IsConcurrencyToken` makes EF put the ORIGINAL xmin in the UPDATE's WHERE clause
        //   - `ValueGenerated.OnAddOrUpdate` keeps xmin out of the INSERT column list
        //   - `HasDefaultValueSql("xmin")` supplies the system column as the source of the value
        // The property is a shadow property, so `xmin` never appears in the domain model.
        builder.Property<uint>("xmin")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasDefaultValueSql("xmin");
    }
}

internal sealed class ComponentConfiguration : IEntityTypeConfiguration<Component>
{
    public void Configure(EntityTypeBuilder<Component> builder)
    {
        builder.Property(c => c.PartNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(Component.MaxNameLength).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(Component.MaxCategoryLength).IsRequired();
        builder.Property(c => c.LifecycleStatus).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(c => c.PartNumber).IsUnique().HasDatabaseName("ux_components_part_number");
    }
}

internal sealed class BomRevisionConfiguration : IEntityTypeConfiguration<BomRevision>
{
    public void Configure(EntityTypeBuilder<BomRevision> builder)
    {
        builder.Property(r => r.RevisionCode).HasMaxLength(2).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(BomRevision.MaxActorLength).IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // §5.3: codes are assigned per product, so the same code cannot appear twice for one product.
        builder.HasIndex(r => new { r.ProductId, r.RevisionCode })
            .IsUnique()
            .HasDatabaseName("ux_bom_revisions_product_revision_code");

        // ADR-004: at most one Released revision per product — the invariant that actually
        // matters, enforced by the schema rather than by a code path that could be bypassed.
        builder.HasIndex(r => r.ProductId)
            .IsUnique()
            .HasFilter("status = 'Released'")
            .HasDatabaseName("ux_bom_revisions_one_released_per_product");

        // `Lines` is exposed as IReadOnlyList with a `_lines` backing field (spec §5.3: the
        // revision owns its lines and nothing else can add to them). The FK is the BomLine
        // property that already exists, not a string name — naming it as a string makes EF
        // create a second property and collide on the same column.
        builder.HasMany(r => r.Lines)
            .WithOne()
            .HasForeignKey(l => l.BomRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_lines");
    }
}

internal sealed class BomLineConfiguration : IEntityTypeConfiguration<BomLine>
{
    public void Configure(EntityTypeBuilder<BomLine> builder)
    {
        builder.Property(l => l.Unit).HasMaxLength(BomLine.MaxUnitLength).IsRequired();
        builder.Property(l => l.Quantity).IsRequired();

        // A BOM line without its component is not a line, and an inner join would silently
        // drop it while `total` still counted it. The FK makes that state unrepresentable.
        builder.HasOne<Component>()
            .WithMany()
            .HasForeignKey(l => l.ComponentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Belt and braces with the domain rule: the database refuses a duplicate component
        // in the same revision even if application code were to bypass the aggregate.
        builder.HasIndex(l => new { l.BomRevisionId, l.ComponentId })
            .IsUnique()
            .HasDatabaseName("ux_bom_lines_revision_component");
    }
}

internal sealed class EngineeringChangeOrderConfiguration : IEntityTypeConfiguration<EngineeringChangeOrder>
{
    public void Configure(EntityTypeBuilder<EngineeringChangeOrder> builder)
    {
        builder.Property(e => e.EcoNumber).HasMaxLength(EngineeringChangeOrder.MaxEcoNumberLength).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(EngineeringChangeOrder.MaxTitleLength).IsRequired();
        builder.Property(e => e.ReasonForChange).HasMaxLength(EngineeringChangeOrder.MaxTextLength).IsRequired();
        builder.Property(e => e.ImpactAssessment).HasMaxLength(EngineeringChangeOrder.MaxTextLength).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(EngineeringChangeOrder.MaxActorLength).IsRequired();
        builder.Property(e => e.DecidedBy).HasMaxLength(EngineeringChangeOrder.MaxActorLength);
        builder.Property(e => e.ReviewerComment).HasMaxLength(EngineeringChangeOrder.MaxTextLength);

        // §5.4: `ECO-2026-0042` is generated at creation and never reused (A-04).
        builder.HasIndex(e => e.EcoNumber).IsUnique().HasDatabaseName("ux_ecos_eco_number");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Both links are kept for auditability (§5.4): which revision this was written against,
        // and which revision approval produced.
        builder.HasOne<BomRevision>()
            .WithMany()
            .HasForeignKey(e => e.BaseReleasedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BomRevision>()
            .WithMany()
            .HasForeignKey(e => e.ApprovedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // No FK property exists on the line (spec §5.4 keeps it a pure change record), so this
        // names a shadow property. Named in PascalCase like every other identifier; the
        // snake_case pass in PersistenceModel converts it to `engineering_change_order_id`.
        builder.HasMany(e => e.Lines)
            .WithOne()
            .HasForeignKey("EngineeringChangeOrderId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_lines");
    }
}

internal sealed class EngineeringChangeLineConfiguration : IEntityTypeConfiguration<EngineeringChangeLine>
{
    public void Configure(EntityTypeBuilder<EngineeringChangeLine> builder)
    {
        builder.Property(l => l.ChangeType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(l => l.Reason).HasMaxLength(EngineeringChangeLine.MaxReasonLength).IsRequired();

        // The parent link is a shadow property because the line has no back-reference in the
        // domain. Declaring its type explicitly lets the unique index below name it.
        builder.Property<Guid>("EngineeringChangeOrderId");

        // The duplicate-target rule is a domain rule and now also a schema rule. Without this,
        // approval's `FirstOrDefault` would silently discard one of two changes to the same part.
        builder.HasIndex("EngineeringChangeOrderId", nameof(EngineeringChangeLine.TargetComponentId))
            .IsUnique()
            .HasDatabaseName("ux_engineering_change_lines_eco_target");

        // Both component references are real references; without FKs an ECO could point at a
        // part that does not exist and only fail much later, in a read join or an approval.
        builder.HasOne<Component>()
            .WithMany()
            .HasForeignKey(l => l.TargetComponentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Component>()
            .WithMany()
            .HasForeignKey(l => l.ReplacementComponentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.Property(a => a.Actor).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Details).IsRequired();

        // §7: the audit endpoint filters by entity.
        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("ix_audit_events_entity");
    }
}
