using Falcom;
using static Falcom.ConfigManager;

AssemblyInfoWrapper programInformation = new();
PrintProgramInformation(programInformation);

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
builder.Services.AddSingleton<WatchdogSender>();
builder.Services.AddSingleton<AktuelleFahrtRepository>();
builder.Services.AddHostedService<DatabaseOrderPoller>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ConsoleShutdownService>();

var host = builder.Build();
var configManager = host.Services.GetRequiredService<ConfigManager>();
var fileSink = host.Services.GetRequiredService<FalcomFileSink>();
WriteProgramInformationToLogfile(fileSink, programInformation);

Console.WriteLine(
   "ConnectionString: {0}",
   configManager.ConnectionString);
Console.WriteLine(new string('=', 72));

host.Run();

static void PrintProgramInformation(AssemblyInfoWrapper assemblyInfo)
{
   Console.WriteLine();
   Console.WriteLine(new string('=', 72));
   Console.WriteLine("              F A L C O M   P R O G R A M M S T A R T");
   Console.WriteLine(new string('=', 72));
   Console.WriteLine("  Programmname  : {0}", GetProgramName(assemblyInfo));
   Console.WriteLine("  Version       : {0}", GetVersion(assemblyInfo));
   Console.WriteLine(
      "  Build-Datum   : {0:dd.MM.yyyy HH:mm:ss}",
      assemblyInfo.FileModifiedTime);
   Console.WriteLine("  Autor         : Christof Goletzko");
   Console.WriteLine("  Firma         : IROPA Elektrotechnik GMBH");
   Console.WriteLine(new string('=', 72));
   Console.WriteLine();
}

static void WriteProgramInformationToLogfile(
   FalcomFileSink fileSink,
   AssemblyInfoWrapper assemblyInfo)
{
   string timestamp = DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff");
   fileSink.Write(
      $"""
      {timestamp} [INFORMATION] Falcom.Program: 0044|========================================================================
                            F A L C O M   P R O G R A M M S T A R T
      ========================================================================
        Programmname  : {GetProgramName(assemblyInfo)}
        Version       : {GetVersion(assemblyInfo)}
        Build-Datum   : {assemblyInfo.FileModifiedTime:dd.MM.yyyy HH:mm:ss}
        Autor         : Christof Goletzko
        Firma         : IROPA Elektrotechnik GMBH
      ========================================================================
      """);
}

static string GetProgramName(AssemblyInfoWrapper assemblyInfo)
{
   return string.IsNullOrWhiteSpace(assemblyInfo.ProduktName)
      ? assemblyInfo.Name
      : assemblyInfo.ProduktName;
}

static string GetVersion(AssemblyInfoWrapper assemblyInfo)
{
   return $"{assemblyInfo.Major}.{assemblyInfo.Minor}.{assemblyInfo.Build}.{assemblyInfo.Revision}";
}
