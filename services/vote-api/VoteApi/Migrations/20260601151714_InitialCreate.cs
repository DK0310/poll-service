using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoteApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PollCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OptionIndex = table.Column<int>(type: "int", nullable: false),
                    VoterToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollCode_OptionIndex",
                table: "Votes",
                columns: new[] { "PollCode", "OptionIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollCode_VoterToken",
                table: "Votes",
                columns: new[] { "PollCode", "VoterToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VotedAt",
                table: "Votes",
                column: "VotedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Votes");
        }
    }
}
