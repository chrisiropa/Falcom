namespace Falcom
{
   /// <summary>
   /// Fahrauftrag von FALCOM an die Kran-SPS.
   /// </summary>
   public sealed class KranfahrtAuftragEvent : FalcomEventBase
   {
      public const string EventName = "KranfahrtAuftrag";
      public const string Direction = "FALCOM->KRAN_SPS";

      public const string AuftragNummerNodeName = "AuftragNummer";
      public const string AuftragTeilfahrtNodeName = "AuftragTeilfahrt";
      public const string QuelleNodeName = "Quelle";
      public const string ZielNodeName = "Ziel";
      public const string SollMasseNodeName = "SollMasse";
      public const string ToleranzNodeName = "Toleranz";
      public const string TelegrammNummerNodeName = "TelegrammNummer";
      public const string ZaehlerAnfahrtNodeName = "ZaehlerAnfahrt";

      public KranfahrtAuftragEvent(
         long? aktuelleFahrtID,
         long auftragNummer,
         int auftragTeilfahrt,
         long quellePositionID,
         long zielPositionID,
         decimal sollMasseKg,
         decimal toleranzKg,
         int zaehlerAnfahrt = 0)
      {
         AktuelleFahrtID = aktuelleFahrtID;
         AuftragNummer = auftragNummer;
         AuftragTeilfahrt = auftragTeilfahrt;
         QuellePositionID = quellePositionID;
         ZielPositionID = zielPositionID;
         SollMasseKg = sollMasseKg;
         ToleranzKg = toleranzKg;
         ZaehlerAnfahrt = zaehlerAnfahrt;
      }

      public override string Source => "FALCOM";

      public override bool IsStateTrigger => true;

      public long? AktuelleFahrtID { get; }

      public long AuftragNummer { get; }

      public int AuftragTeilfahrt { get; }

      public long QuellePositionID { get; }

      public long ZielPositionID { get; }

      public decimal SollMasseKg { get; }

      public decimal ToleranzKg { get; }

      public int ZaehlerAnfahrt { get; private set; }

      public void SetZaehlerAnfahrt(int zaehlerAnfahrt)
      {
         ZaehlerAnfahrt = zaehlerAnfahrt;
      }

      public static KranfahrtAuftragEvent FromAktuelleFahrt(
         AktuelleFahrtResult aktuelleFahrt,
         int auftragTeilfahrt,
         decimal toleranzKg)
      {
         return new KranfahrtAuftragEvent(
            aktuelleFahrt.AktuelleFahrtID,
            aktuelleFahrt.AuftragID ?? 0,
            aktuelleFahrt.AuftragTeilfahrt ?? auftragTeilfahrt,
            aktuelleFahrt.QuellePositionID ?? 0,
            aktuelleFahrt.ZielPositionID ?? 0,
            aktuelleFahrt.SollMengeKg ?? 0m,
            toleranzKg);
      }
   }
}
