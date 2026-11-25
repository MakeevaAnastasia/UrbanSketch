using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanSketch.Financial.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedFinancialCalculations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "calculation_input_hash",
                table: "financial_models",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calculation_snapshot",
                table: "financial_models",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calculation_input_hash",
                table: "financial_models");

            migrationBuilder.DropColumn(
                name: "calculation_snapshot",
                table: "financial_models");
        }
    }
}
