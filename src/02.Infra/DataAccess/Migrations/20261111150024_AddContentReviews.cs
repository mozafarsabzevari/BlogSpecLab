using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogSpecLab.Infra.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddContentReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewedPostVersion = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewedPostModifiedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SchemaVersion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Issues = table.Column<string>(type: "TEXT", nullable: true),
                    MissingInformation = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentReviews_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReviews_PostId_CreatedAt",
                table: "ContentReviews",
                columns: new[] { "PostId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentReviews");
        }
    }
}
