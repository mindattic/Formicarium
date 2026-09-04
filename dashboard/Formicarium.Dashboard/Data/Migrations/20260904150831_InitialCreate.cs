using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formicarium.Dashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuildProgress",
                columns: table => new
                {
                    Step = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildProgress", x => x.Step);
                });

            migrationBuilder.CreateTable(
                name: "RiserSamples",
                columns: table => new
                {
                    Ts = table.Column<long>(type: "bigint", nullable: false),
                    Riser = table.Column<int>(type: "int", nullable: false),
                    Cumulative = table.Column<long>(type: "bigint", nullable: false),
                    Crossings = table.Column<long>(type: "bigint", nullable: false),
                    Rate = table.Column<double>(type: "float", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Brightness = table.Column<double>(type: "float", nullable: false),
                    DayNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiserSamples", x => new { x.Ts, x.Riser });
                });

            migrationBuilder.CreateTable(
                name: "Samples",
                columns: table => new
                {
                    Ts = table.Column<long>(type: "bigint", nullable: false),
                    NestBottomC = table.Column<double>(type: "float", nullable: true),
                    NestTopC = table.Column<double>(type: "float", nullable: true),
                    OutworldC = table.Column<double>(type: "float", nullable: true),
                    NestRh = table.Column<double>(type: "float", nullable: true),
                    OutworldRh = table.Column<double>(type: "float", nullable: true),
                    SoilPct = table.Column<double>(type: "float", nullable: true),
                    Heater = table.Column<bool>(type: "bit", nullable: false),
                    Refill = table.Column<bool>(type: "bit", nullable: false),
                    Feed = table.Column<bool>(type: "bit", nullable: false),
                    Fan = table.Column<bool>(type: "bit", nullable: false),
                    NestFan = table.Column<bool>(type: "bit", nullable: false),
                    Faults = table.Column<int>(type: "int", nullable: false),
                    LightingMode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Ts);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiserSamples_Channel",
                table: "RiserSamples",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_RiserSamples_DayNumber_Riser",
                table: "RiserSamples",
                columns: new[] { "DayNumber", "Riser" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildProgress");

            migrationBuilder.DropTable(
                name: "RiserSamples");

            migrationBuilder.DropTable(
                name: "Samples");
        }
    }
}
