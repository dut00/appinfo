using AspNetCore.AppInfo;
using AspNetCore.AppInfo.Configuration;
using AspNetCore.AppInfo.Environment;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppInfo()
    .WithConfigurationDetails()
    .WithEnvironmentDetails()
    .WithOwner(o => o.Owner = "red_team@company.com")
    .WithProperty("Team", "red");

var app = builder.Build();

app.MapGet("/", () => "AspNetCore.AppInfo sample. Open /appinfo.");

// Left open for the demo. In a real application protect it, for example with .RequireAuthorization().
app.MapAppInfo();

app.Run();
