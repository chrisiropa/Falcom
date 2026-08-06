namespace Falcom
{
   public sealed class MaterialEigenschaftenAnforderungEvent : FalcomEventBase
   {
      public const string EventName = "Event_205";
      public const string Direction = "KRAN_SPS->FALCOM";
      public const string TriggerNodeName = EventName;

      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => false;

      public int AnforderungsZaehler { get; }
      public bool IstInitialwert { get; }

      public MaterialEigenschaftenAnforderungEvent(int anforderungsZaehler, bool istInitialwert)
      {
         AnforderungsZaehler = anforderungsZaehler;
         IstInitialwert = istInitialwert;
      }
   }

   public sealed record MaterialEigenschaftenEintrag(
      int ArrayIndex,
      int ID,
      string MatName,
      DateTime? DatumZeit,
      int DiMasseGattMin,
      int DiZeitAbtippen,
      int DiMasseVorAbtippen,
      int DiMasseTolPos,
      int DiMasseTolNeg,
      float RKraftStufenPro100Kg,
      bool XAbwurfFlach,
      bool XAbwurfAbzett,
      bool XAbwurfTippen,
      bool XNachfassenMagAus,
      bool XNachfassenMagDauernd,
      bool XKreislauf);

   public sealed record MaterialEigenschaftenSnapshot(IReadOnlyList<MaterialEigenschaftenEintrag> Eintraege)
   {
      public int AnzahlMaterialien => Eintraege.Count;
   }
}
