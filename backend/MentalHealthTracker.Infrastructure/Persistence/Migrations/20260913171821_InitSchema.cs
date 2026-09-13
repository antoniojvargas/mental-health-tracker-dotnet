using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentalHealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    google_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    log_date = table.Column<DateOnly>(type: "date", nullable: false),
                    mood_rating = table.Column<short>(type: "smallint", nullable: false),
                    anxiety_level = table.Column<short>(type: "smallint", nullable: false),
                    stress_level = table.Column<short>(type: "smallint", nullable: false),
                    sleep_hours = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    sleep_quality = table.Column<short>(type: "smallint", nullable: false),
                    sleep_disturbances = table.Column<List<string>>(type: "text[]", nullable: false),
                    activity_type = table.Column<string>(type: "text", nullable: true),
                    activity_minutes = table.Column<short>(type: "smallint", nullable: true),
                    social_frequency = table.Column<string>(type: "text", nullable: false),
                    symptoms = table.Column<string>(type: "jsonb", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_logs_user_id",
                table: "daily_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_logs_user_id_log_date",
                table: "daily_logs",
                columns: new[] { "user_id", "log_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_google_id",
                table: "users",
                column: "google_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_logs");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
