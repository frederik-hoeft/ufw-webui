using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ufw.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class NetworkInterfaceInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetworkInterfaceCacheState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ReconciledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInterfaceCacheState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkInterfaces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Comment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInterfaces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInterfaces_Name",
                table: "NetworkInterfaces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInterfaces_PublicId",
                table: "NetworkInterfaces",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkInterfaceCacheState");

            migrationBuilder.DropTable(
                name: "NetworkInterfaces");
        }
    }
}
