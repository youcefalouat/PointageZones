using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PointageZones.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationAndUpdatePointageAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "lastUpdate",
                table: "Zones",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateTimeDebTour",
                table: "Pointages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateTimeFinTour",
                table: "Pointages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Last_Update",
                table: "Pointages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObservationId",
                table: "Pointages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ref_User_Assign",
                table: "Pointages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ref_User_Update",
                table: "Pointages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Observations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Observations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pointages_ObservationId",
                table: "Pointages",
                column: "ObservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pointages_Observations_ObservationId",
                table: "Pointages",
                column: "ObservationId",
                principalTable: "Observations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pointages_Observations_ObservationId",
                table: "Pointages");

            migrationBuilder.DropTable(
                name: "Observations");

            migrationBuilder.DropIndex(
                name: "IX_Pointages_ObservationId",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "DateTimeDebTour",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "DateTimeFinTour",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "Last_Update",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "ObservationId",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "Ref_User_Assign",
                table: "Pointages");

            migrationBuilder.DropColumn(
                name: "Ref_User_Update",
                table: "Pointages");

            migrationBuilder.AlterColumn<DateTime>(
                name: "lastUpdate",
                table: "Zones",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
