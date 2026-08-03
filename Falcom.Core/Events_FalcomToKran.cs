namespace Falcom
{
   /// <summary>
   /// Fahrauftrag von FALCOM an die Kran-SPS.
   /// </summary>
   public sealed class KranfahrtAuftragEvent : FalcomEventBase
   {
      public const string EventName = "Event_102";
      public const string Direction = "FALCOM->KRAN_SPS";

      public const string AuftragNummerNodeName = "Nr";
      public const string AuftragTeilfahrtNodeName = "TeilNr";
      public const string QuelleNodeName = "Quelle";
      public const string ZielNodeName = "Ziel";
      public const string SollMasseNodeName = "SollMasse";
      public const string ToleranzNodeName = "Toleranz";
      public const string EventTriggerNodeName = EventName;
      public const string ZaehlerAnfahrtNodeName = "ZielPos";
      public const string MaterialNrNodeName = "MaterialNr";

      public KranfahrtAuftragEvent(
         long? aktuelleFahrtID,
         long auftragNummer,
         int auftragTeilfahrt,
         long quellePositionID,
         long zielPositionID,
         decimal sollMasseKg,
         decimal toleranzKg,
         int zaehlerAnfahrt = 0,
         int materialNr = 0)
      {
         AktuelleFahrtID = aktuelleFahrtID;
         AuftragNummer = auftragNummer;
         AuftragTeilfahrt = auftragTeilfahrt;
         QuellePositionID = quellePositionID;
         ZielPositionID = zielPositionID;
         SollMasseKg = sollMasseKg;
         ToleranzKg = toleranzKg;
         ZaehlerAnfahrt = zaehlerAnfahrt;
         MaterialNr = materialNr;
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

      public int MaterialNr { get; }

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
