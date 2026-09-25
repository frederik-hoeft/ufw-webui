using Microsoft.EntityFrameworkCore.Migrations;
using Ufw.Web.Model.V1.KnownHosts;

#nullable disable

namespace Ufw.Web.Data.Migrations;

public partial class KnownHostDnsResolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<KnownHostAddressSource>(
            name: "AddressSource",
            table: "KnownHosts",
            type: "integer",
            nullable: false,
            defaultValue: KnownHostAddressSource.Literal);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DnsResolvedAt",
            table: "KnownHosts",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AddressSource", table: "KnownHosts");
        migrationBuilder.DropColumn(name: "DnsResolvedAt", table: "KnownHosts");
    }
}
