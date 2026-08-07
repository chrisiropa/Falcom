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
      public const string QuelleUnterpositionNodeName = "QuelleUnterPos";
      public const string ZielUnterpositionNodeName = "ZielUnterPos";
      public const string SollMasseNodeName = "SollMasse";
      public const string MasseTolPosNodeName = "MasseTol_pos";
      public const string MasseTolNegNodeName = "MasseTol_neg";
      public const string EventTriggerNodeName = EventName;
      public const string MaterialNrNodeName = "MaterialNr";

      public KranfahrtAuftragEvent(
         long? aktuelleFahrtID,
         long auftragNummer,
         int auftragTeilfahrt,
         long quellePositionID,
         long zielPositionID,
         int quelleUnterposition,
         int zielUnterposition,
         decimal sollMasseKg,
         int masseTolPosKg,
         int masseTolNegKg,
         int materialNr = 0)
      {
         AktuelleFahrtID = aktuelleFahrtID;
         AuftragNummer = auftragNummer;
         AuftragTeilfahrt = auftragTeilfahrt;
         QuellePositionID = quellePositionID;
         ZielPositionID = zielPositionID;
         QuelleUnterposition = quelleUnterposition;
         ZielUnterposition = zielUnterposition;
         SollMasseKg = sollMasseKg;
         MasseTolPosKg = masseTolPosKg;
         MasseTolNegKg = masseTolNegKg;
         MaterialNr = materialNr;
      }

      public override string Source => "FALCOM";

      public override bool IsStateTrigger => true;

      public long? AktuelleFahrtID { get; }

      public long AuftragNummer { get; }

      public int AuftragTeilfahrt { get; }

      public long QuellePositionID { get; }

      public long ZielPositionID { get; }

      public int QuelleUnterposition { get; }

      public int ZielUnterposition { get; }

      public decimal SollMasseKg { get; }

      public int MasseTolPosKg { get; }

      public int MasseTolNegKg { get; }

      public int MaterialNr { get; }

      public static KranfahrtAuftragEvent FromAktuelleFahrt(
         AktuelleFahrtResult aktuelleFahrt,
         int auftragTeilfahrt)
      {
         return new KranfahrtAuftragEvent(
            aktuelleFahrt.AktuelleFahrtID,
            aktuelleFahrt.AuftragID ?? 0,
            aktuelleFahrt.AuftragTeilfahrt ?? auftragTeilfahrt,
            aktuelleFahrt.QuellePositionID ?? 0,
            aktuelleFahrt.ZielPositionID ?? 0,
            aktuelleFahrt.QuelleUnterposition ?? 0,
            aktuelleFahrt.ZielUnterposition ?? 0,
            aktuelleFahrt.SollMengeKg ?? 0m,
            aktuelleFahrt.MasseTolPosKg ?? 0,
            aktuelleFahrt.MasseTolNegKg ?? 0,
            aktuelleFahrt.MaterialNr ?? 0);
      }
   }
}
