using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PointageZones.Migrations
{
    /// <inheritdoc />
    public partial class NewVersionZonePointage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pointages_Tours_TourId",
                table: "Pointages");

            migrationBuilder.DropForeignKey(
                name: "FK_Pointages_Zones_ZoneId",
                table: "Pointages");

            migrationBuilder.DropIndex(
                name: "IX_Pointages_TourId",
                table: "Pointages");

            migrationBuilder.RenameColumn(
                name: "ZoneId",
                table: "Pointages",
                newName: "PlanTourId");

            migrationBuilder.RenameColumn(
                name: "TourId",
                table: "Pointages",
                newName: "IsValid");

            migrationBuilder.RenameIndex(
                name: "IX_Pointages_ZoneId",
                table: "Pointages",
                newName: "IX_Pointages_PlanTourId");

            migrationBuilder.AddColumn<string>(
                name: "Tag",
                table: "Zones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lastUpdate",
                table: "Zones",
                type: "datetime2",
                nullable: true,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DateTimeAssign",
                table: "Pointages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Pointages_PlanTours_PlanTourId",
                table: "Pointages",
                column: "PlanTourId",
                principalTable: "PlanTours",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pointages_PlanTours_PlanTourId",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "Tag",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "lastUpdate",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "DateTimeAssign",
                table: "Pointages");

            migrationBuilder.RenameColumn(
                name: "PlanTourId",
                table: "Pointages",
                newName: "ZoneId");

            migrationBuilder.RenameColumn(
                name: "IsValid",
                table: "Pointages",
                newName: "TourId");

            migrationBuilder.RenameIndex(
                name: "IX_Pointages_PlanTourId",
                table: "Pointages",
                newName: "IX_Pointages_ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Pointages_TourId",
                table: "Pointages",
                column: "TourId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pointages_Tours_TourId",
                table: "Pointages",
                column: "TourId",
                principalTable: "Tours",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pointages_Zones_ZoneId",
                table: "Pointages",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
