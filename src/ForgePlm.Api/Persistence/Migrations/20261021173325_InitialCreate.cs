using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgePlm.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    details = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_components", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false, defaultValueSql: "xmin")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bom_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    effective_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_eco_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bom_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_bom_revisions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bom_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bom_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bom_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_bom_lines_bom_revisions_bom_revision_id",
                        column: x => x.bom_revision_id,
                        principalTable: "bom_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "engineering_change_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    eco_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_released_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_for_change = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    impact_assessment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    requested_effective_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reviewer_comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    approved_revision_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_engineering_change_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_engineering_change_orders_bom_revisions_approved_revision_id",
                        column: x => x.approved_revision_id,
                        principalTable: "bom_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_engineering_change_orders_bom_revisions_base_released_revis~",
                        column: x => x.base_released_revision_id,
                        principalTable: "bom_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_engineering_change_orders_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "engineering_change_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    replacement_component_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_quantity = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    engineering_change_order_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_engineering_change_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_engineering_change_lines_engineering_change_orders_engineer~",
                        column: x => x.engineering_change_order_id,
                        principalTable: "engineering_change_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ux_bom_lines_revision_component",
                table: "bom_lines",
                columns: new[] { "bom_revision_id", "component_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bom_revisions_one_released_per_product",
                table: "bom_revisions",
                column: "product_id",
                unique: true,
                filter: "status = 'Released'");

            migrationBuilder.CreateIndex(
                name: "ux_bom_revisions_product_revision_code",
                table: "bom_revisions",
                columns: new[] { "product_id", "revision_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_components_part_number",
                table: "components",
                column: "part_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_engineering_change_lines_engineering_change_order_id",
                table: "engineering_change_lines",
                column: "engineering_change_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_engineering_change_orders_approved_revision_id",
                table: "engineering_change_orders",
                column: "approved_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_engineering_change_orders_base_released_revision_id",
                table: "engineering_change_orders",
                column: "base_released_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_engineering_change_orders_product_id",
                table: "engineering_change_orders",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_ecos_eco_number",
                table: "engineering_change_orders",
                column: "eco_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_products_part_number",
                table: "products",
                column: "part_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "bom_lines");

            migrationBuilder.DropTable(
                name: "components");

            migrationBuilder.DropTable(
                name: "engineering_change_lines");

            migrationBuilder.DropTable(
                name: "engineering_change_orders");

            migrationBuilder.DropTable(
                name: "bom_revisions");

            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
