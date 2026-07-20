using System.Reflection;

namespace Falcom;

public static class ProgramStartBanner
{
   public static void WriteToLogfile(
      FalcomFileSink fileSink,
      string programName,
      string headline)
   {
      Assembly assembly = Assembly.GetEntryAssembly()
                          ?? Assembly.GetExecutingAssembly();
      AssemblyName assemblyName = assembly.GetName();
      Version? version = assemblyName.Version;
      string versionText = version is null
         ? "unbekannt"
         : $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
      string resolvedProgramName = string.IsNullOrWhiteSpace(programName)
         ? GetProductOrAssemblyName(assembly)
         : programName.Trim();
      string resolvedHeadline = string.IsNullOrWhiteSpace(headline)
         ? $"{resolvedProgramName} PROGRAMMSTART"
         : headline.Trim();
      DateTime buildDate = GetBuildDate(assembly);
      DateTime startTime = DateTime.Now;
      string timestamp = startTime.ToString("dd.MM.yy HH:mm:ss.fff");

      fileSink.Write(
         $"{timestamp} [INF] 0044|Programmstart{Environment.NewLine}{BuildBanner(resolvedHeadline, resolvedProgramName, versionText, buildDate, startTime)}");
   }

   private static string BuildBanner(
      string headline,
      string programName,
      string version,
      DateTime buildDate,
      DateTime startTime)
   {
      const int width = 78;
      string border = new('=', width);
      var lines = new List<string>
      {
         Center($"*** {headline} ***", width),
         border,
         $"  Programmname : {programName}",
         $"  Version      : {version}",
         $"  Build-Datum  : {buildDate:dd.MM.yyyy HH:mm:ss}",
         $"  Startzeit    : {startTime:dd.MM.yyyy HH:mm:ss}",
         "  Autor        : Christof Goletzko",
         "  Firma        : IROPA Elektrotechnik GMBH",
         border
      };

      return string.Join(Environment.NewLine, lines);
   }

   private static string Center(string text, int width)
   {
      if (text.Length >= width)
      {
         return text;
      }

      int left = (width - text.Length) / 2;
      return new string(' ', left) + text;
   }

   private static string GetProductOrAssemblyName(Assembly assembly)
   {
      string? product = assembly
         .GetCustomAttribute<AssemblyProductAttribute>()
         ?.Product;
      if (!string.IsNullOrWhiteSpace(product))
      {
         return product.Trim();
      }

      return assembly.GetName().Name ?? "Unbekannt";
   }

   private static DateTime GetBuildDate(Assembly assembly)
   {
      try
      {
         if (!string.IsNullOrWhiteSpace(assembly.Location)
             && File.Exists(assembly.Location))
         {
            return File.GetLastWriteTime(assembly.Location);
         }
      }
      catch
      {
      }

      return DateTime.Now;
   }
}