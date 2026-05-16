using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3ConnectorFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connection_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_profile_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    connection_profile_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    connection_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    host_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    database_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    schema_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    file_root_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    api_base_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    secret_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    connection_options_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    read_only_enforced = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    last_tested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_test_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_test_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_connection_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_connection_profiles_source_system_definitions_source_system",
                        column: x => x.source_system_definition_id,
                        principalTable: "source_system_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "source_dataset_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dataset_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dataset_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dataset_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_object_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source_schema_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    primary_timestamp_field = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    incremental_cursor_field = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_cursor_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    refresh_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    dataset_options_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_source_dataset_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_dataset_definitions_connection_profiles_connection_p",
                        column: x => x.connection_profile_id,
                        principalTable: "connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_field_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_dataset_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_data_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    is_nullable = table.Column<bool>(type: "boolean", nullable: false),
                    max_length = table.Column<int>(type: "integer", nullable: true),
                    numeric_precision = table.Column<int>(type: "integer", nullable: true),
                    numeric_scale = table.Column<int>(type: "integer", nullable: true),
                    sample_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_primary_key_candidate = table.Column<bool>(type: "boolean", nullable: false),
                    is_timestamp_candidate = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_source_field_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_field_definitions_source_dataset_definitions_source_",
                        column: x => x.source_dataset_definition_id,
                        principalTable: "source_dataset_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_connection_profile_code",
                table: "connection_profiles",
                column: "connection_profile_code",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_is_active",
                table: "connection_profiles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_provider_type",
                table: "connection_profiles",
                column: "provider_type");

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_source_system_definition_id",
                table: "connection_profiles",
                column: "source_system_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_connection_profile_id",
                table: "source_dataset_definitions",
                column: "connection_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_connection_profile_id_dataset_co",
                table: "source_dataset_definitions",
                columns: new[] { "connection_profile_id", "dataset_code" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_dataset_kind",
                table: "source_dataset_definitions",
                column: "dataset_kind");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_is_active",
                table: "source_dataset_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_source_object_name",
                table: "source_dataset_definitions",
                column: "source_object_name");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_field_name",
                table: "source_field_definitions",
                column: "field_name");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_ordinal",
                table: "source_field_definitions",
                column: "ordinal");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_source_data_type",
                table: "source_field_definitions",
                column: "source_data_type");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_source_dataset_definition_id",
                table: "source_field_definitions",
                column: "source_dataset_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_source_dataset_definition_id_field",
                table: "source_field_definitions",
                columns: new[] { "source_dataset_definition_id", "field_name" },
                unique: true,
                filter: "is_deleted = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_field_definitions");

            migrationBuilder.DropTable(
                name: "source_dataset_definitions");

            migrationBuilder.DropTable(
                name: "connection_profiles");
        }
    }
}
