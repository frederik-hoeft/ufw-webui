using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UfwWebUI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnabledField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "UfwRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "UfwRules");
        }
    }
}
