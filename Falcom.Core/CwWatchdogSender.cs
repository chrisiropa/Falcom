namespace Falcom
{
   public sealed class CwWatchdogSender : IDisposable
   {
      private static readonly TimeSpan SendErrorLogThrottle = TimeSpan.FromSeconds(60);
      private static readonly TimeSpan SendLockTimeout = TimeSpan.FromMilliseconds(250);

      private readonly ILogger<CwWatchdogSender> _logger;
      private readonly OPC_Client_Crane _opcClientCrane;
      private readonly SemaphoreSlim sendLock = new(1, 1);
      private DateTime nextSendErrorLogUtc = DateTime.MinValue;
      private bool initialValueLogged;

      public CwWatchdogSender(
         ILogger<CwWatchdogSender> logger,
         OPC_Client_Crane opcClientCrane)
      {
         _logger = logger;
         _opcClientCrane = opcClientCrane;
      }

      public async Task SendAsync(
         int lebensZaehler,
         CancellationToken stoppingToken)
      {
         if (!await sendLock.WaitAsync(SendLockTimeout, stoppingToken))
         {
            if (DateTime.UtcNow >= nextSendErrorLogUtc)
            {
               _logger.LogWarning(
                  "0307|CW-Watchdog-Lebenszaehler wird uebersprungen, weil ein vorheriger OPC-Sendelauf noch blockiert. LebensZaehler={LebensZaehler}.",
                  lebensZaehler);
               nextSendErrorLogUtc = DateTime.UtcNow + SendErrorLogThrottle;
            }

            return;
         }

         try
         {
            if (!initialValueLogged)
            {
               _logger.LogInformation(
                  "0303|CW-Watchdog-Verarbeitung im Dispatcher gestartet. Initialer DINT-Lebenszaehler={LebensZaehler}.",
                  lebensZaehler);
               initialValueLogged = true;
            }

            OPC_Client_Crane.OpcSendResult result =
               await _opcClientCrane.SendFalcomCwLebensZaehlerAsync(
                  lebensZaehler,
                  stoppingToken);

            if (result.Success)
            {
               nextSendErrorLogUtc = DateTime.MinValue;
               _logger.LogDebug(
                  "0304|CW-Watchdog LebensZaehler={Counter} gesendet.",
                  lebensZaehler);
               return;
            }

            if (DateTime.UtcNow >= nextSendErrorLogUtc)
            {
               _logger.LogWarning(
                  "0305|CW-Watchdog-Lebenszaehler konnte nicht gesendet werden. Grund={Reason}. Weitere gleiche Sendefehler werden fuer 60 Sekunden gedrosselt.",
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
      }
   }
}
