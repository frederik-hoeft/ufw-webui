using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ufw.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuleTemplateDisplayNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuleTemplates_Name",
                table: "RuleTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_RuleTemplates_Name",
                table: "RuleTemplates",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RuleTemplates_Name",
                table: "RuleTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_RuleTemplates_Name",
                table: "RuleTemplates",
                column: "Name",
                unique: true);
        }
    }
}
