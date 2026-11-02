using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace FinDoc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "IndexFailureCode",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexFailureMessage",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndexStatus",
                table: "Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IndexingAttempts",
                table: "Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IndexingLeaseUntil",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChunkEmbeddings",
                columns: table => new
                {
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vector = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkEmbeddings", x => x.ChunkId);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings_Chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalTable: "Chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IndexStatus_Status_CreatedAt",
                table: "Documents",
                columns: new[] { "IndexStatus", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings_DocumentId",
                table: "ChunkEmbeddings",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings_TenantId_DocumentId",
                table: "ChunkEmbeddings",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.Sql("""
                CREATE INDEX "IX_ChunkEmbeddings_Vector"
                ON "ChunkEmbeddings" USING hnsw ("Vector" vector_cosine_ops)
                WITH (m = 16, ef_construction = 64);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChunkEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_Documents_IndexStatus_Status_CreatedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexFailureCode",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexFailureMessage",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexingAttempts",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IndexingLeaseUntil",
                table: "Documents");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
