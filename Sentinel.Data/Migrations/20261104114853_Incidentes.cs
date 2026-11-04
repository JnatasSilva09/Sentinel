using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentinel.Data.Migrations
{
    /// <inheritdoc />
    public partial class Incidentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertaOrigemId",
                table: "Incidents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_AlertaOrigemId",
                table: "Incidents",
                column: "AlertaOrigemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_DataAbertura",
                table: "Incidents",
                column: "DataAbertura");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status",
                table: "Incidents",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Alerts_AlertaOrigemId",
                table: "Incidents",
                column: "AlertaOrigemId",
                principalTable: "Alerts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Alerts_AlertaOrigemId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_AlertaOrigemId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_DataAbertura",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_Status",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "AlertaOrigemId",
                table: "Incidents");
        }
    }
}
