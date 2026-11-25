using UrbanSketch.Financial.Api.Calculations;
using UrbanSketch.Financial.Api.Models;

var service = new FinancialCalculationService();
var total = 0;
var failures = 0;
void Test(string name, Action run)
{
    total++;
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static (FinancialModel Model, ConceptVariant Concept) Fixture()
{
    var concept = new ConceptVariant { ResidentialArea = 1000m, CommercialArea = 200m, ParkingSpaces = 20,
        ConstructionResidentialArea = 1000m, ConstructionCommercialArea = 200m };
    return (new FinancialModel { ConceptVariantId = concept.Id, ResidentialConstructionCost = 100000m,
        CommercialConstructionCost = 80000m, ParkingCost = 500000m, ResidentialSalePrice = 200000m,
        CommercialSalePrice = 150000m, ParkingSalePrice = 1000000m, LandCost = 14000000m,
        AdditionalCosts = 10000000m, ProjectDuration = 3 }, concept);
}
Test("TZ section 9 exact cost/revenue components and all indicators", () =>
{
    var (m,c) = Fixture(); var r = service.Calculate(m,c);
    Check(r.Status == CalculationStatus.Complete && r.CanPersistToCurrentSchema);
    Check(r.Costs.Residential == 100000000m && r.Costs.Commercial == 16000000m && r.Costs.Parking == 10000000m);
    Check(r.Costs.Land == 14000000m && r.Costs.Additional == 10000000m && r.TotalCosts == 150000000m);
    Check(r.Revenue.Residential == 200000000m && r.Revenue.Commercial == 30000000m && r.Revenue.Parking == 20000000m);
    Check(r.TotalRevenue == 250000000m && r.Profit == 100000000m && r.Roi == 66.6667m && r.SalesProfitability == 40m);
    Check(decimal.Round(r.Roi!.Value,2,MidpointRounding.AwayFromZero) == 66.67m);
});
Test("loss is valid", () => { var (m,c)=Fixture(); m.LandCost=214000000m; var r=service.Calculate(m,c); Check(r.Profit == -100000000m && r.Roi < 0 && r.SalesProfitability == -40m && r.Status==CalculationStatus.Complete); });
Test("break even", () => { var (m,c)=Fixture(); m.LandCost=114000000m; var r=service.Calculate(m,c); Check(r.Profit==0 && r.Roi==0 && r.SalesProfitability==0); });
Test("zero costs yields null ROI", () => { var (m,c)=Fixture(); m.ResidentialConstructionCost=m.CommercialConstructionCost=m.ParkingCost=m.LandCost=m.AdditionalCosts=0; var r=service.Calculate(m,c); Check(r.TotalCosts==0 && r.Roi==null && r.SalesProfitability==100 && r.Status==CalculationStatus.Complete); });
Test("zero revenue yields null margin", () => { var (m,c)=Fixture(); m.ResidentialSalePrice=m.CommercialSalePrice=m.ParkingSalePrice=0; var r=service.Calculate(m,c); Check(r.TotalRevenue==0 && r.Profit==-150000000m && r.Roi==-100 && r.SalesProfitability==null); });
Test("all zero returns two undefined ratios without division", () => { var (m,c)=Fixture(); c.ResidentialArea=c.CommercialArea=0; c.ParkingSpaces=0; m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0; m.LandCost=m.AdditionalCosts=0; var r=service.Calculate(m,c); Check(r.TotalCosts==0 && r.TotalRevenue==0 && r.Profit==0 && r.Roi==null && r.SalesProfitability==null && r.CanPersistToCurrentSchema); });
Test("unknown areas retain valid revenue and block aggregate costs/profit/ratios", () => { var (m,c)=Fixture(); c.ConstructionResidentialArea=c.ConstructionCommercialArea=null; var r=service.Calculate(m,c); Check(r.Status==CalculationStatus.Incomplete && r.MissingInputs.Count==2 && r.TotalCosts==null && r.Profit==null && r.Roi==null && r.SalesProfitability==null && r.TotalRevenue==250000000m && r.Costs.Parking==10000000m && !r.CanPersistToCurrentSchema); });
Test("one unknown area preserves other category", () => { var (m,c)=Fixture(); c.ConstructionResidentialArea=null; var r=service.Calculate(m,c); Check(r.Costs.Residential==null && r.Costs.Commercial==16000000m && r.MissingInputs.SequenceEqual(new[]{"constructionResidentialArea"}) && r.TotalCosts==null); });
Test("unknown times zero is still unknown", () => { var (m,c)=Fixture(); c.ConstructionResidentialArea=null; m.ResidentialConstructionCost=0; Check(service.Calculate(m,c).Costs.Residential==null); });
Test("override has priority without changing saleable revenue", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=1100; var r=service.Calculate(m,c); Check(r.TotalCosts==160000000m && r.TotalRevenue==250000000m && c.ConstructionResidentialArea==1000); });
Test("source construction fallback", () => { var (m,c)=Fixture(); c.ConstructionCommercialArea=300; Check(service.Calculate(m,c).Costs.Commercial==24000000m); });
Test("explicit override zero beats nonzero source", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=0; Check(service.Calculate(m,c).Costs.Residential==0); });
Test("models independent and previous result immutable", () => { var (m,c)=Fixture(); var first=service.Calculate(m,c); var (other,_)=Fixture(); other.ConceptVariantId=c.Id; other.ConstructionResidentialAreaOverride=1200; Check(service.Calculate(other,c).TotalCosts==170000000m && first.TotalCosts==150000000m && m.ConstructionResidentialAreaOverride==null && m.TotalCosts==null); });
foreach (var field in new[]{"ResidentialSalePrice","ResidentialConstructionCost","LandCost","AdditionalCosts"})
    Test("recalculation after changing " + field, () => { var (m,c)=Fixture(); var before=service.Calculate(m,c); var property=typeof(FinancialModel).GetProperty(field)!; property.SetValue(m,(decimal)property.GetValue(m)!+1000m); var after=service.Calculate(m,c); Check(field=="ResidentialSalePrice" ? after.TotalRevenue==before.TotalRevenue+1000000m && after.TotalCosts==before.TotalCosts : field=="ResidentialConstructionCost" ? after.TotalCosts==before.TotalCosts+1000000m : after.TotalCosts==before.TotalCosts+1000m); });
foreach (var field in new[]{"ResidentialConstructionCost","CommercialConstructionCost","ParkingCost","ResidentialSalePrice","CommercialSalePrice","ParkingSalePrice","LandCost","AdditionalCosts"})
    Test("negative input " + field, () => { var (m,c)=Fixture(); typeof(FinancialModel).GetProperty(field)!.SetValue(m,-1m); var r=service.Calculate(m,c); Check(r.Status==CalculationStatus.Failed && r.Errors.Count>0 && !r.CanPersistToCurrentSchema); });
Test("negative area invalid", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=-1; Check(service.Calculate(m,c).Status==CalculationStatus.Failed); });
Test("negative sale area invalid", () => { var (m,c)=Fixture(); c.ResidentialArea=-1; Check(service.Calculate(m,c).TotalRevenue==null); });
Test("negative parking invalidates both parking components", () => { var (m,c)=Fixture(); c.ParkingSpaces=-1; var r=service.Calculate(m,c); Check(r.Costs.Parking==null && r.Revenue.Parking==null && r.Status==CalculationStatus.Failed); });
Test("input excess fractional precision rejected", () => { var (m,c)=Fixture(); m.ResidentialConstructionCost=0.001m; Check(service.Calculate(m,c).Status==CalculationStatus.Failed); });
Test("area input outside numeric18 rejected", () => { var (m,c)=Fixture(); c.ResidentialArea=10000000000000000m; Check(service.Calculate(m,c).Status==CalculationStatus.Failed); });
Test("money input outside numeric20 rejected safely", () => { var (m,c)=Fixture(); m.LandCost=decimal.MaxValue; Check(service.Calculate(m,c).Status==CalculationStatus.Failed); });
Test("valid inputs whose product exceeds decimal produce structured error", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=9999999999999999.99m; m.ResidentialConstructionCost=999999999999999999.99m; var r=service.Calculate(m,c); Check(r.Status==CalculationStatus.Failed && r.Costs.Residential==null && r.TotalCosts==null && r.TotalRevenue==250000000m && r.Errors.Any(e=>e.Code=="decimalOverflow")); });
Test("decimal multiplication may not silently drop fractional digits", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=9999999999999999.99m; m.ResidentialConstructionCost=1000000000000.01m; var r=service.Calculate(m,c); Check(r.Status==CalculationStatus.Failed && r.Costs.Residential==null && r.Errors.Any(e=>e.Code=="decimalOverflow")); });
Test("subcent product preserved exactly and storage blocked", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=0.01m; m.ResidentialConstructionCost=0.01m; var r=service.Calculate(m,c); Check(r.Costs.Residential==0.0001m && r.TotalCosts==50000000.0001m && !r.CanPersistToCurrentSchema && r.PersistenceIssues.Any(e=>e.Code=="storageScale")); });
Test("money result numeric20 exact boundary", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0; c.ResidentialArea=c.CommercialArea=0; c.ParkingSpaces=0; m.LandCost=999999999999999999.99m; m.AdditionalCosts=0; var r=service.Calculate(m,c); Check(r.TotalCosts==999999999999999999.99m && r.CanPersistToCurrentSchema); });
Test("sum above storage maximum is exact but cannot persist", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0; c.ParkingSpaces=0; m.LandCost=999999999999999999.99m; m.AdditionalCosts=0.01m; var r=service.Calculate(m,c); Check(r.TotalCosts==1000000000000000000m && !r.CanPersistToCurrentSchema && r.PersistenceIssues.Any(e=>e.Code=="storageRange")); });
Test("percent exceeding numeric12 remains explicit storage issue", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0; c.ParkingSpaces=0; m.LandCost=0.01m; m.AdditionalCosts=0; var r=service.Calculate(m,c); Check(r.Roi>99999999.9999m && !r.CanPersistToCurrentSchema && r.PersistenceIssues.Any(e=>e.Field=="roi")); });
Test("percent exact midpoint rounds away from zero", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0; c.ParkingSpaces=0; c.ResidentialArea=1; c.CommercialArea=0; m.LandCost=2000000m; m.AdditionalCosts=0; m.ResidentialSalePrice=2000001m; Check(service.Calculate(m,c).Roi==0.0001m); m.ResidentialSalePrice=1999999m; Check(service.Calculate(m,c).Roi==-0.0001m); });
Test("information-only missing TEP and duration do not invalidate math", () => { var (m,c)=Fixture(); m.ProjectDuration=0; c.ResidentsCount=null; Check(service.Calculate(m,c).Status==CalculationStatus.Complete); });
Test("sum of exact decimal components overflowing decimal is controlled", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=400000000000m; m.ResidentialConstructionCost=m.CommercialConstructionCost=100000000000000000m; var r=service.Calculate(m,c); Check(r.Costs.Residential==40000000000000000000000000000m && r.Costs.Commercial==40000000000000000000000000000m && r.TotalCosts==null && r.Errors.Any(e=>e.Field=="totalCosts" && e.Code=="decimalOverflow")); });
Test("percentage decimal overflow does not invalidate exact revenue", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0; c.ParkingSpaces=0; c.ResidentialArea=1000000000m; c.CommercialArea=0; m.ResidentialSalePrice=100000000000000000m; m.LandCost=0.01m; m.AdditionalCosts=0; var r=service.Calculate(m,c); Check(r.Status==CalculationStatus.Failed && r.TotalRevenue==100000000000000000000000000m && r.Roi==null && r.Errors.Any(e=>e.Field=="roi" && e.Code=="decimalOverflow")); });
Test("percentage numeric12 maximum is allowed exactly", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0; c.ParkingSpaces=0; c.ResidentialArea=1; c.CommercialArea=0; m.LandCost=1000000m; m.AdditionalCosts=0; m.ResidentialSalePrice=1000000999999m; var r=service.Calculate(m,c); Check(r.Roi==99999999.9999m && !r.PersistenceIssues.Any(e=>e.Field=="roi") && r.CanPersistToCurrentSchema); });
Test("rounding happens after summation, never per component", () => { var (m,c)=Fixture(); m.ConstructionResidentialAreaOverride=m.ConstructionCommercialAreaOverride=0.01m; m.ResidentialConstructionCost=0.25m; m.CommercialConstructionCost=0.75m; c.ParkingSpaces=0; m.LandCost=m.AdditionalCosts=0; var r=service.Calculate(m,c); Check(r.Costs.Residential==0.0025m && r.Costs.Commercial==0.0075m && r.TotalCosts==0.01m && !r.PersistenceIssues.Any(e=>e.Field=="totalCosts")); });
Test("service is reentrant across concurrent independent calculations", () => { Parallel.For(0,100, i=>{ var (m,c)=Fixture(); m.LandCost+=i; Check(service.Calculate(m,c).TotalCosts==150000000m+i); }); });
Console.WriteLine($"Calculation tests: {total}; failures: {failures}");
return failures==0 ? 0 : 1;
