using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PointageZones.Migrations
{
    /// <inheritdoc />
    public partial class AjoutOrdreClassementPlanTour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            
            migrationBuilder.AddColumn<int>(
                name: "Ordre",
                table: "PlanTours",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlanTour_TourId_Ordre",
                table: "PlanTours",
                columns: new[] { "TourId", "Ordre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanTour_TourId_ZoneId",
                table: "PlanTours",
                columns: new[] { "TourId", "ZoneId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanTour_TourId_Ordre",
                table: "PlanTours");

            migrationBuilder.DropIndex(
                name: "IX_PlanTour_TourId_ZoneId",
                table: "PlanTours");

            migrationBuilder.DropColumn(
                name: "Ordre",
                table: "PlanTours");
                       
        }
    }
}
