AppInfo

# About
Rozszerzenie jako paczka NuGet, która dodaje stronę z informacją o danej aplikacji.
Stronę tę można znaleźć pod stałym standardowym adresem `/appinfo`


# Inspiracja
Inspiracją była końcówka `/healthz` którą zaproponowano w google do sprawdzania czy dana aplikacja działa - Liveness Probe. Końcówka zwraca HTTP Status Code i na jego podstawie ocenia się zdrowie aplikacji.

# Inspistacja repa nuget
Podoba mi się struktura i podział na paczki taki jak jest w repo:
https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks


# NuGet i extensiony
Paczka może się nazywać `Kudu.AppInfo` i to zwróci paczkę z podstawowym responsem.
Extensiony mogłaby rzszerzać odpowiedź.


# Setup 
## Basic package
`Project.cs`>

```cs
using Kudu.AppInfo;
using Kudu.AppInfo.Configuration; //?
using Kudu.AppInfo.Environment;  //?
using Kudu.AppInfo.Loggers.Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseAppInfo()
    .WithConfigurationDetails()
    .WithEnvironmentDetails()
    .WithOwner(o => { o.Owner = "red_team@company.com"; } );

var app = builder.Build()

app.MapAppInfo(); // by default path is '/appinfo'
// app.MapAppInfo("/custom-appInfo-path");

app.Run()
```


### Example response

```json
{
    "ApplicationName" : "Company.Billing.Api",
    "Version" : "1.0.5.0",
    "Environment" : "Stage",
    "IsProduction" : false,

    "ConfigurationsFiles": [
        "/opt/app-root/appsettings.json",
        "/opt/app-root/appsettings.Stage.json"
    ],

    "ApplicationProcessUptime" : "10.09:34:52.5222809",
    "HostName" : "apps-host-01",
    "ContentRootPath": "/opt/app-root",
    "AssemblyLocation": "/opt/app-root/Company.Billing.Api.dll",
    
    "Owner" : "red_team@company.com",
}
```


## Basic + additional
`Project.cs`> 

```cs
using Kudu.AppInfo;
using Kudu.AppInfo.Configuration;
using Kudu.AppInfo.Environment;
using Kudu.AppInfo.Serilog;
// using Kudu.AppInfo.Loggers.Serilog; //?

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseAppInfo()
    .WithOwner(o => { o.Owner = "red_team@company.com"; } )
    // .ShowConnectionStrings(); // ??
    .WithSerilogInfo(); //?

var app = builder.Build()

app.MapAppInfo();

app.Run()
```

### Example response

```json
{
    "ApplicationName" : "Company.Billing.Api",
    "Version" : "1.0.5.0",
    "Environment" : "Stage",
    "IsProduction" : false,

    "Owner" : "red_team@company.com",

    // "ConnectionStrings": [
    //     "Data Source=aligathor.company.com; Initial Catalog=Tony;User", // trzeba na pewno ukrywać hasła wiec to moze nie być bezpieczne
    // ],

    "Serilog" : {
        "WriteTo": [
            { "Name": "Console" },
            {
                "Name": "File",
                "Args": {
                "Path": "logs/log.txt",
                "RollingInterval": "Day"
                }
            },
            {
                "Name": "Kafka",
                "Args": {
                "BootstrapServer": "els-kafka-node1.company.com:9092, els-kafka-node2.company.com:9092,els-kafka-node3.company.com:9092",
                "Index": "biling-api-stage"
                }
            }
        ]
    }
}
```


##

```cs


    "Loggers" : [
        "Serilog" : {
            "WriteTo": [
                { "Name": "Console" },
                {
                    "Name": "File",
                    "Args": {
                    "Path": "logs/log.txt",
                    "RollingInterval": "Day"
                    }
                },
                {
                    "Name": "Kafka",
                    "Args": {
                    "BootstrapServer": "els-kafka-node1.company.com:9092, els-kafka-node2.company.com:9092,els-kafka-node3.company.com:9092",
                    "Index": "biling-api-stage"
                    }
                }
            ]
        }
    ]
```