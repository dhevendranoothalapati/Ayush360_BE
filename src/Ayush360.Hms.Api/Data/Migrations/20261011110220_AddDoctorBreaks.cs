using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ayush360.Hms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorBreaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "last_edited_by",
                schema: "doctor",
                table: "encounters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "doctor_breaks",
                schema: "doctor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expected_back_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_breaks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_breaks_tenant_id_doctor_id_started_at",
                schema: "doctor",
                table: "doctor_breaks",
                columns: new[] { "tenant_id", "doctor_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_breaks",
                schema: "doctor");

            migrationBuilder.AlterColumn<string>(
                name: "last_edited_by",
                schema: "doctor",
                table: "encounters",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
