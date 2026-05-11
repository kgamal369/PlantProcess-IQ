using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStagingRecordEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staging_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_object_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    raw_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processing_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    processing_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    canonical_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    canonical_entity_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    source_system = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_record_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staging_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_staging_records_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_canonical_entity_id",
                table: "staging_records",
                column: "canonical_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id",
                table: "staging_records",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id_is_processed",
                table: "staging_records",
                columns: new[] { "import_batch_id", "is_processed" });

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id_processing_status",
                table: "staging_records",
                columns: new[] { "import_batch_id", "processing_status" });

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id_source_object_name_row_numb",
                table: "staging_records",
                columns: new[] { "import_batch_id", "source_object_name", "row_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staging_records");
        }
    }
}
