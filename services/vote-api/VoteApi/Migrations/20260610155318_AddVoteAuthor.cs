using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVoteAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "Votes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorRole",
                table: "Votes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "AuthorRole",
                table: "Votes");
        }
    }
}
