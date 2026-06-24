using Falcom;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace FalcomWpf;

public partial class MainWindow : Window
{
   private readonly FalcomRuntimeStatus runtimeStatus;
   private readonly FalcomUiLogSink uiLogSink;
   private readonly DispatcherTimer refreshTimer = new();
   private int lastLogCount;

   public MainWindow(
      FalcomRuntimeStatus runtimeStatus,
      FalcomUiLogSink uiLogSink)
   {
      this.runtimeStatus = runtimeStatus;
      this.uiLogSink = uiLogSink;

      InitializeComponent();

      refreshTimer.Interval = TimeSpan.FromSeconds(1);
      refreshTimer.Tick += (_, _) => RefreshView();
      refreshTimer.Start();

      RefreshView();
   }

   private void RefreshView()
   {
      FalcomRuntimeStatusSnapshot snapshot = runtimeStatus.Snapshot();
      DateTime now = DateTime.Now;

      LastRefreshText.Text = $"Aktualisiert: {now:dd.MM.yyyy HH:mm:ss}";

      OpcLamp.Fill = snapshot.OpcKranSpsVerbunden
         ? Brushes.LimeGreen
         : Brushes.Firebrick;
      OpcStatusText.Text = snapshot.OpcKranSpsStatusText;
      OpcStatusTimeText.Text = FormatTimestamp(snapshot.OpcKranSpsStatusZeit);

      bool watchdogFresh = snapshot.LetzterWatchdogGesendetAm is not null
                            && now - snapshot.LetzterWatchdogGesendetAm.Value < TimeSpan.FromSeconds(5);
      WatchdogLamp.Fill = watchdogFresh ? Brushes.LimeGreen : Brushes.Firebrick;
      WatchdogValueText.Text = snapshot.LetzterWatchdogWert?.ToString() ?? "-";
      WatchdogTimeText.Text = $"{snapshot.WatchdogStatusText} {FormatTimestamp(snapshot.LetzterWatchdogGesendetAm)}";

      bool spsLifeFresh = snapshot.LetzterSpsLebensZaehlerEmpfangenAm is not null
                          && now - snapshot.LetzterSpsLebensZaehlerEmpfangenAm.Value < TimeSpan.FromSeconds(5);
      SpsLifeLamp.Fill = spsLifeFresh ? Brushes.LimeGreen : Brushes.Firebrick;
      SpsLifeValueText.Text = snapshot.LetzterSpsLebensZaehler?.ToString() ?? "-";
      SpsLifeTimeText.Text = $"{snapshot.SpsLebensZaehlerStatusText} {FormatTimestamp(snapshot.LetzterSpsLebensZaehlerEmpfangenAm)}";

      CurrentRideText.Text = snapshot.AktuelleFahrtID is null
         ? "Keine Fahrt"
         : $"#{snapshot.AktuelleFahrtID} / Auftrag {snapshot.AktuellerAuftragID}";
      CurrentRideDetailText.Text = snapshot.AktuelleFahrtID is null
         ? string.Empty
         : $"{snapshot.AktuellerAuftragsTyp}: {snapshot.AktuelleQuelle} → {snapshot.AktuellesZiel}, Soll {snapshot.AktuelleSollMengeKg:0.###} kg";

      RefreshLogs();
   }

   private void RefreshLogs()
   {
      IReadOnlyList<FalcomUiLogEntry> entries = uiLogSink.Snapshot();

      if (entries.Count == lastLogCount)
      {
         return;
      }

      lastLogCount = entries.Count;

      LogList.ItemsSource = entries
         .Select(entry => entry.Line)
         .Reverse()
         .ToList();

      OpcSendList.ItemsSource = entries
         .Where(entry =>
            entry.Line.Contains("Watchdog", StringComparison.OrdinalIgnoreCase)
            || entry.Line.Contains("KranfahrtAuftrag", StringComparison.OrdinalIgnoreCase)
            || entry.Line.Contains("0047|", StringComparison.OrdinalIgnoreCase)
            || entry.Line.Contains("003E|", StringComparison.OrdinalIgnoreCase))
         .Select(entry => entry.Line)
         .Reverse()
         .ToList();

      OpcReceiveList.ItemsSource = entries
         .Where(entry =>
            entry.Line.Contains("LebensZaehler", StringComparison.OrdinalIgnoreCase)
            || entry.Line.Contains("KranfahrtBeendet", StringComparison.OrdinalIgnoreCase)
            || entry.Line.Contains("004E|", StringComparison.OrdinalIgnoreCase)
            || entry.Line.Contains("0026|", StringComparison.OrdinalIgnoreCase))
         .Select(entry => entry.Line)
         .Reverse()
         .ToList();
   }

   private static string FormatTimestamp(DateTime? timestamp)
   {
      return timestamp is null
         ? string.Empty
         : timestamp.Value.ToString("dd.MM.yyyy HH:mm:ss");
   }
}
