namespace Falcom;

/// <summary>
/// Historischer Name: Die Klasse ist inzwischen kein SignalR-Client mehr.
/// Sie ist der zentrale Publisher in den Falcom-internen Live-State.
/// Der Falcom-eigene SignalR-Server verteilt diesen Zustand an die Webanwendung.
/// </summary>
public sealed class FalcomKranLiveSignalRClient
{
   private readonly FalcomKranLiveStatusService liveStatusService;

   public FalcomKranLiveSignalRClient(
      FalcomKranLiveStatusService liveStatusService)
   {
      this.liveStatusService = liveStatusService;
   }

   public Task SendSpsLebensZaehlerAsync(
      int lebensZaehler,
      CancellationToken cancellationToken)
   {
      if (cancellationToken.IsCancellationRequested)
      {
         return Task.FromCanceled(cancellationToken);
      }

      liveStatusService.SetSpsLebensZaehler(
         lebensZaehler,
         DateTime.UtcNow,
         "Kran-SPS");

      return Task.CompletedTask;
   }

   public Task SendKranPositionAsync(
      int? posKranX,
      int? posKatzeY,
      int? posHubZ,
      int? magnetAn,
      CancellationToken cancellationToken)
   {
      if (cancellationToken.IsCancellationRequested)
      {
         return Task.FromCanceled(cancellationToken);
      }

      liveStatusService.SetKranPosition(
         posKranX,
         posKatzeY,
         posHubZ,
         magnetAn,
         DateTime.UtcNow,
         "Kran-SPS");

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

      liveStatusService.SetKranOpcEvent(
         eventId,
         eventName,
         direction,
         triggerNodeName,
         triggerValue,
         values,
         DateTime.UtcNow,
         "Falcom/OPC");

      return Task.CompletedTask;
   }
}
