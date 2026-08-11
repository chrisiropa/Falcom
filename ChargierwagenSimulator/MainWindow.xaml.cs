using Falcom;
using Microsoft.Extensions.Logging;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChargierwagenSimulator;

public partial class MainWindow : Window
{
   private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);
   private static readonly TimeSpan ReconnectLogThrottle = TimeSpan.FromMinutes(1);
   private const int SimulatedIstgewChW1 = 1000;
   private const int SimulatedIstgewChW2 = 2000;
   private const int SimulatedIstgewChW3 = 3000;

   private readonly FalcomUiLogSink uiLogSink = new();
   private readonly FalcomFileSink fileLogSink;
   private readonly DispatcherTimer logRefreshTimer = new();
   private readonly DispatcherTimer statusRefreshTimer = new();
   private readonly DispatcherTimer event402Timer = new();
   private readonly DispatcherTimer event403Timer = new();
   private readonly DispatcherTimer event404Timer = new();
   private readonly DispatcherTimer event405Timer = new();
   private readonly CancellationTokenSource reconnectCancellation = new();
   private readonly string opcEndpoint;
   private readonly IReadOnlyList<EventConfiguration> events;
   private readonly IReadOnlyList<EventMappingConfiguration> zuordnungen;

   private OpcClient? opcClient;
   private int reconnectLoopRunning;
   private bool disposed;
   private DateTime nextReconnectLogUtc = DateTime.MinValue;
   private long lastLogChangeVersion;
   private int event402Counter;
   private int event402SendFailures;
   private int event403Counter;
   private int event403SendFailures;
   private int event404Counter;
   private int event404SendFailures;
   private int event405Counter;
   private int event405SendFailures;
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

      event402Timer.Interval = TimeSpan.FromSeconds(1);
      event402Timer.Tick += (_, _) => SendEvent402Status();
      event402Timer.Start();

      event403Timer.Interval = TimeSpan.FromSeconds(1);
      event403Timer.Tick += (_, _) => SendEvent403Beladebereit();
      event403Timer.Start();

      event404Timer.Interval = TimeSpan.FromSeconds(1);
      event404Timer.Tick += (_, _) => SendEvent404Steuerung();
      event404Timer.Start();

      event405Timer.Interval = TimeSpan.FromSeconds(1);
      event405Timer.Tick += (_, _) => SendEvent405GattierungAbgeschlossen();
      event405Timer.Start();

      SimulatorConfiguration configuration = SimulatorConfig.Load();
      opcEndpoint = configuration.OpcEndpoint.Trim();
      events = configuration.Events;
      zuordnungen = configuration.Zuordnungen;
      fileLogSink = new FalcomFileSink(configuration.LogfilePath);
      ProgramStartBanner.WriteToLogfile(
         fileLogSink,
         "ChargierwagenSimulator",
         "CHARGIERWAGEN-SIMULATOR PROGRAMMSTART");

      CreateClientInstance();

      Log($"0701|Logdatei aktiv: {configuration.LogfilePath}");
      Log($"0702|OPC Endpoint: {opcEndpoint}");
      Log($"0703|Event-Konfiguration geladen: Events={events.Count}, Nodes={events.Sum(x => x.Nodes.Count)}, Zuordnungen={zuordnungen.Count}.");

      foreach (EventConfiguration eventConfiguration in events)
      {
         Log($"0704|{eventConfiguration.EventName} {eventConfiguration.Direction}: Nodes={eventConfiguration.Nodes.Count}.");
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
      LogWarning($"0705|OPC-Hintergrund-Reconnect wird gestartet. Grund={reason}.");

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
                        Log($"0706|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
                        return;
                     }

                     opcClient?.Disconnect();
                     CreateClientInstance();
                     opcClient?.Connect();

                     if (opcClient is { State: OpcClientState.Connected })
                     {
                        SetOpcConnectedFromBackground("Verbunden");
                        Log($"0707|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
                        return;
                     }
                  }
                  catch (Exception ex)
                  {
                     SetOpcReconnectFromBackground("Reconnect laeuft");

                     if (DateTime.UtcNow >= nextReconnectLogUtc)
                     {
                        LogWarning($"0708|OPC-Hintergrund-Reconnect Versuch={attempt} noch nicht erfolgreich. Naechster Versuch in {ConnectRetryDelay.TotalSeconds:0} Sekunden. Fehler={ex.GetType().Name}: {ex.Message}");
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
            Log("0709|OPC Client meldet Connected.");
         }
         else if (e.NewState == OpcClientState.Disconnected)
         {
            SetOpcDisconnected("Getrennt");
            LogWarning("0710|OPC Client meldet Disconnected.");
            StartBackgroundReconnectLoop("OPC Client meldet Disconnected");
         }
         else if (e.NewState == OpcClientState.Reconnecting)
         {
            SetOpcReconnect("Reconnect laeuft");
            Log("0711|OPC Client meldet Reconnecting.");
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

   private void SendEvent402Status()
   {
      if (opcClient is not { State: OpcClientState.Connected })
      {
         return;
      }

      EventConfiguration? event402 = events.FirstOrDefault(e =>
         e.ID == 402
         || string.Equals(e.EventName, "Event_402", StringComparison.OrdinalIgnoreCase));
      if (event402 is null)
      {
         return;
      }

      EventNodeConfiguration? triggerNode = event402.Nodes.FirstOrDefault(n =>
         string.Equals(n.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
         || string.Equals(n.NodeName, "Event_402", StringComparison.OrdinalIgnoreCase));
      if (triggerNode is null || string.IsNullOrWhiteSpace(triggerNode.OpcNode))
      {
         return;
      }

      try
      {
         int nextValue = event402Counter == int.MaxValue
            ? 1
            : event402Counter + 1;

         WriteEvent402PayloadNode(event402, "Istgew_ChW1", SimulatedIstgewChW1);
         WriteEvent402PayloadNode(event402, "Istgew_ChW2", SimulatedIstgewChW2);
         WriteEvent402PayloadNode(event402, "Istgew_ChW3", SimulatedIstgewChW3);
         WriteNode(triggerNode.OpcNode, nextValue);

         event402Counter = nextValue;
         event402SendFailures = 0;
         SimulationDetailText.Text = $"Event_402 sekündlich aktiv. Letzter Zaehler={event402Counter}.";

         if (event402Counter == 1 || event402Counter % 60 == 0)
         {
            Log($"0712|Event_402 Status gesendet. Zaehler={event402Counter}, Istgew_ChW1={SimulatedIstgewChW1}, Istgew_ChW2={SimulatedIstgewChW2}, Istgew_ChW3={SimulatedIstgewChW3}.");
         }
      }
      catch (Exception ex)
      {
         event402SendFailures++;
         SimulationDetailText.Text = $"Event_402 Sendefehler ({event402SendFailures}).";

         if (event402SendFailures == 1 || event402SendFailures % 60 == 0)
         {
            LogWarning($"0713|Event_402 konnte nicht geschrieben werden. Fehler={ex.GetType().Name}: {ex.Message}");
         }
      }
   }

   private void WriteEvent402PayloadNode(
      EventConfiguration event402,
      string nodeName,
      object value)
   {
      EventNodeConfiguration? node = event402.Nodes.FirstOrDefault(n =>
         string.Equals(n.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
      if (node is null || string.IsNullOrWhiteSpace(node.OpcNode))
      {
         return;
      }

      WriteNode(node.OpcNode, value);
   }

   private void SendEvent403Beladebereit()
   {
      if (opcClient is not { State: OpcClientState.Connected })
      {
         return;
      }

      EventConfiguration? event403 = events.FirstOrDefault(e =>
         e.ID == 403
         || string.Equals(e.EventName, "Event_403", StringComparison.OrdinalIgnoreCase));
      if (event403 is null)
      {
         return;
      }

      EventNodeConfiguration? triggerNode = event403.Nodes.FirstOrDefault(n =>
         string.Equals(n.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
         || string.Equals(n.NodeName, "Event_403", StringComparison.OrdinalIgnoreCase));
      if (triggerNode is null || string.IsNullOrWhiteSpace(triggerNode.OpcNode))
      {
         return;
      }

      try
      {
         int nextValue = event403Counter == int.MaxValue
            ? 1
            : event403Counter + 1;
         bool chw1 = true;
         bool chw2 = nextValue % 2 == 0;
         bool chw3 = nextValue % 3 == 0;

         WriteEventPayloadNode(event403, "Beladebereit_ChW1", chw1);
         WriteEventPayloadNode(event403, "Beladebereit_ChW2", chw2);
         WriteEventPayloadNode(event403, "Beladebereit_ChW3", chw3);
         WriteNode(triggerNode.OpcNode, nextValue);

         event403Counter = nextValue;
         event403SendFailures = 0;

         if (event403Counter == 1 || event403Counter % 60 == 0)
         {
            Log($"0714|Event_403 Beladebereit gesendet. Zaehler={event403Counter}, Beladebereit_ChW1={chw1}, Beladebereit_ChW2={chw2}, Beladebereit_ChW3={chw3}.");
         }
      }
      catch (Exception ex)
      {
         event403SendFailures++;

         if (event403SendFailures == 1 || event403SendFailures % 60 == 0)
         {
            LogWarning($"0715|Event_403 konnte nicht geschrieben werden. Fehler={ex.GetType().Name}: {ex.Message}");
         }
      }
   }

   private void SendEvent404Steuerung()
   {
      if (opcClient is not { State: OpcClientState.Connected })
      {
         return;
      }

      EventConfiguration? event404 = events.FirstOrDefault(e =>
         e.ID == 404
         || string.Equals(e.EventName, "Event_404", StringComparison.OrdinalIgnoreCase));
      if (event404 is null)
      {
         return;
      }

      EventNodeConfiguration? triggerNode = event404.Nodes.FirstOrDefault(n =>
         string.Equals(n.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
         || string.Equals(n.NodeName, "Event_404", StringComparison.OrdinalIgnoreCase));
      if (triggerNode is null || string.IsNullOrWhiteSpace(triggerNode.OpcNode))
      {
         return;
      }

      try
      {
         int nextValue = event404Counter == int.MaxValue
            ? 1
            : event404Counter + 1;
         bool chw1 = nextValue % 2 == 1;
         bool chw2 = true;
         bool chw3 = nextValue % 4 == 0;

         WriteEventPayloadNode(event404, "Stoerung_ChW1", chw1);
         WriteEventPayloadNode(event404, "Stoerung_ChW2", chw2);
         WriteEventPayloadNode(event404, "Stoerung_ChW3", chw3);
         WriteNode(triggerNode.OpcNode, nextValue);

         event404Counter = nextValue;
         event404SendFailures = 0;

         if (event404Counter == 1 || event404Counter % 60 == 0)
         {
            Log($"0716|Event_404 Stoerung gesendet. Zaehler={event404Counter}, Stoerung_ChW1={chw1}, Stoerung_ChW2={chw2}, Stoerung_ChW3={chw3}.");
         }
      }
      catch (Exception ex)
      {
         event404SendFailures++;

         if (event404SendFailures == 1 || event404SendFailures % 60 == 0)
         {
            LogWarning($"0717|Event_404 konnte nicht geschrieben werden. Fehler={ex.GetType().Name}: {ex.Message}");
         }
      }
   }

   private void SendEvent405GattierungAbgeschlossen()
   {
      if (opcClient is not { State: OpcClientState.Connected })
      {
         return;
      }

      EventConfiguration? event405 = events.FirstOrDefault(e =>
         e.ID == 405
         || string.Equals(e.EventName, "Event_405", StringComparison.OrdinalIgnoreCase));
      if (event405 is null)
      {
         return;
      }

      EventNodeConfiguration? triggerNode = event405.Nodes.FirstOrDefault(n =>
         string.Equals(n.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
         || string.Equals(n.NodeName, "Event_405", StringComparison.OrdinalIgnoreCase));
      if (triggerNode is null || string.IsNullOrWhiteSpace(triggerNode.OpcNode))
      {
         return;
      }

      try
      {
         int nextValue = event405Counter == int.MaxValue
            ? 1
            : event405Counter + 1;

         WriteEventPayloadNode(event405, "GattierungAbgeschl", true);
         WriteEventPayloadNode(event405, "C", 0.02f + (nextValue % 10) / 1000f);
         WriteEventPayloadNode(event405, "Si", 0.05f + (nextValue % 10) / 1000f);
         WriteEventPayloadNode(event405, "MN", 0.30f + (nextValue % 10) / 1000f);
         WriteEventPayloadNode(event405, "Cu", 0.04f + (nextValue % 10) / 1000f);
         WriteEventPayloadNode(event405, "ChW_ID", 1);
         WriteEventPayloadNode(event405, "AuftragsNr", 100000 + nextValue);
         WriteNode(triggerNode.OpcNode, nextValue);

         event405Counter = nextValue;
         event405SendFailures = 0;

         if (event405Counter == 1 || event405Counter % 60 == 0)
         {
            Log($"0718|Event_405 Gattierung abgeschlossen gesendet. Zaehler={event405Counter}, Auftrag={100000 + nextValue}.");
         }
      }
      catch (Exception ex)
      {
         event405SendFailures++;

         if (event405SendFailures == 1 || event405SendFailures % 60 == 0)
         {
            LogWarning($"0719|Event_405 konnte nicht geschrieben werden. Fehler={ex.GetType().Name}: {ex.Message}");
         }
      }
   }

   private void WriteEventPayloadNode(
      EventConfiguration eventConfiguration,
      string nodeName,
      object value)
   {
      EventNodeConfiguration? node = eventConfiguration.Nodes.FirstOrDefault(n =>
         string.Equals(n.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
      if (node is null || string.IsNullOrWhiteSpace(node.OpcNode))
      {
         return;
      }

      WriteNode(node.OpcNode, value);
   }

   private void WriteNode(string opcNode, object value)
   {
      OpcStatus status = opcClient!.WriteNode(opcNode, value);
      if (status.IsBad)
      {
         throw new InvalidOperationException(
            $"OPC-Schreiben fehlgeschlagen. Node={opcNode}, Status={status.Code}, Beschreibung={status.Description}");
      }
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
      event402Timer.Stop();
      event403Timer.Stop();
      event404Timer.Stop();
      event405Timer.Stop();
      opcClient?.Disconnect();
      opcClient?.Dispose();
      reconnectCancellation.Dispose();
      fileLogSink.Dispose();
      base.OnClosed(e);
   }
}
