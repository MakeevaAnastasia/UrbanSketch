using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanSketch.Financial.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "concept_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    residential_sale_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commercial_sale_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    residential_construction_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    commercial_construction_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    parking_spaces = table.Column<int>(type: "integer", nullable: false),
                    population = table.Column<int>(type: "integer", nullable: true),
                    buildings_count = table.Column<int>(type: "integer", nullable: true),
                    max_storeys = table.Column<int>(type: "integer", nullable: true),
                    social_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    kindergarten_places = table.Column<int>(type: "integer", nullable: true),
                    school_places = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_variants", x => x.id);
                    table.ForeignKey(
                        name: "FK_concept_variants_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    residential_construction_cost_per_m2 = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    commercial_construction_cost_per_m2 = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    parking_construction_cost_each = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    residential_sale_price_per_m2 = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    commercial_sale_price_per_m2 = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    parking_sale_price_each = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    land_cost = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    additional_costs = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    duration_years = table.Column<int>(type: "integer", nullable: false),
                    total_costs = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    total_revenue = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    profit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    roi_percent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    sales_margin_percent = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_financial_models_concept_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "concept_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cash_flows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    expense_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    revenue_percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    costs = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    revenue = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    net_flow = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true),
                    cumulative_flow = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_flows", x => x.id);
                    table.CheckConstraint("ck_cash_flows_cost_share", "expense_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_cash_flows_period_number", "period_number > 0");
                    table.CheckConstraint("ck_cash_flows_revenue_share", "revenue_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_cash_flows_financial_models_financial_model_id",
                        column: x => x.financial_model_id,
                        principalTable: "financial_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_cash_flows_financial_model_id_period_number",
                table: "cash_flows",
                columns: new[] { "financial_model_id", "period_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_concept_variants_project_id",
                table: "concept_variants",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_financial_models_variant_id",
                table: "financial_models",
                column: "variant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_flows");

            migrationBuilder.DropTable(
                name: "financial_models");

            migrationBuilder.DropTable(
                name: "concept_variants");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
