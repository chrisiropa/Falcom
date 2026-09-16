using Falcom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace FalcomWpf;

public partial class App : Application
{
   private IHost? host;

   protected override async void OnStartup(StartupEventArgs e)
   {
      base.OnStartup(e);

      try
      {
         HostApplicationBuilder builder = Host.CreateApplicationBuilder(e.Args);
         builder.Logging.ClearProviders();
         builder.Logging.AddFalcomLogging(builder.Configuration);

         builder.Services.AddFalcomCore(builder.Configuration);
         builder.Services.AddSingleton<MainWindow>();

         host = builder.Build();
         ProgramStartBanner.WriteToLogfile(
            host.Services.GetRequiredService<FalcomFileSink>(),
            "FALCOM WPF",
            "FALCOM WPF PROGRAMMSTART");
         await host.StartAsync();

         MainWindow = host.Services.GetRequiredService<MainWindow>();
         MainWindow.Show();
      }
      catch (Exception exception)
      {
         string fallbackLogPath = WriteStartupFailure(exception);
         MessageBox.Show(
            "FALCOM WPF konnte nicht gestartet werden.\n\n"
            + "Pruefe die Datenbankverbindung und die appsettings.json.\n\n"
            + $"Fehlerdetails: {fallbackLogPath}",
            "FALCOM Diagnose",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
         Shutdown(-1);
      }
   }

   private string WriteStartupFailure(Exception exception)
   {
      string detail = $"{DateTime.Now:dd.MM.yy HH:mm:ss.fff} [CRT] FALCOM WPF konnte nicht gestartet werden.{Environment.NewLine}{exception}{Environment.NewLine}";

      try
      {
         host?.Services.GetService<FalcomFileSink>()?.Write(detail);
      }
      catch
      {
         // The configured log target may itself be unavailable during startup.
      }

      string fallbackLogPath = Path.Combine(AppContext.BaseDirectory, "FalcomWpf-startup-error.log");
      try
      {
         File.AppendAllText(fallbackLogPath, detail);
      }
      catch
      {
         fallbackLogPath = "Windows-Ereignisanzeige (Anwendung)";
      }

      return fallbackLogPath;
   }

   protected override async void OnExit(ExitEventArgs e)
   {
      if (host is not null)
      {
         try
         {
            await host.StopAsync(TimeSpan.FromSeconds(5));
         }
         catch (OperationCanceledException)
         {
            // Normales Beenden: laufende Hintergrundaufgaben dürfen beim Schließen abbrechen.
         }
         finally
         {
            host.Dispose();
         }
      }

      base.OnExit(e);
   }
}
