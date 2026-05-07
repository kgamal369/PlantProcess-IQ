using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantLayoutAndIntegrationMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "line_code",
                table: "process_step_executions");

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    site_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_sites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_system_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_system_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_system_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_read_only_source = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_source_system_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_areas", x => x.id);
                    table.ForeignKey(
                        name: "fk_areas_areas_parent_area_id",
                        column: x => x.parent_area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_areas_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    equipment_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    equipment_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    equipment_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_equipment", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_equipment_equipment_parent_equipment_id",
                        column: x => x.parent_equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipment_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_areas_area_type",
                table: "areas",
                column: "area_type");

            migrationBuilder.CreateIndex(
                name: "ix_areas_parent_area_id",
                table: "areas",
                column: "parent_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_areas_site_id",
                table: "areas",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_areas_site_id_area_code",
                table: "areas",
                columns: new[] { "site_id", "area_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_area_id",
                table: "equipment",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_equipment_type",
                table: "equipment",
                column: "equipment_type");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_is_active",
                table: "equipment",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_parent_equipment_id",
                table: "equipment",
                column: "parent_equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_site_id",
                table: "equipment",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_site_id_equipment_code",
                table: "equipment",
                columns: new[] { "site_id", "equipment_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sites_country_code",
                table: "sites",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_sites_site_code",
                table: "sites",
                column: "site_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_system_definitions_source_system_code",
                table: "source_system_definitions",
                column: "source_system_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_system_definitions_source_system_type",
                table: "source_system_definitions",
                column: "source_system_type");

            migrationBuilder.AddForeignKey(
                name: "fk_material_units_sites_site_id",
                table: "material_units",
                column: "site_id",
                principalTable: "sites",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_material_units_sites_site_id",
                table: "material_units");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "source_system_definitions");

            migrationBuilder.DropTable(
                name: "areas");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.AddColumn<string>(
                name: "line_code",
                table: "process_step_executions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
