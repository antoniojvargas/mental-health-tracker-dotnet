using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRedundantDailyLogsUserIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_daily_logs_user_id",
                table: "daily_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_daily_logs_user_id",
                table: "daily_logs",
                column: "user_id");
        }
    }
}
