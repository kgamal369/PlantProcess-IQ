using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add2ndDayDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    import_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_object_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    checksum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    row_count = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_batches_source_system_definitions_source_system_defi",
                        column: x => x.source_system_definition_id,
                        principalTable: "source_system_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "industry_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    template_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    industry_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("pk_industry_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mapping_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mapping_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_object_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mapping_json = table.Column<string>(type: "jsonb", nullable: false),
                    mapping_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("pk_mapping_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_mapping_definitions_source_system_definitions_source_system",
                        column: x => x.source_system_definition_id,
                        principalTable: "source_system_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_unit_type_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    industry_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_type_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    material_unit_type_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_material_unit_type_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_unit_type_definitions_industry_templates_industry_",
                        column: x => x.industry_template_id,
                        principalTable: "industry_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operation_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    industry_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    operation_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_operation_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_operation_definitions_industry_templates_industry_template_",
                        column: x => x.industry_template_id,
                        principalTable: "industry_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    industry_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    route_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    product_family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_routes", x => x.id);
                    table.ForeignKey(
                        name: "fk_routes_industry_templates_industry_template_id",
                        column: x => x.industry_template_id,
                        principalTable: "industry_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    expected_material_unit_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_route_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_steps_operation_definitions_operation_definition_id",
                        column: x => x.operation_definition_id,
                        principalTable: "operation_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_route_steps_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_import_batch_code",
                table: "import_batches",
                column: "import_batch_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_source_system_definition_id",
                table: "import_batches",
                column: "source_system_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_started_at_utc",
                table: "import_batches",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_status",
                table: "import_batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_industry_templates_industry_name",
                table: "industry_templates",
                column: "industry_name");

            migrationBuilder.CreateIndex(
                name: "ix_industry_templates_is_active",
                table: "industry_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_industry_templates_template_code",
                table: "industry_templates",
                column: "template_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_definitions_is_active",
                table: "mapping_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_definitions_mapping_code",
                table: "mapping_definitions",
                column: "mapping_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_definitions_source_system_definition_id",
                table: "mapping_definitions",
                column: "source_system_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_definitions_source_system_definition_id_source_obje",
                table: "mapping_definitions",
                columns: new[] { "source_system_definition_id", "source_object_name", "target_entity_name", "mapping_version" });

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_type_definitions_industry_template_id",
                table: "material_unit_type_definitions",
                column: "industry_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_type_definitions_industry_template_id_materia",
                table: "material_unit_type_definitions",
                columns: new[] { "industry_template_id", "material_unit_type_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_type_definitions_is_active",
                table: "material_unit_type_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_industry_template_id",
                table: "operation_definitions",
                column: "industry_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_industry_template_id_operation_code",
                table: "operation_definitions",
                columns: new[] { "industry_template_id", "operation_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_is_active",
                table: "operation_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_operation_category",
                table: "operation_definitions",
                column: "operation_category");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_expected_material_unit_type",
                table: "route_steps",
                column: "expected_material_unit_type");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_operation_definition_id",
                table: "route_steps",
                column: "operation_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id",
                table: "route_steps",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id_sequence_no",
                table: "route_steps",
                columns: new[] { "route_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_routes_industry_template_id",
                table: "routes",
                column: "industry_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_routes_industry_template_id_route_code",
                table: "routes",
                columns: new[] { "industry_template_id", "route_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_routes_is_active",
                table: "routes",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_routes_product_family",
                table: "routes",
                column: "product_family");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropTable(
                name: "mapping_definitions");

            migrationBuilder.DropTable(
                name: "material_unit_type_definitions");

            migrationBuilder.DropTable(
                name: "route_steps");

            migrationBuilder.DropTable(
                name: "operation_definitions");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "industry_templates");
        }
    }
}
