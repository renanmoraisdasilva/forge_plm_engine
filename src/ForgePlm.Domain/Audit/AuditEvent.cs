namespace ForgePlm.Domain.Audit;

/// <summary>
/// The actions worth recording (spec §5.5). A closed set on purpose: an audit trail where
/// anything can be logged is a log file, not an audit trail.
/// </summary>
public static class AuditActions
{
    public const string ProductCreated = "ProductCreated";
    public const string ComponentCreated = "ComponentCreated";
    public const string EcoCreated = "EcoCreated";
    public const string EcoSubmitted = "EcoSubmitted";
    public const string EcoApproved = "EcoApproved";
    public const string EcoRejected = "EcoRejected";
    public const string RevisionReleased = "RevisionReleased";
}

/// <summary>
/// An append-only record of a meaningful domain action (spec §5.5).
///
/// Written in the same transaction as the action it describes, so an audit row and the
/// change it claims cannot drift apart. Application code may insert; nothing may edit or
/// delete. This is a historical record, not event sourcing — the domain state lives in the
/// aggregates, and these rows exist to explain how it got there.
/// </summary>
public class AuditEvent
{
    public const int MaxActorLength = 200;

    public Guid Id { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Demo identity only. This is not authentication; see spec §16.</summary>
    public string Actor { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    /// <summary>Small JSON payload: the identifiers and before/after summary worth keeping.</summary>
    public string Details { get; private set; } = "{}";

    private AuditEvent()
    {
        // EF Core materialisation only.
    }

    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static AuditEvent Record(
        string actor,
        string action,
        string entityType,
        Guid entityId,
        string? detailsJson,
        DateTime utcNow,
        Guid? id = null)
    {
        return new AuditEvent
        {
            Id = id ?? Guid.NewGuid(),
            OccurredAtUtc = utcNow,
            Actor = Common.DomainText.Required(actor, MaxActorLength, "Audit actor", Common.DomainErrorCodes.BlankActor),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson,
        };
    }
}
