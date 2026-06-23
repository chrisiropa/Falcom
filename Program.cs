using Falcom;
using static Falcom.ConfigManager;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
   options.ServiceName = "IROPA Falcom";
});

builder.Logging.ClearProviders();
builder.Logging.AddFalcomLogging(builder.Configuration);

builder.Services.Configure<Appsettings>(builder.Configuration.GetSection("Appsettings"));
builder.Services.AddSingleton<ConfigManager>();
builder.Services.AddSingleton<Parameter>();
builder.Services.AddSingleton<Lager>();;
builder.Services.AddSingleton<OPC_Client_Crane>();
builder.Services.AddSingleton<FalcomEventQueue>();
builder.Services.AddHostedService<DatabaseOrderPoller>();
builder.Services.AddHostedService<WatchdogSender>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ConsoleShutdownService>();

var host = builder.Build();
host.Run();
