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

builder.Services.AddFalcomCore(builder.Configuration);
builder.Services.AddHostedService<ConsoleShutdownService>();

var host = builder.Build();
var configManager = host.Services.GetRequiredService<ConfigManager>();
var fileSink = host.Services.GetRequiredService<FalcomFileSink>();
ProgramStartBanner.WriteToLogfile(fileSink, "FALCOM", "FALCOM PROGRAMMSTART");

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
