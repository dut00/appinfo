using Dut00.AppInfo;
using Dut00.AppInfo.Configuration;
using Dut00.AppInfo.ConnectionStrings;
using Dut00.AppInfo.Environment;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppInfo()
    .WithConfigurationDetails()
    .WithEnvironmentDetails()
    .WithConnectionStrings()
    .WithOwner(o => o.Owner = "red_team@company.com")
    .WithProperty("AssetId", "AST-008821")
    .WithProperty("Team", "red");

var app = builder.Build();

app.MapGet("/", () => "Dut00.AppInfo sample. Open /appinfo.");

// Left open for the demo. In a real application protect it, for example with .RequireAuthorization().
app.MapAppInfo();

app.Run();
