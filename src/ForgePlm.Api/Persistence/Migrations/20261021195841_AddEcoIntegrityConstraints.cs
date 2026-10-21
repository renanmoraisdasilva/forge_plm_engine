using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgePlm.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEcoIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_engineering_change_lines_engineering_change_order_id",
                table: "engineering_change_lines");

            // No `defaultValue` on purpose: a backfill of Guid.Empty would leave a permanent
            // DEFAULT on the column and produce an FK violation instead of a clear NOT NULL
            // failure. Existing rows are expected to have a parent; if one does not, the ALTER
            // should fail and surface the real data problem.
            migrationBuilder.AlterColumn<Guid>(
                name: "engineering_change_order_id",
                table: "engineering_change_lines",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_engineering_change_lines_replacement_component_id",
                table: "engineering_change_lines",
                column: "replacement_component_id");

            migrationBuilder.CreateIndex(
                name: "ix_engineering_change_lines_target_component_id",
                table: "engineering_change_lines",
                column: "target_component_id");

            migrationBuilder.CreateIndex(
                name: "ux_engineering_change_lines_eco_target",
                table: "engineering_change_lines",
                columns: new[] { "engineering_change_order_id", "target_component_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bom_lines_component_id",
                table: "bom_lines",
                column: "component_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bom_lines_components_component_id",
                table: "bom_lines",
                column: "component_id",
                principalTable: "components",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_engineering_change_lines_components_replacement_component_id",
                table: "engineering_change_lines",
                column: "replacement_component_id",
                principalTable: "components",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_engineering_change_lines_components_target_component_id",
                table: "engineering_change_lines",
                column: "target_component_id",
                principalTable: "components",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bom_lines_components_component_id",
                table: "bom_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_engineering_change_lines_components_replacement_component_id",
                table: "engineering_change_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_engineering_change_lines_components_target_component_id",
                table: "engineering_change_lines");

            migrationBuilder.DropIndex(
                name: "ix_engineering_change_lines_replacement_component_id",
                table: "engineering_change_lines");

            migrationBuilder.DropIndex(
                name: "ix_engineering_change_lines_target_component_id",
                table: "engineering_change_lines");

            migrationBuilder.DropIndex(
                name: "ux_engineering_change_lines_eco_target",
                table: "engineering_change_lines");

            migrationBuilder.DropIndex(
                name: "ix_bom_lines_component_id",
                table: "bom_lines");

            migrationBuilder.AlterColumn<Guid>(
                name: "engineering_change_order_id",
                table: "engineering_change_lines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "ix_engineering_change_lines_engineering_change_order_id",
                table: "engineering_change_lines",
                column: "engineering_change_order_id");
        }
    }
}
