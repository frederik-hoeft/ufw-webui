using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UfwWebUI.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandUfwRuleModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortRangeEnd",
                table: "UfwRules");

            migrationBuilder.DropColumn(
                name: "PortRangeStart",
                table: "UfwRules");

            migrationBuilder.RenameColumn(
                name: "TargetSubnet",
                table: "UfwRules",
                newName: "Target");

            migrationBuilder.RenameColumn(
                name: "TargetIp",
                table: "UfwRules",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "SourceSubnet",
                table: "UfwRules",
                newName: "Ports");

            migrationBuilder.RenameColumn(
                name: "SourceIp",
                table: "UfwRules",
                newName: "Interface");

            migrationBuilder.RenameColumn(
                name: "Forward",
                table: "UfwRules",
                newName: "IsRoute");

            migrationBuilder.AlterColumn<int>(
                name: "Protocol",
                table: "UfwRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Direction",
                table: "UfwRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direction",
                table: "UfwRules");

            migrationBuilder.RenameColumn(
                name: "Target",
                table: "UfwRules",
                newName: "TargetSubnet");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "UfwRules",
                newName: "TargetIp");

            migrationBuilder.RenameColumn(
                name: "Ports",
                table: "UfwRules",
                newName: "SourceSubnet");

            migrationBuilder.RenameColumn(
                name: "IsRoute",
                table: "UfwRules",
                newName: "Forward");

            migrationBuilder.RenameColumn(
                name: "Interface",
                table: "UfwRules",
                newName: "SourceIp");

            migrationBuilder.AlterColumn<int>(
                name: "Protocol",
                table: "UfwRules",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "PortRangeEnd",
                table: "UfwRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PortRangeStart",
                table: "UfwRules",
                type: "INTEGER",
                nullable: true);
        }
    }
}
