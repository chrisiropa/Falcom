namespace Falcom
{
   public sealed class WatchdogEvent : FalcomEventBase
   {
      public const string EventName = "LebensZaehlerFalcom";
      public WatchdogEvent(int lebensZaehler)
      {
         LebensZaehler = lebensZaehler;
      }

      public override string Source => "Worker-Zeitsteuerung";

      public override bool IsStateTrigger => false;

      public int LebensZaehler { get; }
   }
}
