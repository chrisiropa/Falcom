namespace Falcom
{
   public sealed class KranPositionenAnforderungEvent : FalcomEventBase
   {
      public const string EventName = "Event_206";
      public const string Direction = "KRAN_SPS->FALCOM";
      public const string TriggerNodeName = EventName;

      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => false;

      public int AnforderungsZaehler { get; }
      public bool IstInitialwert { get; }

      public KranPositionenAnforderungEvent(int anforderungsZaehler, bool istInitialwert)
      {
         AnforderungsZaehler = anforderungsZaehler;
         IstInitialwert = istInitialwert;
      }
   }

   public sealed record KranPositionenUnterposition(
      int ArrayIndex,
      int DiKatzeX,
      int DiKranY);

   public sealed record KranPositionenEintrag(
      int ArrayIndex,
      string Art,
      string Bezeichnung,
      string PositionsTyp,
      int ID,
      int DiKatzeStartX,
      int DiKatzeBreiteX,
      int DiKranStartY,
      int DiKranLaengeY,
      int DiHubStartZ,
      int DiHubHoeheZ,
      int PositionsAnz,
      IReadOnlyList<KranPositionenUnterposition> Positionen);

   public sealed record KranPositionenSnapshot(IReadOnlyList<KranPositionenEintrag> Eintraege)
   {
      public int AnzahlPositionen => Eintraege.Count;
   }
}
