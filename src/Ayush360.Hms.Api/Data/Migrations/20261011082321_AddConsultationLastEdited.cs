using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ayush360.Hms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationLastEdited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_edited_at",
                schema: "doctor",
                table: "encounters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_edited_by",
                schema: "doctor",
                table: "encounters",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_edited_at",
                schema: "doctor",
                table: "encounters");

            migrationBuilder.DropColumn(
                name: "last_edited_by",
                schema: "doctor",
                table: "encounters");
        }
    }
}
