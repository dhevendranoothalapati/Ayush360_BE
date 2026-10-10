using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ayush360.Hms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialHospital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hospital");

            migrationBuilder.EnsureSchema(
                name: "registration");

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "hospital",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "number_sequences",
                schema: "hospital",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    sequence_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    last_value = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_sequences", x => new { x.tenant_id, x.sequence_key, x.period });
                });

            migrationBuilder.CreateTable(
                name: "patients",
                schema: "registration",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    patient_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    dob_estimated = table.Column<bool>(type: "boolean", nullable: false),
                    blood_group = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    mobile_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    emergency_contact_phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    last_visit_date = table.Column<DateOnly>(type: "date", nullable: true),
                    additional_details = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "hospital",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    follow_up_free_days = table.Column<int>(type: "integer", nullable: false),
                    receipt_footer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.id);
                    table.CheckConstraint("ck_settings_follow_up_free_days", "follow_up_free_days BETWEEN 0 AND 90");
                });

            migrationBuilder.CreateTable(
                name: "doctors",
                schema: "hospital",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    department_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    qualification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    specialization = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    medical_registration_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    medical_council = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    consultation_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctors", x => x.id);
                    table.ForeignKey(
                        name: "fk_doctors_departments_department_id",
                        column: x => x.department_id,
                        principalSchema: "hospital",
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visits",
                schema: "registration",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    patient_id = table.Column<long>(type: "bigint", nullable: false),
                    visit_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    token_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    visit_date = table.Column<DateOnly>(type: "date", nullable: false),
                    registration_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    department_id = table.Column<long>(type: "bigint", nullable: false),
                    doctor_id = table.Column<long>(type: "bigint", nullable: false),
                    reason_for_visit = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    message_consent = table.Column<bool>(type: "boolean", nullable: false),
                    visit_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    queue_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    consultation_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_follow_up = table.Column<bool>(type: "boolean", nullable: false),
                    follow_up_of_visit_id = table.Column<long>(type: "bigint", nullable: true),
                    payer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    triage_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    arrival_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    icu_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    bed_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    additional_details = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visits", x => x.id);
                    table.CheckConstraint("ck_visits_consultation_fee", "consultation_fee >= 0");
                    table.CheckConstraint("ck_visits_deposit_amount", "deposit_amount >= 0");
                    table.ForeignKey(
                        name: "fk_visits_departments_department_id",
                        column: x => x.department_id,
                        principalSchema: "hospital",
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_visits_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalSchema: "hospital",
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_visits_patients_patient_id",
                        column: x => x.patient_id,
                        principalSchema: "registration",
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_visits_visits_follow_up_of_visit_id",
                        column: x => x.follow_up_of_visit_id,
                        principalSchema: "registration",
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mlc_register",
                schema: "registration",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    mlc_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    police_station = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    fir_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mlc_register", x => x.id);
                    table.ForeignKey(
                        name: "fk_mlc_register_visits_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "registration",
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                schema: "registration",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    receipt_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    collected_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    collected_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
                    table.CheckConstraint("ck_receipts_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_receipts_visits_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "registration",
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_departments_tenant_id_code",
                schema: "hospital",
                table: "departments",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doctors_department_id",
                schema: "hospital",
                table: "doctors",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_doctors_tenant_id_department_id",
                schema: "hospital",
                table: "doctors",
                columns: new[] { "tenant_id", "department_id" });

            migrationBuilder.CreateIndex(
                name: "ix_doctors_tenant_id_medical_registration_number",
                schema: "hospital",
                table: "doctors",
                columns: new[] { "tenant_id", "medical_registration_number" },
                unique: true,
                filter: "medical_registration_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_doctors_user_id",
                schema: "hospital",
                table: "doctors",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mlc_register_tenant_id_mlc_number",
                schema: "registration",
                table: "mlc_register",
                columns: new[] { "tenant_id", "mlc_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mlc_register_visit_id",
                schema: "registration",
                table: "mlc_register",
                column: "visit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_patients_tenant_id_first_name_last_name",
                schema: "registration",
                table: "patients",
                columns: new[] { "tenant_id", "first_name", "last_name" });

            migrationBuilder.CreateIndex(
                name: "ix_patients_tenant_id_mobile_number",
                schema: "registration",
                table: "patients",
                columns: new[] { "tenant_id", "mobile_number" });

            migrationBuilder.CreateIndex(
                name: "ix_patients_tenant_id_patient_number",
                schema: "registration",
                table: "patients",
                columns: new[] { "tenant_id", "patient_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipts_tenant_id_collected_on_collected_by_user_id",
                schema: "registration",
                table: "receipts",
                columns: new[] { "tenant_id", "collected_on", "collected_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_receipts_tenant_id_receipt_number",
                schema: "registration",
                table: "receipts",
                columns: new[] { "tenant_id", "receipt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipts_visit_id",
                schema: "registration",
                table: "receipts",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_settings_tenant_id",
                schema: "hospital",
                table: "settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visits_department_id",
                schema: "registration",
                table: "visits",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_visits_doctor_id",
                schema: "registration",
                table: "visits",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_visits_follow_up_of_visit_id",
                schema: "registration",
                table: "visits",
                column: "follow_up_of_visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_visits_patient_id",
                schema: "registration",
                table: "visits",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_visits_tenant_id_patient_id_visit_date",
                schema: "registration",
                table: "visits",
                columns: new[] { "tenant_id", "patient_id", "visit_date" });

            migrationBuilder.CreateIndex(
                name: "ix_visits_tenant_id_visit_date_doctor_id",
                schema: "registration",
                table: "visits",
                columns: new[] { "tenant_id", "visit_date", "doctor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_visits_tenant_id_visit_date_token_number",
                schema: "registration",
                table: "visits",
                columns: new[] { "tenant_id", "visit_date", "token_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visits_tenant_id_visit_number",
                schema: "registration",
                table: "visits",
                columns: new[] { "tenant_id", "visit_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mlc_register",
                schema: "registration");

            migrationBuilder.DropTable(
                name: "number_sequences",
                schema: "hospital");

            migrationBuilder.DropTable(
                name: "receipts",
                schema: "registration");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "hospital");

            migrationBuilder.DropTable(
                name: "visits",
                schema: "registration");

            migrationBuilder.DropTable(
                name: "doctors",
                schema: "hospital");

            migrationBuilder.DropTable(
                name: "patients",
                schema: "registration");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "hospital");
        }
    }
}
