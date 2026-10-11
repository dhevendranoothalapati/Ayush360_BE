using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ayush360.Hms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "doctor");

            migrationBuilder.CreateTable(
                name: "encounters",
                schema: "doctor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    chief_complaints = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    history = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    past_history = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    allergies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    examination = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    advice = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    follow_up_date = table.Column<DateOnly>(type: "date", nullable: true),
                    vitals = table.Column<string>(type: "jsonb", nullable: false),
                    admission_requested = table.Column<bool>(type: "boolean", nullable: false),
                    admission_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    admission_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounters", x => x.id);
                    table.ForeignKey(
                        name: "fk_encounters_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalSchema: "hospital",
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_encounters_visits_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "registration",
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medicines",
                schema: "hospital",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    generic_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    strength = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    form = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    default_dose = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    default_frequency = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medicines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "encounter_diagnoses",
                schema: "doctor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    encounter_id = table.Column<long>(type: "bigint", nullable: false),
                    icd10_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounter_diagnoses", x => x.id);
                    table.ForeignKey(
                        name: "fk_encounter_diagnoses_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalSchema: "doctor",
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lab_orders",
                schema: "doctor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    encounter_id = table.Column<long>(type: "bigint", nullable: false),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    test_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    test_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lab_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_lab_orders_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalSchema: "doctor",
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prescription_items",
                schema: "doctor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    encounter_id = table.Column<long>(type: "bigint", nullable: false),
                    medicine_id = table.Column<long>(type: "bigint", nullable: true),
                    medicine_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    strength = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    form = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    dose = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    frequency = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    timing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    duration_days = table.Column<int>(type: "integer", nullable: true),
                    instructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescription_items", x => x.id);
                    table.CheckConstraint("ck_prescription_items_duration_days", "duration_days IS NULL OR duration_days BETWEEN 1 AND 365");
                    table.ForeignKey(
                        name: "fk_prescription_items_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalSchema: "doctor",
                        principalTable: "encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_encounter_diagnoses_encounter_id",
                schema: "doctor",
                table: "encounter_diagnoses",
                column: "encounter_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounter_diagnoses_tenant_id_icd10_code",
                schema: "doctor",
                table: "encounter_diagnoses",
                columns: new[] { "tenant_id", "icd10_code" });

            migrationBuilder.CreateIndex(
                name: "ix_encounters_doctor_id",
                schema: "doctor",
                table: "encounters",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounters_tenant_id_doctor_id_started_at",
                schema: "doctor",
                table: "encounters",
                columns: new[] { "tenant_id", "doctor_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_encounters_tenant_id_patient_id_started_at",
                schema: "doctor",
                table: "encounters",
                columns: new[] { "tenant_id", "patient_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_encounters_visit_id",
                schema: "doctor",
                table: "encounters",
                column: "visit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lab_orders_encounter_id",
                schema: "doctor",
                table: "lab_orders",
                column: "encounter_id");

            migrationBuilder.CreateIndex(
                name: "ix_lab_orders_tenant_id_status_created_at",
                schema: "doctor",
                table: "lab_orders",
                columns: new[] { "tenant_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lab_orders_tenant_id_visit_id",
                schema: "doctor",
                table: "lab_orders",
                columns: new[] { "tenant_id", "visit_id" });

            migrationBuilder.CreateIndex(
                name: "ix_medicines_tenant_id_name_strength_form",
                schema: "hospital",
                table: "medicines",
                columns: new[] { "tenant_id", "name", "strength", "form" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prescription_items_encounter_id",
                schema: "doctor",
                table: "prescription_items",
                column: "encounter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encounter_diagnoses",
                schema: "doctor");

            migrationBuilder.DropTable(
                name: "lab_orders",
                schema: "doctor");

            migrationBuilder.DropTable(
                name: "medicines",
                schema: "hospital");

            migrationBuilder.DropTable(
                name: "prescription_items",
                schema: "doctor");

            migrationBuilder.DropTable(
                name: "encounters",
                schema: "doctor");
        }
    }
}
