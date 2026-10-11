using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ayush360.Hms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientAadhaar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aadhaar_encrypted",
                schema: "registration",
                table: "patients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aadhaar_hash",
                schema: "registration",
                table: "patients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aadhaar_last4",
                schema: "registration",
                table: "patients",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_patients_tenant_id_aadhaar_hash",
                schema: "registration",
                table: "patients",
                columns: new[] { "tenant_id", "aadhaar_hash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_patients_tenant_id_aadhaar_hash",
                schema: "registration",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "aadhaar_encrypted",
                schema: "registration",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "aadhaar_hash",
                schema: "registration",
                table: "patients");

            migrationBuilder.DropColumn(
                name: "aadhaar_last4",
                schema: "registration",
                table: "patients");
        }
    }
}
