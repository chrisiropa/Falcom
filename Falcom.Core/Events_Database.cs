using System;

namespace Falcom
{
   /// <summary>
   /// Wird vom DB-Poller ausgelöst, wenn ein neuer Chargierauftrag vom Bediener freigegeben wurde
   /// und aktuell kein anderer Auftrag aktiv in Arbeit ist.
   /// </summary>
   public sealed class OrderReleasedEvent : FalcomEventBase
   {
      public override string Source => "Database-Poller";
      public override bool IsStateTrigger => true;

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
   /// Wird vom DB-Poller ausgelöst, wenn aus Sicht der Datenbank eine neue Kranfahrt ansteht.
   /// Die konkrete Fahrt wird danach zentral über FALCOM_TryCreateNextAktuelleFahrt erzeugt.
   /// </summary>
   public sealed class NextKranfahrtAvailableEvent : FalcomEventBase
   {
      public override string Source => "Database-Poller";
      public override bool IsStateTrigger => true;

      public string AuftragsTyp { get; init; }
      public long? AuftragID { get; init; }
      public string Reason { get; init; }

      public NextKranfahrtAvailableEvent(string auftragsTyp, long? auftragID, string reason)
      {
         AuftragsTyp = auftragsTyp;
         AuftragID = auftragID;
         Reason = reason;
      }
   }

   /// <summary>
   /// Wird vom DB-Poller (oder einer API) ausgelöst, wenn der Bediener den aktuell laufenden 
   /// Auftrag über die IROPA-Weboberfläche hart abgebrochen hat.
   /// </summary>
   public sealed class OrderCancelledEvent : FalcomEventBase
   {
      public override string Source => "Database-Poller";
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
      public override bool IsStateTrigger => true;

      public int AuftragsNummer { get; init; }
      public string DeklarierteQuelle { get; init; }
      public double KorrekturGewicht { get; init; }

      public OrderCorrectionSubmittedEvent(int auftragsNummer, string deklarierteQuelle, double korrekturGewicht)
      {
         AuftragsNummer = auftragsNummer;
         DeklarierteQuelle = deklarierteQuelle;
         KorrekturGewicht = korrekturGewicht;
      }
   }
}
