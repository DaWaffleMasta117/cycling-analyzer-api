using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyclingAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AthleteId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DistanceMeters = table.Column<float>(type: "REAL", nullable: false),
                    MovingTimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ElevationGainMeters = table.Column<float>(type: "REAL", nullable: false),
                    AveragePowerWatts = table.Column<float>(type: "REAL", nullable: false),
                    MaxPowerWatts = table.Column<float>(type: "REAL", nullable: false),
                    AverageHeartRate = table.Column<float>(type: "REAL", nullable: false),
                    MaxHeartRate = table.Column<float>(type: "REAL", nullable: false),
                    AverageSpeedMs = table.Column<float>(type: "REAL", nullable: false),
                    WeightKgAtTime = table.Column<float>(type: "REAL", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rides_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_AthleteId_StartDate",
                table: "Rides",
                columns: new[] { "AthleteId", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rides");
        }
    }
}
