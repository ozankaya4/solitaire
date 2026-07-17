using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solitaire.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentLevel",
                table: "PlayerStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ElapsedMs",
                table: "GameSaves",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentLevel",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "ElapsedMs",
                table: "GameSaves");
        }
    }
}
