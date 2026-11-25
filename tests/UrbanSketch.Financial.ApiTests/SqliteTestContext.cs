using Microsoft.EntityFrameworkCore;
using UrbanSketch.Financial.Api.Data;
using UrbanSketch.Financial.Api.Models;

internal class SqliteTestContext(DbContextOptions<FinancialDbContext> options) : FinancialDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // PostgreSQL numeric precision is verified separately against its migration.
        // TEXT prevents SQLite numeric affinity from losing exact decimal digits.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(decimal))
                    property.SetColumnType("TEXT");
        modelBuilder.Entity<CashFlow>().Property(x => x.CostShare).HasColumnType("NUMERIC");
        modelBuilder.Entity<CashFlow>().Property(x => x.RevenueShare).HasColumnType("NUMERIC");
        modelBuilder.Entity<Project>().Property(x => x.CreatedAt)
            .HasConversion(v => v.ToString("O"), v => DateTime.Parse(v, null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}
