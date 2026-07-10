using Microsoft.AspNetCore.SignalR.Client;

namespace Falcom;

public sealed class FalcomKranLiveSignalRClient : IAsyncDisposable
{
   private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);
   private static readonly TimeSpan ReconnectAttemptInterval = TimeSpan.FromSeconds(30);
   private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(2);
   private static readonly TimeSpan ErrorLogThrottle = TimeSpan.FromMinutes(1);

   private readonly ILogger<FalcomKranLiveSignalRClient> logger;
   private readonly ConfigManager configManager;
   private readonly SemaphoreSlim connectionLock = new(1, 1);
   private readonly object latestSyncRoot = new();
   private readonly CancellationTokenSource senderCancellation = new();
   private readonly Task senderTask;

   private HubConnection? connection;
   private DateTime nextErrorLogUtc = DateTime.MinValue;
   private DateTime nextConnectAttemptUtc = DateTime.MinValue;
   private string? activeHubUrl;

   private LatestSpsLebensZaehler? latestSpsLebensZaehler;
   private LatestKranPosition? latestKranPosition;
   private readonly List<LatestKranOpcEvent> pendingKranOpcEvents = new();

   public FalcomKranLiveSignalRClient(
      ILogger<FalcomKranLiveSignalRClient> logger,
      ConfigManager configManager)
   {
      this.logger = logger;
      this.configManager = configManager;
      senderTask = Task.Run(() => SendLoopAsync(senderCancellation.Token));
   }

   public Task SendSpsLebensZaehlerAsync(
      int lebensZaehler,
      CancellationToken cancellationToken)
   {
      if (cancellationToken.IsCancellationRequested)
      {
         return Task.FromCanceled(cancellationToken);
      }

      lock (latestSyncRoot)
      {
         latestSpsLebensZaehler = new LatestSpsLebensZaehler(
            lebensZaehler,
            DateTime.UtcNow);
      }

      return Task.CompletedTask;
   }

   public Task SendKranPositionAsync(
      int? posKranX,
      int? posKatzeY,
      int? posHubZ,
      CancellationToken cancellationToken)
   {
      if (cancellationToken.IsCancellationRequested)
      {
         return Task.FromCanceled(cancellationToken);
      }

      lock (latestSyncRoot)
      {
         latestKranPosition = new LatestKranPosition(
            posKranX,
            posKatzeY,
            posHubZ,
            DateTime.UtcNow);
      }

      return Task.CompletedTask;
   }

   public Task SendKranOpcEventAsync(
      int eventId,
      string eventName,
      string direction,
      string triggerNodeName,
      object? triggerValue,
      IReadOnlyDictionary<string, object?> values,
      CancellationToken cancellationToken)
   {
      if (cancellationToken.IsCancellationRequested)
      {
         return Task.FromCanceled(cancellationToken);
      }

      lock (latestSyncRoot)
      {
         pendingKranOpcEvents.Add(new LatestKranOpcEvent(
            eventId,
            eventName,
            direction,
            triggerNodeName,
            triggerValue,
            values
               .Select(pair => new LatestKranOpcEventItem(pair.Key, pair.Value))
               .ToArray(),
            DateTime.UtcNow));
      }

      return Task.CompletedTask;
   }

   private async Task SendLoopAsync(CancellationToken cancellationToken)
   {
      using PeriodicTimer timer = new(SendInterval);

      try
      {
         while (await timer.WaitForNextTickAsync(cancellationToken))
         {
            await TrySendLatestAsync(cancellationToken);
         }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         // normales Beenden
      }
      catch (Exception ex)
      {
         logger.LogInformation(
            "005D|SignalR-Live-Sender wurde unerwartet beendet. Live-Visu faellt aus, Falcom laeuft weiter. Grund={Reason}.",
            BuildShortReason(ex));
      }
   }

   private async Task TrySendLatestAsync(CancellationToken cancellationToken)
   {
      string hubUrl = configManager.KranLiveHubUrl;

      if (string.IsNullOrWhiteSpace(hubUrl))
      {
         return;
      }

      LatestSpsLebensZaehler? spsLebensZaehler;
      LatestKranPosition? kranPosition;
      LatestKranOpcEvent[] kranOpcEvents;

      lock (latestSyncRoot)
      {
         spsLebensZaehler = latestSpsLebensZaehler;
         kranPosition = latestKranPosition;
         kranOpcEvents = pendingKranOpcEvents.ToArray();
      }

      if (spsLebensZaehler is null && kranPosition is null && kranOpcEvents.Length == 0)
      {
         return;
      }

      try
      {
         HubConnection? hubConnection = await GetStartedConnectionAsync(
            hubUrl.Trim(),
            cancellationToken);

         if (hubConnection is null)
         {
            return;
         }

         if (spsLebensZaehler is not null)
         {
            await InvokeWithTimeoutAsync(
               hubConnection,
               "PublishSpsLebensZaehler",
               new
               {
                  LebensZaehler = spsLebensZaehler.LebensZaehler,
                  TimestampUtc = spsLebensZaehler.TimestampUtc,
                  Source = "Kran-SPS"
               },
               cancellationToken);
         }

         if (kranPosition is not null)
         {
            await InvokeWithTimeoutAsync(
               hubConnection,
               "PublishKranPosition",
               new
               {
                  PosKranX = kranPosition.PosKranX,
                  PosKatzeY = kranPosition.PosKatzeY,
                  PosHubZ = kranPosition.PosHubZ,
                  TimestampUtc = kranPosition.TimestampUtc,
                  Source = "Kran-SPS"
               },
               cancellationToken);
         }

         foreach (LatestKranOpcEvent kranOpcEvent in kranOpcEvents)
         {
            await InvokeWithTimeoutAsync(
               hubConnection,
               "PublishKranOpcEvent",
               new
               {
                  EventId = kranOpcEvent.EventId,
                  EventName = kranOpcEvent.EventName,
                  Direction = kranOpcEvent.Direction,
                  TriggerNodeName = kranOpcEvent.TriggerNodeName,
                  TriggerValue = Convert.ToString(kranOpcEvent.TriggerValue),
                  TimestampUtc = kranOpcEvent.TimestampUtc,
                  Source = "Falcom/OPC",
                  Items = kranOpcEvent.Items.Select(item => new
                  {
                     NodeName = item.NodeName,
                     Value = Convert.ToString(item.Value)
                  }).ToArray()
               },
               cancellationToken);
         }

         if (kranOpcEvents.Length > 0)
         {
            lock (latestSyncRoot)
            {
               foreach (LatestKranOpcEvent sentEvent in kranOpcEvents)
               {
                  pendingKranOpcEvents.Remove(sentEvent);
               }
            }
         }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
         throw;
      }
      catch (Exception ex)
      {
         LogSendProblemIfDue(ex, hubUrl);
         await ResetConnectionAfterProblemAsync();
      }
   }

   private async Task<HubConnection?> GetStartedConnectionAsync(
      string hubUrl,
      CancellationToken cancellationToken)
   {
      await connectionLock.WaitAsync(cancellationToken);

      try
      {
         if (!string.Equals(activeHubUrl, hubUrl, StringComparison.OrdinalIgnoreCase))
         {
            if (connection is not null)
            {
               await connection.DisposeAsync();
            }

            connection = null;
            activeHubUrl = hubUrl;
            nextConnectAttemptUtc = DateTime.MinValue;
         }

         if (connection is null)
         {
            connection = new HubConnectionBuilder()
               .WithUrl(hubUrl)
               .WithAutomaticReconnect()
               .Build();
         }

         if (connection.State == HubConnectionState.Connected)
         {
            return connection;
         }

         if (connection.State != HubConnectionState.Disconnected)
         {
            return null;
         }

         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextConnectAttemptUtc)
         {
            return null;
         }

         nextConnectAttemptUtc = nowUtc.Add(ReconnectAttemptInterval);

         logger.LogDebug(
            "0054|SignalR-Verbindung zur Kran-Webseite wird aufgebaut: {HubUrl}",
            hubUrl);

         using CancellationTokenSource timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
         timeoutCancellation.CancelAfter(OperationTimeout);

         await connection.StartAsync(timeoutCancellation.Token);
         return connection;
      }
      finally
      {
         connectionLock.Release();
      }
   }

   private static async Task InvokeWithTimeoutAsync(
      HubConnection hubConnection,
      string methodName,
      object payload,
      CancellationToken cancellationToken)
   {
      using CancellationTokenSource timeoutCancellation =
         CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      timeoutCancellation.CancelAfter(OperationTimeout);

      await hubConnection.InvokeAsync(
         methodName,
         payload,
         timeoutCancellation.Token);
   }

   private async Task ResetConnectionAfterProblemAsync()
   {
      if (!await connectionLock.WaitAsync(0))
      {
         return;
      }

      try
      {
         if (connection is null)
         {
            return;
         }

         await connection.DisposeAsync();
         connection = null;
         nextConnectAttemptUtc = DateTime.UtcNow.Add(ReconnectAttemptInterval);
      }
      catch
      {
         connection = null;
         nextConnectAttemptUtc = DateTime.UtcNow.Add(ReconnectAttemptInterval);
      }
      finally
      {
         connectionLock.Release();
      }
   }

   private void LogSendProblemIfDue(Exception ex, string hubUrl)
   {
      DateTime nowUtc = DateTime.UtcNow;

      if (nowUtc < nextErrorLogUtc)
      {
         return;
      }

      logger.LogInformation(
         "0055|Kran-Webseite/SignalR-Hub ist aktuell nicht erreichbar. Kein Fehler ! Live-Anzeige wird ausgelassen. Hub={HubUrl}, Grund={Reason}. Neuer Hinweis fruehestens in {DelaySeconds:N0} Sekunden.",
         hubUrl,
         BuildShortReason(ex),
         ErrorLogThrottle.TotalSeconds);

      nextErrorLogUtc = nowUtc.Add(ErrorLogThrottle);
   }

   private static string BuildShortReason(Exception ex)
   {
      Exception root = ex;

      while (root.InnerException is not null)
      {
         root = root.InnerException;
      }

      return $"{root.GetType().Name}: {root.Message}";
   }

   public async ValueTask DisposeAsync()
   {
      await senderCancellation.CancelAsync();

      try
      {
         await senderTask;
      }
      catch (OperationCanceledException)
      {
         // normales Beenden
      }

      senderCancellation.Dispose();

      if (connection is not null)
      {
         await connection.DisposeAsync();
      }

      connectionLock.Dispose();
   }

   private sealed record LatestSpsLebensZaehler(
      int LebensZaehler,
      DateTime TimestampUtc);

   private sealed record LatestKranPosition(
      int? PosKranX,
      int? PosKatzeY,
      int? PosHubZ,
      DateTime TimestampUtc);

   private sealed record LatestKranOpcEvent(
      int EventId,
      string EventName,
      string Direction,
      string TriggerNodeName,
      object? TriggerValue,
      IReadOnlyList<LatestKranOpcEventItem> Items,
      DateTime TimestampUtc);

   private sealed record LatestKranOpcEventItem(
      string NodeName,
      object? Value);
}
