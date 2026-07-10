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
   private long lastLogChangeVersion;

   public MainWindow(
      FalcomRuntimeStatus runtimeStatus,
      FalcomUiLogSink uiLogSink)
   {
      this.runtimeStatus = runtimeStatus;
      this.uiLogSink = uiLogSink;

      InitializeComponent();

      refreshTimer.Interval = TimeSpan.FromMilliseconds(200);
      refreshTimer.Tick += (_, _) => RefreshView();
      refreshTimer.Start();

      RefreshView();
   }

   private void RefreshView()
   {
      FalcomRuntimeStatusSnapshot snapshot = runtimeStatus.Snapshot();
      DateTime now = DateTime.Now;

      LastRefreshText.Text = $"Aktualisiert: {now:dd.MM.yyyy HH:mm:ss}";

      bool opcStatusFresh = snapshot.OpcKranSpsStatusZeit is not null
                            && now - snapshot.OpcKranSpsStatusZeit.Value < TimeSpan.FromSeconds(10);
      OpcLamp.Fill = snapshot.OpcKranSpsVerbunden && opcStatusFresh
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
      SpsLifeLamp.Fill = snapshot.SpsLebensZaehlerGueltig && spsLifeFresh ? Brushes.LimeGreen : Brushes.Firebrick;
      SpsLifeValueText.Text = snapshot.LetzterSpsLebensZaehler?.ToString() ?? "-";
      SpsLifeTimeText.Text = $"{snapshot.SpsLebensZaehlerStatusText} {FormatTimestamp(snapshot.LetzterSpsLebensZaehlerEmpfangenAm)}";

      CurrentRideText.Text = snapshot.AktuelleFahrtID is null
         ? "Keine Fahrt"
         : $"#{snapshot.AktuelleFahrtID} / Auftrag {snapshot.AktuellerAuftragID}";
      CurrentRideDetailText.Text = snapshot.AktuelleFahrtID is null
         ? $"Letzte Prüfung auf freigegebenen Auftrag: {FormatTimestamp(snapshot.LetzteFreigabePruefungAm)}"
         : $"{snapshot.AktuellerAuftragsTyp}: {snapshot.AktuelleQuelle} → {snapshot.AktuellesZiel}, Soll {snapshot.AktuelleSollMengeKg:0.###} kg";

      RefreshLogs();
   }

   private void RefreshLogs()
   {
      long changeVersion = uiLogSink.ChangeVersion;

      if (changeVersion == lastLogChangeVersion)
      {
         return;
      }

      lastLogChangeVersion = changeVersion;

      LogList.ItemsSource = uiLogSink.SnapshotAblauf()
         .Select(entry => entry.Line)
         .ToList();
      ScrollToLastItem(LogList);

      OpcSendList.ItemsSource = uiLogSink.SnapshotOpcSend()
         .Select(entry => entry.Line)
         .ToList();
      ScrollToLastItem(OpcSendList);

      OpcReceiveList.ItemsSource = uiLogSink.SnapshotOpcReceive()
         .Select(entry => entry.Line)
         .ToList();
      ScrollToLastItem(OpcReceiveList);
   }

   private static string FormatTimestamp(DateTime? timestamp)
   {
      return timestamp is null
         ? string.Empty
         : timestamp.Value.ToString("dd.MM.yyyy HH:mm:ss");
   }

   private static void ScrollToLastItem(System.Windows.Controls.ListBox listBox)
   {
      if (listBox.Items.Count == 0)
      {
         return;
      }

      listBox.ScrollIntoView(listBox.Items[^1]);
   }
}
