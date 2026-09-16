using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ufw.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuleMetadataEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleMetadata",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Group = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleMetadataTags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleMetadataId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadata_RuleId",
                table: "RuleMetadata",
                column: "RuleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadataTags_NormalizedName",
                table: "RuleMetadataTags",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_RuleMetadataTags_RuleMetadataId_NormalizedName",
                table: "RuleMetadataTags",
                columns: ["RuleMetadataId", "NormalizedName"],
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleMetadataTags");

            migrationBuilder.DropTable(
                name: "RuleMetadata");
        }
    }
}
