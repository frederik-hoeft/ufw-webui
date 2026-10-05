using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace Ufw.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuleTemplatePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "citext", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    AddressFamily = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Protocol = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    SourcePorts = table.Column<string>(type: "text", nullable: true),
                    SourceInterface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Destination = table.Column<string>(type: "text", nullable: false),
                    DestinationPorts = table.Column<string>(type: "text", nullable: true),
                    DestinationInterface = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Comment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GroupId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleTemplates_RuleGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "RuleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RuleTemplateTags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    TagId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleTemplateTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleTemplateTags_RuleTags_TagId",
                        column: x => x.TagId,
                        principalTable: "RuleTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RuleTemplateTags_RuleTemplates_RuleTemplateId",
                        column: x => x.RuleTemplateId,
                        principalTable: "RuleTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleTemplates_GroupId",
                table: "RuleTemplates",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleTemplates_Name",
                table: "RuleTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleTemplates_PublicId",
                table: "RuleTemplates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleTemplateTags_RuleTemplateId_TagId",
                table: "RuleTemplateTags",
                columns: ["RuleTemplateId", "TagId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleTemplateTags_TagId",
                table: "RuleTemplateTags",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleTemplateTags");

            migrationBuilder.DropTable(
                name: "RuleTemplates");
        }
    }
}
