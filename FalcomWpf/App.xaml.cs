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
