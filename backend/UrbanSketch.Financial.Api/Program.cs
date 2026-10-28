var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Спринт 1: только проверка работы каркаса. API предметной области появится в спринте 2.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    component = "UrbanSketch Financial API",
    sprint = 1
}));

app.Run();
