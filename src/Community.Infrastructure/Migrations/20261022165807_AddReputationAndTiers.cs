using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Community.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReputationAndTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaderboardSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodType = table.Column<string>(type: "text", nullable: false),
                    PeriodCode = table.Column<string>(type: "text", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    PermanentReputationPoints = table.Column<int>(type: "integer", nullable: false),
                    TrustSafetyScore = table.Column<int>(type: "integer", nullable: false),
                    CurrentTierLevel = table.Column<int>(type: "integer", nullable: false),
                    TierTitle = table.Column<string>(type: "text", nullable: false),
                    WeeklyContributionScore = table.Column<int>(type: "integer", nullable: false),
                    MonthlyContributionScore = table.Column<int>(type: "integer", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTssPassiveRecoveryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveCleanWeeks = table.Column<int>(type: "integer", nullable: false),
                    ConsecutiveInactiveWeeks = table.Column<int>(type: "integer", nullable: false),
                    IsSanctioned = table.Column<bool>(type: "boolean", nullable: false),
                    SanctionLevel = table.Column<int>(type: "integer", nullable: false),
                    SanctionExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SanctionReason = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSanctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SanctionLevel = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSanctions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    PostsCount = table.Column<int>(type: "integer", nullable: false),
                    LikesCount = table.Column<int>(type: "integer", nullable: false),
                    TierTitle = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardEntries_LeaderboardSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "LeaderboardSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReputationTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserReputationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    PointsChanged = table.Column<int>(type: "integer", nullable: false),
                    TssChanged = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReputationTransactions_UserReputations_UserReputationId",
                        column: x => x.UserReputationId,
                        principalTable: "UserReputations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardEntries_SnapshotId",
                table: "LeaderboardEntries",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationTransactions_UserReputationId",
                table: "ReputationTransactions",
                column: "UserReputationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardEntries");

            migrationBuilder.DropTable(
                name: "ReputationTransactions");

            migrationBuilder.DropTable(
                name: "UserSanctions");

            migrationBuilder.DropTable(
                name: "LeaderboardSnapshots");

            migrationBuilder.DropTable(
                name: "UserReputations");
        }
    }
}
