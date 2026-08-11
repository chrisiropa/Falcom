namespace Falcom
{
   public sealed class CwWatchdogEvent : FalcomEventBase
   {
      public const string EventName = "Event_301";

      public CwWatchdogEvent(int lebensZaehler)
      {
         LebensZaehler = lebensZaehler;
      }

      public override string Source => "Worker-Zeitsteuerung";

      public override bool IsStateTrigger => false;

      public int LebensZaehler { get; }
   }
}
