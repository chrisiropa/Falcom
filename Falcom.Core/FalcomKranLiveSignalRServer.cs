using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Falcom;

public sealed class FalcomKranLiveSignalRServer : IHostedService, IAsyncDisposable
{
   private readonly ConfigManager configManager;
   private readonly FalcomKranLiveStatusService liveStatusService;
   private readonly ILogger<FalcomKranLiveSignalRServer> logger;

   private WebApplication? app;

   public FalcomKranLiveSignalRServer(
      ConfigManager configManager,
      FalcomKranLiveStatusService liveStatusService,
      ILogger<FalcomKranLiveSignalRServer> logger)
   {
      this.configManager = configManager;
      this.liveStatusService = liveStatusService;
      this.logger = logger;
   }

   public async Task StartAsync(CancellationToken cancellationToken)
   {
      string hubUrl = configManager.KranLiveHubUrl;

      if (string.IsNullOrWhiteSpace(hubUrl)
          || !Uri.TryCreate(hubUrl, UriKind.Absolute, out Uri? hubUri))
      {
         logger.LogInformation(
            "0065|Falcom SignalR-Live-Server nicht gestartet: KranLiveHubUrl ist nicht gueltig konfiguriert.");
         return;
      }

      string path = string.IsNullOrWhiteSpace(hubUri.AbsolutePath)
         ? "/falcom-kran-hub"
         : hubUri.AbsolutePath;
      string listenUrl = $"{hubUri.Scheme}://{hubUri.Host}:{hubUri.Port}";

      WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
      builder.WebHost.UseUrls(listenUrl);
      builder.Logging.ClearProviders();
      builder.Services.AddSignalR();
      builder.Services.AddSingleton(liveStatusService);
      builder.Services.AddHostedService<FalcomKranLiveBroadcaster>();

      app = builder.Build();
      app.MapHub<FalcomKranLiveHub>(path);

      await app.StartAsync(cancellationToken);

      logger.LogInformation(
         "0066|Falcom SignalR-Live-Server gestartet: {HubUrl}",
         hubUrl);
   }

   public async Task StopAsync(CancellationToken cancellationToken)
   {
      if (app is null)
      {
         return;
      }

      await app.StopAsync(cancellationToken);
   }

   public async ValueTask DisposeAsync()
   {
      if (app is not null)
      {
         await app.DisposeAsync();
      }
   }
}
