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
                name: "AudienceQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PollCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Upvotes = table.Column<int>(type: "int", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudienceQuestionUpvotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AudienceQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoterKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudienceQuestionUpvotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    PollCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionIndex = table.Column<int>(type: "int", nullable: false),
                    TextAnswer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AuthorRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VoterToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudienceQuestions_PollCode",
                table: "AudienceQuestions",
                column: "PollCode");

            migrationBuilder.CreateIndex(
                name: "IX_AudienceQuestionUpvotes_AudienceQuestionId_VoterKey",
                table: "AudienceQuestionUpvotes",
                columns: new[] { "AudienceQuestionId", "VoterKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollCode_QuestionId_OptionIndex",
                table: "Votes",
                columns: new[] { "PollCode", "QuestionId", "OptionIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollCode_QuestionId_VoterToken",
                table: "Votes",
                columns: new[] { "PollCode", "QuestionId", "VoterToken" },
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
                name: "AudienceQuestions");

            migrationBuilder.DropTable(
                name: "AudienceQuestionUpvotes");

            migrationBuilder.DropTable(
                name: "Votes");
        }
    }
}
