using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ufw.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuleGroupPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GroupId",
                table: "RuleMetadata",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RuleGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "citext", maxLength: 64, nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadata_GroupId",
                table: "RuleMetadata",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleGroups_Name",
                table: "RuleGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleGroups_PublicId",
                table: "RuleGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RuleMetadata_RuleGroups_GroupId",
                table: "RuleMetadata",
                column: "GroupId",
                principalTable: "RuleGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RuleMetadata_RuleGroups_GroupId",
                table: "RuleMetadata");

            migrationBuilder.DropTable(
                name: "RuleGroups");

            migrationBuilder.DropIndex(
                name: "IX_RuleMetadata_GroupId",
                table: "RuleMetadata");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "RuleMetadata");
        }
    }
}
