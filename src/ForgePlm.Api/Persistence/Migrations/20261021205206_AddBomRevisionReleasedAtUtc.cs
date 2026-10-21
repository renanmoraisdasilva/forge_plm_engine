using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgePlm.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBomRevisionReleasedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "released_at_utc",
                table: "bom_revisions",
                type: "timestamp with time zone",
                nullable: true);

            // Recover prior release times from immutable audit events. Before this migration,
            // requested future dates could be stored as effectivity even though release happened
            // immediately. The event time is authoritative for both timestamps in this prototype.
            migrationBuilder.Sql(
                """
                WITH release_events AS (
                    SELECT ae.entity_id AS revision_id, ae.occurred_at_utc AS released_at_utc
                    FROM audit_events ae
                    WHERE ae.action = 'RevisionReleased' AND ae.entity_type = 'BomRevision'
                    UNION ALL
                    SELECT (ae.details::jsonb ->> 'revisionId')::uuid AS revision_id,
                           ae.occurred_at_utc AS released_at_utc
                    FROM audit_events ae
                    WHERE ae.action = 'RevisionReleased'
                      AND ae.entity_type = 'EngineeringChangeOrder'
                      AND jsonb_typeof(ae.details::jsonb -> 'revisionId') = 'string'
                ), latest_release AS (
                    SELECT DISTINCT ON (revision_id) revision_id, released_at_utc
                    FROM release_events
                    ORDER BY revision_id, released_at_utc DESC
                )
                UPDATE bom_revisions r
                SET released_at_utc = e.released_at_utc,
                    effective_at_utc = e.released_at_utc
                FROM latest_release e
                WHERE r.id = e.revision_id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE audit_events ae
                SET details = jsonb_set(
                    jsonb_set(ae.details::jsonb, '{releasedAtUtc}', to_jsonb(ae.occurred_at_utc), true),
                    '{effectiveAtUtc}', to_jsonb(ae.occurred_at_utc), true
                )::text
                WHERE ae.action = 'RevisionReleased';
                """);

            // A legacy released revision without an audit event still receives a non-null
            // release timestamp from the best available value.
            migrationBuilder.Sql(
                "UPDATE bom_revisions SET released_at_utc = effective_at_utc " +
                "WHERE status = 'Released' AND released_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "released_at_utc",
                table: "bom_revisions");
        }
    }
}
