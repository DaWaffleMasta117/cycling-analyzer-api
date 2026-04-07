using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CyclingAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRidePowerStreams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RidePowerStreams",
                columns: table => new
                {
                    RideId = table.Column<long>(type: "INTEGER", nullable: false),
                    WattsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DataPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RidePowerStreams", x => x.RideId);
                    table.ForeignKey(
                        name: "FK_RidePowerStreams_Rides_RideId",
                        column: x => x.RideId,
                        principalTable: "Rides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RidePowerStreams");
        }
    }
}
