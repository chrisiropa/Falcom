using Falcom;
using Microsoft.Extensions.Logging;
using Opc.UaFx.Client;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace EOfenSimulator;

public partial class MainWindow : Window
{
   private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);
   private static readonly TimeSpan ReconnectLogThrottle = TimeSpan.FromMinutes(1);

   private readonly FalcomUiLogSink uiLogSink = new();
   private readonly FalcomFileSink fileLogSink;
   private readonly DispatcherTimer logRefreshTimer = new();
   private readonly DispatcherTimer statusRefreshTimer = new();
   private readonly CancellationTokenSource reconnectCancellation = new();
   private readonly string opcEndpoint;
   private readonly IReadOnlyList<EventConfiguration> events;
   private readonly IReadOnlyList<EventMappingConfiguration> zuordnungen;

   private OpcClient? opcClient;
   private int reconnectLoopRunning;
   private bool disposed;
   private DateTime nextReconnectLogUtc = DateTime.MinValue;
   private long lastLogChangeVersion;
   private string opcStatusText = "Initialisierung";
   private string opcStatusDetailText = string.Empty;

   public MainWindow()
   {
      InitializeComponent();

      logRefreshTimer.Interval = TimeSpan.FromMilliseconds(250);
      logRefreshTimer.Tick += (_, _) => RefreshLogs();
      logRefreshTimer.Start();

      statusRefreshTimer.Interval = TimeSpan.FromSeconds(1);
      statusRefreshTimer.Tick += (_, _) => RefreshStatusView();
      statusRefreshTimer.Start();

      SimulatorConfiguration configuration = SimulatorConfig.Load();
      opcEndpoint = configuration.OpcEndpoint.Trim();
      events = configuration.Events;
      zuordnungen = configuration.Zuordnungen;
      fileLogSink = new FalcomFileSink(configuration.LogfilePath);
      ProgramStartBanner.WriteToLogfile(
         fileLogSink,
         "EOfenSimulator",
         "E-OFEN-SIMULATOR PROGRAMMSTART");

      CreateClientInstance();

      Log($"0601|Logdatei aktiv: {configuration.LogfilePath}");
      Log($"0602|OPC Endpoint: {opcEndpoint}");
      Log($"0603|Event-Konfiguration geladen: Events={events.Count}, Nodes={events.Sum(x => x.Nodes.Count)}, Zuordnungen={zuordnungen.Count}.");

      foreach (EventConfiguration eventConfiguration in events)
      {
         Log($"0604|{eventConfiguration.EventName} {eventConfiguration.Direction}: Nodes={eventConfiguration.Nodes.Count}.");
      }

      RefreshEventView();
      RefreshLogs();
      RefreshStatusView();
      StartBackgroundReconnectLoop("Programmstart");
   }

   private void CreateClientInstance()
   {
      if (opcClient is not null)
      {
         opcClient.StateChanged -= OnClientStateChanged;
      }

      opcClient = new OpcClient(opcEndpoint)
      {
         OperationTimeout = 5_000,
         SessionTimeout = 5_000,
         ReconnectTimeout = 5_000
      };
      opcClient.StateChanged += OnClientStateChanged;
   }

   private void StartBackgroundReconnectLoop(string reason)
   {
      if (disposed || reconnectCancellation.IsCancellationRequested)
      {
         return;
      }

      if (Interlocked.CompareExchange(ref reconnectLoopRunning, 1, 0) != 0)
      {
         return;
      }

      SetOpcReconnectFromBackground("Reconnect laeuft");
      LogWarning($"0605|OPC-Hintergrund-Reconnect wird gestartet. Grund={reason}.");

      _ = Task.Run(
         async () =>
         {
            int attempt = 0;
            CancellationToken cancellationToken = reconnectCancellation.Token;

            try
            {
               while (!cancellationToken.IsCancellationRequested)
               {
                  attempt++;

                  try
                  {
                     if (opcClient is { State: OpcClientState.Connected })
                     {
                        SetOpcConnectedFromBackground("Verbunden");
                        Log($"0606|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
                        return;
                     }

                     opcClient?.Disconnect();
                     CreateClientInstance();
                     opcClient?.Connect();

                     if (opcClient is { State: OpcClientState.Connected })
                     {
                        SetOpcConnectedFromBackground("Verbunden");
                        Log($"0607|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
                        return;
                     }
                  }
                  catch (Exception ex)
                  {
                     SetOpcReconnectFromBackground("Reconnect laeuft");

                     if (DateTime.UtcNow >= nextReconnectLogUtc)
                     {
                        LogWarning($"0608|OPC-Hintergrund-Reconnect Versuch={attempt} noch nicht erfolgreich. Naechster Versuch in {ConnectRetryDelay.TotalSeconds:0} Sekunden. Fehler={ex.GetType().Name}: {ex.Message}");
                        nextReconnectLogUtc = DateTime.UtcNow.Add(ReconnectLogThrottle);
                     }
                  }

                  await Task.Delay(ConnectRetryDelay, cancellationToken);
               }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
               Interlocked.Exchange(ref reconnectLoopRunning, 0);
            }
         },
         reconnectCancellation.Token);
   }

   private void OnClientStateChanged(object? sender, OpcClientStateChangedEventArgs e)
   {
      Dispatcher.BeginInvoke(() =>
      {
         if (e.NewState == OpcClientState.Connected)
         {
            SetOpcConnected("Verbunden");
            Log("0609|OPC Client meldet Connected.");
         }
         else if (e.NewState == OpcClientState.Disconnected)
         {
            SetOpcDisconnected("Getrennt");
            LogWarning("0610|OPC Client meldet Disconnected.");
            StartBackgroundReconnectLoop("OPC Client meldet Disconnected");
         }
         else if (e.NewState == OpcClientState.Reconnecting)
         {
            SetOpcReconnect("Reconnect laeuft");
            Log("0611|OPC Client meldet Reconnecting.");
            StartBackgroundReconnectLoop("OPC Client meldet Reconnecting");
         }
      });
   }

   private void SetOpcConnected(string detail)
   {
      opcStatusText = "Verbunden";
      opcStatusDetailText = detail;
      OpcLamp.Fill = Brushes.LimeGreen;
      OpcStatusText.Text = opcStatusText;
      OpcStatusDetailText.Text = $"{detail} | {opcEndpoint}";
      SimulationStatusText.Text = "Bereit";
   }

   private void SetOpcDisconnected(string detail)
   {
      opcStatusText = "Getrennt";
      opcStatusDetailText = detail;
      OpcLamp.Fill = Brushes.Firebrick;
      OpcStatusText.Text = opcStatusText;
      OpcStatusDetailText.Text = $"{detail} | {opcEndpoint}";
      SimulationStatusText.Text = "OPC getrennt";
   }

   private void SetOpcReconnect(string detail)
   {
      opcStatusText = "Reconnect";
      opcStatusDetailText = detail;
      OpcLamp.Fill = Brushes.Goldenrod;
      OpcStatusText.Text = opcStatusText;
      OpcStatusDetailText.Text = $"{detail} | {opcEndpoint}";
      SimulationStatusText.Text = "Reconnect laeuft";
   }

   private void SetOpcConnectedFromBackground(string detail)
   {
      Dispatcher.BeginInvoke(() => SetOpcConnected(detail));
   }

   private void SetOpcReconnectFromBackground(string detail)
   {
      Dispatcher.BeginInvoke(() => SetOpcReconnect(detail));
   }

   private void RefreshStatusView()
   {
      LastRefreshText.Text = $"Aktualisiert: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
      OpcStatusText.Text = opcStatusText;
      OpcStatusDetailText.Text = string.IsNullOrWhiteSpace(opcStatusDetailText)
         ? opcEndpoint
         : $"{opcStatusDetailText} | {opcEndpoint}";
      EventCountText.Text = $"{events.Count} Events";
      EventDetailText.Text = $"{events.Sum(x => x.Nodes.Count)} Nodes, {zuordnungen.Count} Zuordnungen";
   }

   private void RefreshEventView()
   {
      EventList.ItemsSource = events
         .SelectMany(e => e.Nodes
            .Select(n => $"{e.EventName,-10} {e.Direction,-16} {n.NodeRole,-8} {n.NodeName,-34} {n.OpcNode}"))
         .ToList();
   }

   private void RefreshLogs()
   {
      if (lastLogChangeVersion == uiLogSink.ChangeVersion)
      {
         return;
      }

      lastLogChangeVersion = uiLogSink.ChangeVersion;
      AblaufLogList.ItemsSource = uiLogSink.SnapshotAblauf()
         .Select(entry => entry.Line)
         .ToList();

      if (AblaufLogList.Items.Count > 0)
      {
         AblaufLogList.ScrollIntoView(AblaufLogList.Items[^1]);
      }
   }

   private void Log(string message)
   {
      WriteLog(LogLevel.Information, message);
   }

   private void LogWarning(string message)
   {
      WriteLog(LogLevel.Warning, message);
   }

   private void WriteLog(LogLevel level, string message)
   {
      string levelText = level switch
      {
         LogLevel.Warning => "WRN",
         LogLevel.Error => "ERR",
         _ => "INF"
      };
      string line = $"{DateTime.Now:dd.MM.yy HH:mm:ss.fff} [{levelText}] {message}";
      fileLogSink.Write(line);
      uiLogSink.Write(level, line);
   }

   protected override void OnClosed(EventArgs e)
   {
      disposed = true;
      reconnectCancellation.Cancel();
      opcClient?.Disconnect();
      opcClient?.Dispose();
      reconnectCancellation.Dispose();
      fileLogSink.Dispose();
      base.OnClosed(e);
   }
}
