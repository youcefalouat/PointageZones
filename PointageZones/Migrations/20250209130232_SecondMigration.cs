using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PointageZones.Migrations
{
    /// <inheritdoc />
    public partial class SecondMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Pointages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Zones",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Zones",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Pointages",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Pointages",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
