using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkerHeartbeats",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastBeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerHeartbeats", x => x.Name);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkerHeartbeats");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents");
        }
    }
}
