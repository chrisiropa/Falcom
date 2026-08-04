namespace Falcom
{
   public sealed class BunkerMaterialAnforderungEvent : FalcomEventBase
   {
      public const string EventName = "Event_204";
      public const string Direction = "KRAN_SPS->FALCOM";
      public const string TriggerNodeName = EventName;

      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => false;

      public int AnforderungsZaehler { get; }
      public bool IstInitialwert { get; }

      public BunkerMaterialAnforderungEvent(int anforderungsZaehler, bool istInitialwert)
      {
         AnforderungsZaehler = anforderungsZaehler;
         IstInitialwert = istInitialwert;
      }
   }

   public sealed record BunkerMaterialEintrag(int ArrayIndex, int BuNr, int MaterialNr);

   public sealed record BunkerMaterialSnapshot(IReadOnlyList<BunkerMaterialEintrag> Eintraege)
   {
      public int AnzahlBunker => Eintraege.Count;
   }
}
