using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LikesCount = table.Column<int>(type: "integer", nullable: true),
                    CommentsCount = table.Column<int>(type: "integer", nullable: true),
                    SharesCount = table.Column<int>(type: "integer", nullable: true),
                    ViewsCount = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    IsApproved = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<int>(type: "integer", nullable: true),
                    IsPinned = table.Column<int>(type: "integer", nullable: true),
                    IsLiked = table.Column<int>(type: "integer", nullable: true),
                    IsCommented = table.Column<int>(type: "integer", nullable: true),
                    IsShared = table.Column<int>(type: "integer", nullable: true),
                    IsViewed = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Posts");
        }
    }
}
