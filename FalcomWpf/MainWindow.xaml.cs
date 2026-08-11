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

      bool opcStatusFresh = IsFresh(snapshot.OpcKranSpsStatusZeit, now, TimeSpan.FromSeconds(10));
      OpcLamp.Fill = LampBrush(snapshot.OpcKranSpsVerbunden && opcStatusFresh);
      OpcStatusText.Text = snapshot.OpcKranSpsStatusText;
      OpcStatusTimeText.Text = FormatTimestamp(snapshot.OpcKranSpsStatusZeit);

      bool cwStatusFresh = IsFresh(snapshot.OpcCwSpsStatusZeit, now, TimeSpan.FromSeconds(10))
                           || IsFresh(snapshot.LetzterCwLebensZaehlerEmpfangenAm, now, TimeSpan.FromSeconds(10));
      CwOpcLamp.Fill = LampBrush(snapshot.OpcCwSpsVerbunden && cwStatusFresh);
      CwOpcStatusText.Text = snapshot.CwLebensZaehlerGueltig
         ? snapshot.LetzterCwLebensZaehler?.ToString() ?? "-"
         : snapshot.OpcCwSpsStatusText;
      CwOpcStatusTimeText.Text = snapshot.CwLebensZaehlerGueltig
         ? $"{snapshot.CwLebensZaehlerStatusText} {FormatTimestamp(snapshot.LetzterCwLebensZaehlerEmpfangenAm)}"
         : FormatTimestamp(snapshot.OpcCwSpsStatusZeit);

      bool eOfenStatusFresh = IsFresh(snapshot.OpcEOfenSpsStatusZeit, now, TimeSpan.FromSeconds(10))
                              || IsFresh(snapshot.LetzterEOfenLebensZaehlerEmpfangenAm, now, TimeSpan.FromSeconds(10));
      EOfenOpcLamp.Fill = LampBrush(snapshot.OpcEOfenSpsVerbunden && eOfenStatusFresh);
      EOfenOpcStatusText.Text = snapshot.EOfenLebensZaehlerGueltig
         ? snapshot.LetzterEOfenLebensZaehler?.ToString() ?? "-"
         : snapshot.OpcEOfenSpsStatusText;
      EOfenOpcStatusTimeText.Text = snapshot.EOfenLebensZaehlerGueltig
         ? $"{snapshot.EOfenLebensZaehlerStatusText} {FormatTimestamp(snapshot.LetzterEOfenLebensZaehlerEmpfangenAm)}"
         : FormatTimestamp(snapshot.OpcEOfenSpsStatusZeit);

      bool watchdogFresh = IsFresh(snapshot.LetzterWatchdogGesendetAm, now, TimeSpan.FromSeconds(5));
      WatchdogLamp.Fill = watchdogFresh ? Brushes.LimeGreen : Brushes.Firebrick;
      WatchdogValueText.Text = snapshot.LetzterWatchdogWert?.ToString() ?? "-";
      WatchdogTimeText.Text = $"{snapshot.WatchdogStatusText} {FormatTimestamp(snapshot.LetzterWatchdogGesendetAm)}";

      bool spsLifeFresh = IsFresh(snapshot.LetzterSpsLebensZaehlerEmpfangenAm, now, TimeSpan.FromSeconds(5));
      SpsLifeLamp.Fill = LampBrush(snapshot.SpsLebensZaehlerGueltig && spsLifeFresh);
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
         .Select(ToLogDisplayEntry)
         .ToList();
      ScrollToLastItem(LogList);

      OpcSendList.ItemsSource = uiLogSink.SnapshotOpcSend()
         .Select(ToLogDisplayEntry)
         .ToList();
      ScrollToLastItem(OpcSendList);

      OpcReceiveList.ItemsSource = uiLogSink.SnapshotOpcReceive()
         .Select(ToLogDisplayEntry)
         .ToList();
      ScrollToLastItem(OpcReceiveList);
   }

   private static LogDisplayEntry ToLogDisplayEntry(FalcomUiLogEntry entry)
   {
      return new LogDisplayEntry(entry.Line, LogBrush(entry.LogLevel));
   }

   private static Brush LogBrush(LogLevel logLevel)
   {
      return logLevel switch
      {
         LogLevel.Warning => Brushes.Gold,
         LogLevel.Error => Brushes.OrangeRed,
         LogLevel.Critical => Brushes.Magenta,
         LogLevel.Debug => Brushes.DeepSkyBlue,
         LogLevel.Trace => Brushes.Gray,
         _ => Brushes.Gainsboro
      };
   }

   private static string FormatTimestamp(DateTime? timestamp)
   {
      return timestamp is null
         ? string.Empty
         : timestamp.Value.ToString("dd.MM.yyyy HH:mm:ss");
   }

   private static bool IsFresh(DateTime? timestamp, DateTime now, TimeSpan maxAge)
   {
      return timestamp is not null && now - timestamp.Value < maxAge;
   }

   private static Brush LampBrush(bool ok)
   {
      return ok ? Brushes.LimeGreen : Brushes.Firebrick;
   }

   private static void ScrollToLastItem(System.Windows.Controls.ListBox listBox)
   {
      if (listBox.Items.Count == 0)
      {
         return;
      }

      listBox.ScrollIntoView(listBox.Items[^1]);
   }

   private sealed record LogDisplayEntry(string Line, Brush Foreground);
}
