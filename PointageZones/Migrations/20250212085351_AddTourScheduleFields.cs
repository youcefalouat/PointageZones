using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PointageZones.Migrations
{
    /// <inheritdoc />
    public partial class AddTourScheduleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "DebTour",
                table: "Tours",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "FinTour",
                table: "Tours",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrqTourMin",
                table: "Tours",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebTour",
                table: "Tours");

            migrationBuilder.DropColumn(
                name: "FinTour",
                table: "Tours");

            migrationBuilder.DropColumn(
                name: "FrqTourMin",
                table: "Tours");
        }
    }
}
