using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace Falcom;

public sealed class FalcomKranLiveBroadcaster : BackgroundService
{
   private readonly FalcomKranLiveStatusService liveStatusService;
   private readonly IHubContext<FalcomKranLiveHub> hubContext;
   private readonly Channel<LiveUpdate> updates =
      Channel.CreateUnbounded<LiveUpdate>(new UnboundedChannelOptions
      {
         SingleReader = true,
         SingleWriter = false
      });

   public FalcomKranLiveBroadcaster(
      FalcomKranLiveStatusService liveStatusService,
      IHubContext<FalcomKranLiveHub> hubContext)
   {
      this.liveStatusService = liveStatusService;
      this.hubContext = hubContext;
   }

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      liveStatusService.SpsLebensZaehlerChanged += OnSpsLebensZaehlerChanged;
      liveStatusService.KranPositionChanged += OnKranPositionChanged;
      liveStatusService.KranOpcEventChanged += OnKranOpcEventChanged;

      try
      {
         await foreach (LiveUpdate update in updates.Reader.ReadAllAsync(stoppingToken))
         {
            await hubContext.Clients.All.SendAsync(
               update.MethodName,
               update.Payload,
               stoppingToken);
         }
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
         // normales Beenden
      }
      finally
      {
         liveStatusService.SpsLebensZaehlerChanged -= OnSpsLebensZaehlerChanged;
         liveStatusService.KranPositionChanged -= OnKranPositionChanged;
         liveStatusService.KranOpcEventChanged -= OnKranOpcEventChanged;
      }
   }

   private void OnSpsLebensZaehlerChanged(SpsLebensZaehlerSnapshot snapshot)
   {
      updates.Writer.TryWrite(new LiveUpdate("SpsLebensZaehler", snapshot));
   }

   private void OnKranPositionChanged(KranPositionSnapshot snapshot)
   {
      updates.Writer.TryWrite(new LiveUpdate("KranPosition", snapshot));
   }

   private void OnKranOpcEventChanged(KranOpcEventSnapshot snapshot)
   {
      updates.Writer.TryWrite(new LiveUpdate("KranOpcEvent", snapshot));
   }

   private sealed record LiveUpdate(
      string MethodName,
      object Payload);
}
