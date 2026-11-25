using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanSketch.Financial.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialModelConstructionAreaOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "construction_commercial_area_override",
                table: "financial_models",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "construction_residential_area_override",
                table: "financial_models",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "construction_commercial_area_override",
                table: "financial_models");

            migrationBuilder.DropColumn(
                name: "construction_residential_area_override",
                table: "financial_models");
        }
    }
}
