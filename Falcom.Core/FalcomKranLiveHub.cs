using Microsoft.AspNetCore.SignalR;

namespace Falcom;

public sealed class FalcomKranLiveHub : Hub
{
   private readonly FalcomKranLiveStatusService liveStatusService;

   public FalcomKranLiveHub(FalcomKranLiveStatusService liveStatusService)
   {
      this.liveStatusService = liveStatusService;
   }

   public override async Task OnConnectedAsync()
   {
      SpsLebensZaehlerSnapshot? spsLebensZaehler =
         liveStatusService.GetSpsLebensZaehler();
      KranPositionSnapshot? kranPosition =
         liveStatusService.GetKranPosition();

      if (spsLebensZaehler is not null)
      {
         await Clients.Caller.SendAsync(
            "SpsLebensZaehler",
            spsLebensZaehler,
            Context.ConnectionAborted);
      }

      if (kranPosition is not null)
      {
         await Clients.Caller.SendAsync(
            "KranPosition",
            kranPosition,
            Context.ConnectionAborted);
      }

      foreach (KranOpcEventSnapshot snapshot in liveStatusService.GetKranOpcEvents())
      {
         await Clients.Caller.SendAsync(
            "KranOpcEvent",
            snapshot,
            Context.ConnectionAborted);
      }

      await base.OnConnectedAsync();
   }
}
