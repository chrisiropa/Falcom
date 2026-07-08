namespace Falcom
{
   /// <summary>
   /// Lebenszaehler der Kran-SPS. Dieses Event dient als Datenfluss- und Lebenszeichen-Event.
   /// </summary>
   public sealed class KranSpsLebensZaehlerEvent : FalcomEventBase
   {
      public const string EventName = "LebensZaehlerKran";

      public KranSpsLebensZaehlerEvent(int lebensZaehler)
      {
         LebensZaehler = lebensZaehler;
      }

      public override string Source => "Kran-SPS";

      public override bool IsStateTrigger => false;

      public int LebensZaehler { get; }
   }
}
