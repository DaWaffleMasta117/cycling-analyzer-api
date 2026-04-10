using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyclingAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRideColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageHeartRate",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "MaxHeartRate",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "WeightKgAtTime",
                table: "Rides");

            migrationBuilder.AddColumn<float>(
                name: "NormalizedPowerWatts",
                table: "Rides",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizedPowerWatts",
                table: "Rides");

            migrationBuilder.AddColumn<float>(
                name: "AverageHeartRate",
                table: "Rides",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "MaxHeartRate",
                table: "Rides",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "WeightKgAtTime",
                table: "Rides",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }
    }
}
