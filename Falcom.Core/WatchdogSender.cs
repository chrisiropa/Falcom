namespace Falcom
{
   public sealed class WatchdogSender : IDisposable
   {
      private static readonly TimeSpan SendErrorLogThrottle = TimeSpan.FromSeconds(60);

      private readonly ILogger<WatchdogSender> _logger;
      private readonly OPC_Client_Crane _opcClientCrane;
      private readonly FalcomRuntimeStatus _runtimeStatus;
      private readonly SemaphoreSlim sendLock = new(1, 1);
      private DateTime nextSendErrorLogUtc = DateTime.MinValue;
      private bool initialValueLogged;

      public WatchdogSender(
         ILogger<WatchdogSender> logger,
         OPC_Client_Crane opcClientCrane,
         FalcomRuntimeStatus runtimeStatus)
      {
         _logger = logger;
         _opcClientCrane = opcClientCrane;
         _runtimeStatus = runtimeStatus;
      }

      public async Task SendAsync(
         int lebensZaehler,
         CancellationToken stoppingToken)
      {
         await sendLock.WaitAsync(stoppingToken);

         try
         {
            if (!initialValueLogged)
            {
               _logger.LogInformation(
                  "003A|Watchdog-Verarbeitung im Dispatcher gestartet. Initialer DINT-Lebenszaehler={LebensZaehler}. Versand laeuft ueber die zentrale Kran-OPC-Verbindung.",
                  lebensZaehler);
               initialValueLogged = true;
            }

            OPC_Client_Crane.OpcSendResult result =
               await _opcClientCrane.SendFalcomLebensZaehlerAsync(
                  lebensZaehler,
                  stoppingToken);

            if (result.Success)
            {
               nextSendErrorLogUtc = DateTime.MinValue;
               _runtimeStatus.SetWatchdogSent(lebensZaehler);
               _logger.LogDebug(
                  "003E|Watchdog LebensZaehler={Counter} ueber zentrale Kran-OPC-Verbindung gesendet.",
                  lebensZaehler);
               return;
            }

            _runtimeStatus.SetWatchdogError("Sendefehler");

            if (DateTime.UtcNow >= nextSendErrorLogUtc)
            {
               _logger.LogWarning(
                  "003C|Watchdog-Lebenszaehler konnte ueber die zentrale Kran-OPC-Verbindung nicht gesendet werden. Grund={Reason}. Weitere gleiche Sendefehler werden fuer 60 Sekunden gedrosselt.",
                  result.Reason);
               nextSendErrorLogUtc = DateTime.UtcNow + SendErrorLogThrottle;
            }
         }
         finally
         {
            sendLock.Release();
         }
      }

      public void Dispose()
      {
         sendLock.Dispose();
         _logger.LogInformation(
            "0042|Watchdog-Sender wird beendet. Es wurde keine eigene OPC-Verbindung verwendet.");
      }
   }
}