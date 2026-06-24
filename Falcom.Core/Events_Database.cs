using System;

namespace Falcom
{
   /// <summary>
   /// Wird vom DB-Poller ausgelöst, wenn ein neuer Chargierauftrag vom Bediener freigegeben wurde
   /// und aktuell kein anderer Auftrag aktiv in Arbeit ist.
   /// </summary>
   public sealed class OrderReleasedEvent : FalcomEventBase
   {
      // Da der Poller die SQL-Datenbank liest, ist die Quelle klar definiert
      public override string Source => "Database-Poller";

      // Dieses Event weckt die State Machine auf (schaltet von Idle -> In_Arbeit)
      public override bool IsStateTrigger => true;

      // Die fachlichen Nutzdaten (Payload) exakt passend zu deiner Stored Procedure
      public int AuftragsNummer { get; init; }
      public string EisensorteId { get; init; }
      public double ZielGewichtKg { get; init; }

      public OrderReleasedEvent(int auftragsNummer, string eisensorteId, double zielGewichtKg)
      {
         AuftragsNummer = auftragsNummer;
         EisensorteId = eisensorteId;
         ZielGewichtKg = zielGewichtKg;
      }
   }

   /// <summary>
   /// Wird vom DB-Poller (oder einer API) ausgelöst, wenn der Bediener den aktuell laufenden 
   /// Auftrag über die IROPA-Weboberfläche hart abgebrochen hat.
   /// </summary>
   public sealed class OrderCancelledEvent : FalcomEventBase
   {
      public override string Source => "Database-Poller";

      // Ein Abbruch unterbricht die State Machine sofort und zwingt sie zurück in den Idle-Modus
      public override bool IsStateTrigger => true;

      public int AuftragsNummer { get; init; }
      public string Grund { get; init; }

      public OrderCancelledEvent(int auftragsNummer, string grund = "Manuell durch Bediener")
      {
         AuftragsNummer = auftragsNummer;
         Grund = grund;
      }
   }

   /// <summary>
   /// Wird ausgelöst, wenn der Bediener im "Misstrauens-Modus" (Sperrzustand bei Gewichtsabweichung) 
   /// eine manuelle Korrektur oder Materialdeklaration über die Webseite übermittelt hat.
   /// </summary>
   public sealed class OrderCorrectionSubmittedEvent : FalcomEventBase
   {
      public override string Source => "Database-Poller";

      // Löst den Sperrzustand auf und bringt die State Machine zurück in den normalen Ablauf
      public override bool IsStateTrigger => true;

      public int AuftragsNummer { get; init; }
      public string DeklarierteQuelle { get; init; } // z.B. "Box 4"
      public double KorrekturGewicht { get; init; }  // Die manuell reingekippten kg

      public OrderCorrectionSubmittedEvent(int auftragsNummer, string deklarierteQuelle, double korrekturGewicht)
      {
         AuftragsNummer = auftragsNummer;
         DeklarierteQuelle = deklarierteQuelle;
         KorrekturGewicht = korrekturGewicht;
      }
   }
}