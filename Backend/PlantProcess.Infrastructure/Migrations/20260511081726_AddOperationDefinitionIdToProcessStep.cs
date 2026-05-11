using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationDefinitionIdToProcessStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "source_system_type",
                table: "source_system_definitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "source_system_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_source_system_definitions_is_active",
                table: "source_system_definitions",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_source_system_definitions_is_active",
                table: "source_system_definitions");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "source_system_definitions");

            migrationBuilder.AlterColumn<string>(
                name: "source_system_type",
                table: "source_system_definitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
