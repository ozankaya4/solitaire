using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solitaire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLevelToLeaderboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeaderboardEntries_Variant_Score",
                table: "LeaderboardEntries");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "LeaderboardEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_Variant_Level",
                table: "LeaderboardEntries",
                columns: new[] { "Variant", "Level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeaderboardEntries_Variant_Level",
                table: "LeaderboardEntries");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "LeaderboardEntries");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_Variant_Score",
                table: "LeaderboardEntries",
                columns: new[] { "Variant", "Score" });
        }
    }
}
