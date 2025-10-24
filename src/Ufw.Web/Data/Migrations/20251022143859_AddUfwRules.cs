using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UfwWebUI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUfwRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UfwRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Forward = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceIp = table.Column<string>(type: "TEXT", nullable: true),
                    SourceSubnet = table.Column<string>(type: "TEXT", nullable: true),
                    TargetIp = table.Column<string>(type: "TEXT", nullable: true),
                    TargetSubnet = table.Column<string>(type: "TEXT", nullable: true),
                    Protocol = table.Column<int>(type: "INTEGER", nullable: true),
                    PortRangeStart = table.Column<int>(type: "INTEGER", nullable: true),
                    PortRangeEnd = table.Column<int>(type: "INTEGER", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UfwRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UfwRules_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UfwRules_AuthorId",
                table: "UfwRules",
                column: "AuthorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UfwRules");
        }
    }
}
