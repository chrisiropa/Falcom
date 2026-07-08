using Microsoft.AspNetCore.SignalR.Client;

namespace Falcom;

public sealed class FalcomKranLiveSignalRClient : IAsyncDisposable
{
   private static readonly TimeSpan ErrorLogThrottle = TimeSpan.FromMinutes(1);

   private readonly ILogger<FalcomKranLiveSignalRClient> logger;
   private readonly ConfigManager configManager;
   private readonly SemaphoreSlim connectionLock = new(1, 1);
   private HubConnection? connection;
   private DateTime nextErrorLogUtc = DateTime.MinValue;

   public FalcomKranLiveSignalRClient(
      ILogger<FalcomKranLiveSignalRClient> logger,
      ConfigManager configManager)
   {
      this.logger = logger;
      this.configManager = configManager;
   }

   public async Task SendSpsLebensZaehlerAsync(
      int lebensZaehler,
      CancellationToken cancellationToken)
   {
      string hubUrl = configManager.KranLiveHubUrl;

      if (string.IsNullOrWhiteSpace(hubUrl))
      {
         return;
      }

      try
      {
         HubConnection hubConnection = await GetStartedConnectionAsync(
            hubUrl.Trim(),
            cancellationToken);

         await hubConnection.InvokeAsync(
            "PublishSpsLebensZaehler",
            new
            {
               LebensZaehler = lebensZaehler,
               TimestampUtc = DateTime.UtcNow,
               Source = "Kran-SPS"
            },
            cancellationToken);

         logger.LogDebug(
            "0056|SPS-LebensZaehler {LebensZaehler} wurde an die Kran-Webseite gesendet.",
            lebensZaehler);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception ex)
      {
         LogSendProblemIfDue(ex);
      }
   }

   public async Task SendKranPositionAsync(
      int? posKranX,
      int? posKatzeY,
      int? posHubZ,
      CancellationToken cancellationToken)
   {
      string hubUrl = configManager.KranLiveHubUrl;

      if (string.IsNullOrWhiteSpace(hubUrl))
      {
         return;
      }

      try
      {
         HubConnection hubConnection = await GetStartedConnectionAsync(
            hubUrl.Trim(),
            cancellationToken);

         await hubConnection.InvokeAsync(
            "PublishKranPosition",
            new
            {
               PosKranX = posKranX,
               PosKatzeY = posKatzeY,
               PosHubZ = posHubZ,
               TimestampUtc = DateTime.UtcNow,
               Source = "Kran-SPS"
            },
            cancellationToken);

         logger.LogDebug(
            "0059|Kranposition wurde an die Kran-Webseite gesendet. X={PosKranX}, Y={PosKatzeY}, Z={PosHubZ}.",
            posKranX,
            posKatzeY,
            posHubZ);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception ex)
      {
         LogSendProblemIfDue(ex);
      }
   }
   private async Task<HubConnection> GetStartedConnectionAsync(
      string hubUrl,
      CancellationToken cancellationToken)
   {
      await connectionLock.WaitAsync(cancellationToken);

      try
      {
         if (connection is null)
         {
            connection = new HubConnectionBuilder()
               .WithUrl(hubUrl)
               .WithAutomaticReconnect()
               .Build();
         }

         if (connection.State != HubConnectionState.Connected)
         {
            logger.LogInformation(
               "0054|SignalR-Verbindung zur Kran-Webseite wird aufgebaut: {HubUrl}",
               hubUrl);

            await connection.StartAsync(cancellationToken);
         }

         return connection;
      }
      finally
      {
         connectionLock.Release();
      }
   }

   private void LogSendProblemIfDue(Exception ex)
   {
      DateTime nowUtc = DateTime.UtcNow;

      if (nowUtc < nextErrorLogUtc)
      {
         return;
      }

      logger.LogWarning(
         ex,
         "0055|SPS-LebensZaehler konnte nicht an die Kran-Webseite gesendet werden. Weitere gleiche SignalR-Fehler werden fuer 60 Sekunden gedrosselt.");

      nextErrorLogUtc = nowUtc.Add(ErrorLogThrottle);
   }

   public async ValueTask DisposeAsync()
   {
      if (connection is null)
      {
         return;
      }

      await connection.DisposeAsync();
   }
}
