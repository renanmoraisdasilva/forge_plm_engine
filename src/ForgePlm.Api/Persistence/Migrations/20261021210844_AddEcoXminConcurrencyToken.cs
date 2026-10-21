using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForgePlm.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEcoXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "engineering_change_orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "xmin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "engineering_change_orders");
        }
    }
}
