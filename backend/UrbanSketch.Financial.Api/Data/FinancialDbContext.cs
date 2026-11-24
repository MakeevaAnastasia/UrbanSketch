using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Models;

namespace UrbanSketch.Financial.Api.Data;

public class FinancialDbContext(DbContextOptions<FinancialDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ConceptVariant> ConceptVariants => Set<ConceptVariant>();
    public DbSet<FinancialModel> FinancialModels => Set<FinancialModel>();
    public DbSet<CashFlow> CashFlows => Set<CashFlow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var project = modelBuilder.Entity<Project>();
        project.ToTable("projects");
        project.HasKey(x => x.Id);
        project.Property(x => x.Id).HasColumnName("id");
        project.Property(x => x.Name).HasColumnName("name").IsRequired();
        project.Property(x => x.CreatedAt).HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");

        var variant = modelBuilder.Entity<ConceptVariant>();
        variant.ToTable("concept_variants");
        variant.HasKey(x => x.Id);
        variant.Property(x => x.Id).HasColumnName("id");
        variant.Property(x => x.ProjectId).HasColumnName("project_id");
        variant.Property(x => x.Name).HasColumnName("name").IsRequired();
        variant.Property(x => x.ResidentialArea).HasColumnName("residential_sale_area_m2").HasPrecision(18, 2);
        variant.Property(x => x.CommercialArea).HasColumnName("commercial_sale_area_m2").HasPrecision(18, 2);
        variant.Property(x => x.ConstructionResidentialArea).HasColumnName("residential_construction_area_m2").HasPrecision(18, 2);
        variant.Property(x => x.ConstructionCommercialArea).HasColumnName("commercial_construction_area_m2").HasPrecision(18, 2);
        variant.Property(x => x.ParkingSpaces).HasColumnName("parking_spaces");
        variant.Property(x => x.ResidentsCount).HasColumnName("population");
        variant.Property(x => x.BuildingsCount).HasColumnName("buildings_count");
        variant.Property(x => x.MaxFloors).HasColumnName("max_storeys");
        variant.Property(x => x.SocialAreas).HasColumnName("social_area_m2").HasPrecision(18, 2);
        variant.Property(x => x.PreschoolPlaces).HasColumnName("kindergarten_places");
        variant.Property(x => x.SchoolPlaces).HasColumnName("school_places");
        variant.HasOne(x => x.Project).WithMany(x => x.Variants)
            .HasForeignKey(x => x.ProjectId).IsRequired().OnDelete(DeleteBehavior.Cascade);

        var financialModel = modelBuilder.Entity<FinancialModel>();
        financialModel.ToTable("financial_models");
        financialModel.HasKey(x => x.Id);
        financialModel.Property(x => x.Id).HasColumnName("id");
        financialModel.Property(x => x.ConceptVariantId).HasColumnName("variant_id");
        financialModel.Property(x => x.ResidentialConstructionCost).HasColumnName("residential_construction_cost_per_m2").HasPrecision(20, 2);
        financialModel.Property(x => x.CommercialConstructionCost).HasColumnName("commercial_construction_cost_per_m2").HasPrecision(20, 2);
        financialModel.Property(x => x.ParkingCost).HasColumnName("parking_construction_cost_each").HasPrecision(20, 2);
        financialModel.Property(x => x.ResidentialSalePrice).HasColumnName("residential_sale_price_per_m2").HasPrecision(20, 2);
        financialModel.Property(x => x.CommercialSalePrice).HasColumnName("commercial_sale_price_per_m2").HasPrecision(20, 2);
        financialModel.Property(x => x.ParkingSalePrice).HasColumnName("parking_sale_price_each").HasPrecision(20, 2);
        financialModel.Property(x => x.LandCost).HasColumnName("land_cost").HasPrecision(20, 2);
        financialModel.Property(x => x.AdditionalCosts).HasColumnName("additional_costs").HasPrecision(20, 2);
        financialModel.Property(x => x.ProjectDuration).HasColumnName("duration_years");
        financialModel.Property(x => x.TotalCosts).HasColumnName("total_costs").HasPrecision(20, 2);
        financialModel.Property(x => x.TotalRevenue).HasColumnName("total_revenue").HasPrecision(20, 2);
        financialModel.Property(x => x.Profit).HasColumnName("profit").HasPrecision(20, 2);
        financialModel.Property(x => x.Roi).HasColumnName("roi_percent").HasPrecision(12, 4);
        financialModel.Property(x => x.SalesProfitability).HasColumnName("sales_margin_percent").HasPrecision(12, 4);
        financialModel.HasOne(x => x.ConceptVariant).WithMany(x => x.FinancialModels)
            .HasForeignKey(x => x.ConceptVariantId).IsRequired().OnDelete(DeleteBehavior.Cascade);

        var cashFlow = modelBuilder.Entity<CashFlow>();
        cashFlow.ToTable("cash_flows", table =>
        {
            table.HasCheckConstraint("ck_cash_flows_period_number", "period_number > 0");
            table.HasCheckConstraint("ck_cash_flows_cost_share", "expense_percent BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_cash_flows_revenue_share", "revenue_percent BETWEEN 0 AND 100");
        });
        cashFlow.HasKey(x => x.Id);
        cashFlow.Property(x => x.Id).HasColumnName("id");
        cashFlow.Property(x => x.FinancialModelId).HasColumnName("financial_model_id");
        cashFlow.Property(x => x.PeriodNumber).HasColumnName("period_number");
        cashFlow.Property(x => x.CostShare).HasColumnName("expense_percent").HasPrecision(7, 4);
        cashFlow.Property(x => x.RevenueShare).HasColumnName("revenue_percent").HasPrecision(7, 4);
        cashFlow.Property(x => x.PeriodCosts).HasColumnName("costs").HasPrecision(20, 2);
        cashFlow.Property(x => x.PeriodRevenue).HasColumnName("revenue").HasPrecision(20, 2);
        cashFlow.Property(x => x.NetCashFlow).HasColumnName("net_flow").HasPrecision(20, 2);
        cashFlow.Property(x => x.CumulativeCashFlow).HasColumnName("cumulative_flow").HasPrecision(20, 2);
        cashFlow.HasOne(x => x.FinancialModel).WithMany(x => x.CashFlows)
            .HasForeignKey(x => x.FinancialModelId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        cashFlow.HasIndex(x => new { x.FinancialModelId, x.PeriodNumber })
            .IsUnique().HasDatabaseName("ux_cash_flows_financial_model_id_period_number");

        // Nullable value types remain optional; no database defaults replace unknown values.
        // Identifier and CreatedAt initializers are supplied by the entity classes.
    }
}
