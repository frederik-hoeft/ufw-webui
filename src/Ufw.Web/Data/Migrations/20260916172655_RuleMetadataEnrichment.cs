using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ufw.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuleMetadataEnrichment : Migration
    {
        private static readonly string[] s_ruleMetadataTagColumns = ["RuleMetadataId", "TagId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "RuleMetadata",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleTags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "citext", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleMetadataTags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleMetadataId = table.Column<long>(type: "bigint", nullable: false),
                    TagId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleMetadataTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleMetadataTags_RuleMetadata_RuleMetadataId",
                        column: x => x.RuleMetadataId,
                        principalTable: "RuleMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuleMetadataTags_RuleTags_TagId",
                        column: x => x.TagId,
                        principalTable: "RuleTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadata_PublicId",
                table: "RuleMetadata",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadata_RuleId",
                table: "RuleMetadata",
                column: "RuleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadataTags_RuleMetadataId_TagId",
                table: "RuleMetadataTags",
                columns: s_ruleMetadataTagColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadataTags_TagId",
                table: "RuleMetadataTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleTags_Name",
                table: "RuleTags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleTags_PublicId",
                table: "RuleTags",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleMetadataTags");

            migrationBuilder.DropTable(
                name: "RuleMetadata");

            migrationBuilder.DropTable(
                name: "RuleTags");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");
        }
    }
}
