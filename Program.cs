using Falcom;
using static Falcom.ConfigManager;

var builder = Host.CreateApplicationBuilder(args);


builder.Services.Configure<Appsettings>(builder.Configuration.GetSection("Appsettings"));
builder.Services.AddSingleton<ConfigManager>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
