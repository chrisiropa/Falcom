namespace Falcom
{
   /// <summary> Signalisiert das Ende eines physischen Hubs </summary>
   public sealed class KranfahrtBeendetEvent : FalcomEventBase
   {
      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => true;

      public int AuftragsNummer { get; init; }
      public string KranQuelle { get; init; }
      public double Toleranz { get; init; }
      public double IstGewicht { get; init; }
      public int Fehlercode { get; init; }

      public KranfahrtBeendetEvent(int auftragsNummer, string kranQuelle, double toleranz, double istGewicht, int fehlercode)
      {
         AuftragsNummer = auftragsNummer;
         KranQuelle = kranQuelle;
         Toleranz = toleranz;
         IstGewicht = istGewicht;
         Fehlercode = fehlercode;
      }
   }

   
}