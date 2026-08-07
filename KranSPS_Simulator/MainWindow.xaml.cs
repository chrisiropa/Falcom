using Falcom;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KranSPS_Simulator;

public partial class MainWindow : Window
{
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectLogThrottle = TimeSpan.FromMinutes(1);
    private const double DemoKranSpeedMmPerSecond = 2400.0;
    private const double DemoKatzeSpeedMmPerSecond = 2400.0;
    private const double DemoHubSpeedMmPerSecond = 1800.0;
    private const int DemoTelegrammNummer = -1;
    private const string Event102TriggerNodeName = "Event_102";
    private const string Event202TriggerNodeName = "Event_202";
    private const string AuftragNummerNodeName = "Nr";
    private const string AuftragTeilfahrtNodeName = "TeilNr";
    private const string IstMasseNodeName = "IstMasse";
    private const decimal MaxChargierIstGewichtKg = 1000m;
    private const int EinlagerIstGewichtMinKg = 700;
    private const int EinlagerIstGewichtMaxKg = 900;
    private const string Event203Name = "Event_203";
    private const string Event203TriggerNodeName = "Event_203";
    private const string PosKranNodeName = "PosKran";
    private const string PosKatzeNodeName = "PosKatze";
    private const string PosHubNodeName = "PosHub";
    private const string MagnetAnNodeName = "MagnetAn";
    private const string MasseNettoNodeName = "MasseNetto";
    private const string Event207Name = "Event_207";
    private const string Event207TriggerNodeName = "Event_207";
    private const string Event207LkwPlatzNodeName = "LKWPlatz";
    private const string Event104TriggerNodeName = "Event_104";
    private const string Event105TriggerNodeName = "Event_105";
    private const string Event204TriggerNodeName = "Event_204";
    private const string Event205TriggerNodeName = "Event_205";
    private const string Event206TriggerNodeName = "Event_206";

    private readonly FalcomUiLogSink uiLogSink = new();
    private readonly FalcomFileSink fileLogSink;
    private readonly DispatcherTimer logRefreshTimer = new();
    private readonly DispatcherTimer statusRefreshTimer = new();
    private readonly Random demoRandom = new();
    private readonly object opcSyncRoot = new();
    private readonly string opcEndpoint;
    private readonly string databaseConnectionString;
    private readonly string event201NodeId;
    private readonly string event101NodeId;
    private readonly IReadOnlyList<EventNodeConfiguration> kranfahrtBeendetNodes;
    private readonly IReadOnlyList<EventNodeConfiguration> kranfahrtAuftragNodes;
    private readonly IReadOnlyList<SimEventMappingConfiguration> kranfahrtBeendetZuordnungen;
    private readonly IReadOnlyList<EventNodeConfiguration> event203Nodes;
    private readonly IReadOnlyList<EventNodeConfiguration> event207Nodes;
    private readonly IReadOnlyList<EventNodeConfiguration> event104Nodes;
    private readonly IReadOnlyList<EventNodeConfiguration> event105Nodes;
    private readonly IReadOnlyList<EventNodeConfiguration> event204Nodes;
    private readonly IReadOnlyList<EventNodeConfiguration> event205Nodes;
    private readonly IReadOnlyList<EventNodeConfiguration> event206Nodes;
    private readonly Dictionary<string, EventNodeConfiguration> kranfahrtBeendetNodesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EventNodeConfiguration> kranfahrtAuftragNodesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> kranfahrtBeendetValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> kranfahrtAuftragValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> letzteKranfahrtAuftragPayload = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, EinlagerLeerSimulationState> einlagerLeerSimulationen = new();
    private int? letzteVerarbeiteteAuftragTelegrammNummer;
    private readonly Dictionary<string, string> event203Values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> event104Values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> event105Values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> event204Values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> event205Values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> event206Values = new(StringComparer.OrdinalIgnoreCase);
    private readonly KranPositionGroundPosition grundstellung;
    private readonly IReadOnlyDictionary<long, SimKranPosition> positionenById;
    private readonly IReadOnlyDictionary<long, IReadOnlyDictionary<int, KranPositionGroundPosition>> anfahrpunkteByPositionId;
    private readonly CancellationTokenSource reconnectCancellation = new();
    private readonly CancellationTokenSource lebensZaehlerCancellation = new();

    private OpcClient? opcClient;
    private OpcSubscription? subscription;
    private readonly List<OpcMonitoredItem> monitoredItems = new();
    private long lastLogChangeVersion;
    private int reconnectLoopRunning;
    private bool disposed;
    private DateTime nextReconnectLogUtc = DateTime.MinValue;
    private DateTime nextLebensZaehlerErrorLogUtc = DateTime.MinValue;
    private DateTime nextEvent203ErrorLogUtc = DateTime.MinValue;
    private string opcStatusText = "Initialisierung";
    private string opcStatusDetailText = string.Empty;
    private int posKranX;
    private int posKatzeY;
    private int posHubZ;
    private int spsLebensZaehler;
    private int event203Zaehler;
    private int event204AnforderungsZaehler;
    private int event205AnforderungsZaehler;
    private int event206AnforderungsZaehler;
    private int? letzterSpsLebensZaehler;
    private DateTime? letzterSpsLebensZaehlerGesendetAm;
    private int? letzterFalcomLebensZaehler;
    private DateTime? letzterFalcomLebensZaehlerEmpfangenAm;
    private int gewuenschterMagnetAnWert;
    private int masseNetto;
    private int? letzterGesendeterMagnetAnWert;
    private AktuelleFahrtSimulation? aktiveSimulationsFahrt;
    private AktuelleFahrtSimulation? letzteAbgefahreneFahrt;
    private long naechsteInterneFahrtId = 1;
    private DateTime? warteAufNeueFahrtBisUtc;
    private bool demoModeAktiv;
    private SimulationsFahrzustand fahrzustand = SimulationsFahrzustand.Grundstellung;
    private KranMovement? aktuelleBewegung;

    public MainWindow()
    {
        InitializeComponent();

        logRefreshTimer.Interval = TimeSpan.FromMilliseconds(200);
        logRefreshTimer.Tick += (_, _) => RefreshLogs();
        logRefreshTimer.Start();

        statusRefreshTimer.Interval = TimeSpan.FromSeconds(1);
        statusRefreshTimer.Tick += (_, _) => RefreshStatusView();
        statusRefreshTimer.Start();

        SimulatorConfiguration configuration = DatabaseConfig.Load();
        databaseConnectionString = configuration.ConnectionString;
        opcEndpoint = configuration.OpcEndpoint.Trim();
        event201NodeId = configuration.Event201NodeId.Trim();
        event101NodeId = configuration.Event101NodeId.Trim();
        kranfahrtBeendetNodes = configuration.KranfahrtBeendetNodes;
        kranfahrtAuftragNodes = configuration.KranfahrtAuftragNodes;
        kranfahrtBeendetZuordnungen = configuration.KranfahrtBeendetZuordnungen;
        foreach (EventNodeConfiguration node in kranfahrtBeendetNodes)
        {
            kranfahrtBeendetNodesByName[node.NodeName] = node;
        }
        foreach (EventNodeConfiguration node in kranfahrtAuftragNodes)
        {
            kranfahrtAuftragNodesByName[node.NodeName] = node;
        }
        event203Nodes = configuration.Event203Nodes;
        foreach (EventNodeConfiguration node in event203Nodes)
        {
            event203Values[node.NodeName] = node.NodeName switch
            {
                "Bereit" => bool.TrueString,
                "Automatik" => bool.TrueString,
                _ when string.Equals(node.DataType, "Bit", StringComparison.OrdinalIgnoreCase) => bool.FalseString,
                _ => "0"
            };
        }
        event207Nodes = configuration.Event207Nodes;
        event104Nodes = configuration.Event104Nodes;
        event105Nodes = configuration.Event105Nodes;
        event204Nodes = configuration.Event204Nodes;
        event205Nodes = configuration.Event205Nodes;
        event206Nodes = configuration.Event206Nodes;
        foreach (EventNodeConfiguration node in event104Nodes)
        {
            event104Values[node.NodeName] = "-";
        }
        foreach (EventNodeConfiguration node in event105Nodes)
        {
            event105Values[node.NodeName] = "-";
        }
        foreach (EventNodeConfiguration node in event204Nodes)
        {
            event204Values[node.NodeName] = "-";
        }
        foreach (EventNodeConfiguration node in event205Nodes)
        {
            event205Values[node.NodeName] = "-";
        }
        foreach (EventNodeConfiguration node in event206Nodes)
        {
            event206Values[node.NodeName] = "-";
        }
        grundstellung = configuration.Grundstellung;
        positionenById = configuration.Positionen;
        anfahrpunkteByPositionId = configuration.Anfahrpunkte;
        fileLogSink = new FalcomFileSink(configuration.LogfilePath);
        ProgramStartBanner.WriteToLogfile(
            fileLogSink,
            "KranSPS_Simulator",
            "KRAN-SPS-SIMULATOR PROGRAMMSTART");

        ConfigureTraegerLicense();
        CreateClientInstance();

        Log($"010B|Logdatei aktiv: {configuration.LogfilePath}");
        Log($"010C|OPC Endpoint aus FALCOM_PARAMETER.OpcServer: {opcEndpoint}");
        if (configuration.SimulatorSubstitutionen.Count == 0)
        {
            Log("010D|SimulatorSubstitution aus FALCOM_PARAMETER: keine Substitution aktiv.");
        }
        else
        {
            Log($"010E|SimulatorSubstitution aus FALCOM_PARAMETER aktiv: {string.Join("; ", configuration.SimulatorSubstitutionen.Select(substitution => $"{substitution.Suchtext} -> {substitution.Ersatztext}"))}");
        }

        if (string.IsNullOrWhiteSpace(event201NodeId))
        {
            LogError("010A|Event_201.Event_201 ist in der Datenbank nicht gueltig konfiguriert. SPS->FALCOM Event_201 wird nicht geschrieben.");
        }
        else
        {
            Log($"010F|Event_201.Event_201 Node: {event201NodeId}");
        }

        if (string.IsNullOrWhiteSpace(event101NodeId))
        {
            LogError("0110|Event_101.Event_101 ist in der Datenbank nicht gueltig konfiguriert. FALCOM->SPS Event_101 wird nicht empfangen.");
        }
        else
        {
            Log($"0111|Event_101.Event_101 Node: {event101NodeId}");
        }

        Log($"0112|Event_202 Variablen: {string.Join(", ", kranfahrtBeendetNodes.Select(node => node.NodeName))}");
        Log($"0113|Event_102/202 Sim-Zuordnungen geladen: {kranfahrtBeendetZuordnungen.Count}. {string.Join("; ", kranfahrtBeendetZuordnungen.Select(mapping => mapping.Info ?? mapping.TargetNode.NodeName))}");
        Log($"0114|Event_102 Variablen: {string.Join(", ", kranfahrtAuftragNodes.Select(node => node.NodeName))}");
        Log($"0115|Event 203 {Event203Name} Variablen: {string.Join(", ", event203Nodes.Select(node => node.NodeName))}");
        Log($"01B6|Event 207 {Event207Name} Variablen: {string.Join(", ", event207Nodes.Select(node => node.NodeName))}");
        Log($"01DE|Event 104 Variablen: {string.Join(", ", event104Nodes.Select(node => node.NodeName))}");
        Log($"020B|Event 105 Variablen: {string.Join(", ", event105Nodes.Select(node => node.NodeName))}");
        Log($"01DF|Event 204 Variablen: {string.Join(", ", event204Nodes.Select(node => node.NodeName))}");
        Log($"020C|Event 205 Variablen: {string.Join(", ", event205Nodes.Select(node => node.NodeName))}");
        Log($"01FA|Event 206 Variablen: {string.Join(", ", event206Nodes.Select(node => node.NodeName))}");
        FahreGrundstellungAn();
        Log("0116|Kran-SPS-Simulator bereit.");
        RefreshLogs();
        RefreshStatusView();
        RefreshEventView();

        StartBackgroundReconnectLoop("Programmstart");
        StartLebensZaehlerLoop();
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

    private void ConnectOnce()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(MainWindow));
        }

        lock (opcSyncRoot)
        {
            Log($"013C|Verbindung zu {opcEndpoint} wird aufgebaut.");
            ResetSubscription();
            opcClient?.Connect();
            ConfigureOpcSubscriptions();
            Log("013D|OPC-Verbindung und Kanalregistrierung sind bereit.");
        }
    }

    private void StartBackgroundReconnectLoop(string reason)
    {
        if (disposed || reconnectCancellation.IsCancellationRequested)
        {
            return;
        }

        if (Interlocked.CompareExchange(
                ref reconnectLoopRunning,
                1,
                0) != 0)
        {
            return;
        }

        LogWarning($"013E|OPC-Hintergrund-Reconnect wird gestartet. Grund={reason}.");

        _ = Task.Run(
            async () =>
            {
                var attempt = 1;
                CancellationToken cancellationToken = reconnectCancellation.Token;

                try
                {
                    while (!disposed && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            lock (opcSyncRoot)
                            {
                                if (opcClient is { State: OpcClientState.Connected })
                                {
                                    SetOpcConnectedFromBackground(
                                        $"Verbunden mit {opcEndpoint}");
                                    Log($"013F|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
                                    return;
                                }

                                if (opcClient is not null)
                                {
                                    try
                                    {
                                        ResetSubscription();
                                        opcClient.StateChanged -= OnClientStateChanged;
                                        opcClient.Disconnect();
                                    }
                                    catch
                                    {
                                    }

                                    opcClient.Dispose();
                                    opcClient = null;
                                }

                                CreateClientInstance();
                                Log($"0140|Verbindung zu {opcEndpoint} wird aufgebaut.");
                                opcClient!.Connect();
                                ConfigureOpcSubscriptions();
                                Log("0141|OPC-Verbindung und Kanalregistrierung sind bereit.");
                                SetOpcConnectedFromBackground(
                                    $"Verbunden mit {opcEndpoint}");
                                Log($"0142|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            SetOpcReconnectFromBackground("Reconnect laeuft");

                            if (DateTime.UtcNow >= nextReconnectLogUtc)
                            {
                                LogWarning(
                                    $"0143|OPC-Hintergrund-Reconnect Versuch={attempt} noch nicht erfolgreich. " +
                                    $"Naechster Versuch in {ConnectRetryDelay.TotalSeconds:0} Sekunden. " +
                                    $"Fehler={ex.GetType().Name}: {ex.Message}");
                                nextReconnectLogUtc = DateTime.UtcNow.Add(ReconnectLogThrottle);
                            }
                        }

                        attempt++;
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
    private void ResetSubscription()
    {
        if (subscription is null)
        {
            monitoredItems.Clear();
            return;
        }

        foreach (OpcMonitoredItem item in monitoredItems)
        {
            item.DataChangeReceived -= HandleOpcDataChange;
        }

        if (monitoredItems.Count > 0)
        {
            try
            {
                subscription.RemoveMonitoredItem(monitoredItems);
        subscription.ApplyChanges();
            }
            catch
            {
                // Bei totem OPC-Kanal darf das Aufraeumen den Reconnect nicht blockieren.
            }

            monitoredItems.Clear();
        }

        subscription = null;
    }

    private void ConfigureOpcSubscriptions()
    {
        if (opcClient is null)
        {
            return;
        }

        subscription = opcClient.SubscribeNodes();

        if (!string.IsNullOrWhiteSpace(event101NodeId))
        {
            var falcomLifeItem = new OpcMonitoredItem(event101NodeId, OpcAttribute.Value)
            {
                Tag = "Event_101.Event_101"
            };
            falcomLifeItem.DataChangeReceived += HandleOpcDataChange;
            subscription.AddMonitoredItem(falcomLifeItem);
            monitoredItems.Add(falcomLifeItem);
            Log($"0144|OPC Empfangskanal registriert. Event=Event_101, Node={event101NodeId}");
        }

        if (TryGetKranfahrtAuftragNode(Event102TriggerNodeName, out EventNodeConfiguration telegrammNode))
        {
            var telegrammItem = new OpcMonitoredItem(telegrammNode.OpcNode, OpcAttribute.Value)
            {
                Tag = "Event_102.Event_102"
            };
            telegrammItem.DataChangeReceived += HandleOpcDataChange;
            subscription.AddMonitoredItem(telegrammItem);
            monitoredItems.Add(telegrammItem);
            Log($"006D|OPC Empfangskanal registriert. Event=Event_102, Trigger=Event_102, Node={telegrammNode.OpcNode}");
        }
        else
        {
            LogWarning("0145|Event_102.Event_102 ist nicht konfiguriert. Event_102 kann nicht empfangen werden.");
        }

        EventNodeConfiguration? event104Trigger = event104Nodes.FirstOrDefault(
            node => string.Equals(node.NodeName, Event104TriggerNodeName, StringComparison.OrdinalIgnoreCase));
        if (event104Trigger is not null && !string.IsNullOrWhiteSpace(event104Trigger.OpcNode))
        {
            var event104Item = new OpcMonitoredItem(event104Trigger.OpcNode, OpcAttribute.Value)
            {
                Tag = "Event_104.Event_104"
            };
            event104Item.DataChangeReceived += HandleOpcDataChange;
            subscription.AddMonitoredItem(event104Item);
            monitoredItems.Add(event104Item);
            Log($"01E0|OPC Empfangskanal registriert. Event=Event_104, Node={event104Trigger.OpcNode}");
        }
        else
        {
            LogWarning("01E1|Event_104.Event_104 ist nicht konfiguriert. Bunkermaterial-Antworten koennen nicht empfangen werden.");
        }
        EventNodeConfiguration? event105Trigger = event105Nodes.FirstOrDefault(
            node => string.Equals(node.NodeName, Event105TriggerNodeName, StringComparison.OrdinalIgnoreCase));
        if (event105Trigger is not null && !string.IsNullOrWhiteSpace(event105Trigger.OpcNode))
        {
            var event105Item = new OpcMonitoredItem(event105Trigger.OpcNode, OpcAttribute.Value)
            {
                Tag = "Event_105.Event_105"
            };
            event105Item.DataChangeReceived += HandleOpcDataChange;
            subscription.AddMonitoredItem(event105Item);
            monitoredItems.Add(event105Item);
            Log($"0212|OPC Empfangskanal registriert. Event=Event_105, Node={event105Trigger.OpcNode}");
        }
        else
        {
            LogWarning("0213|Event_105.Event_105 ist nicht konfiguriert. Materialeigenschaften-Antworten koennen nicht empfangen werden.");
        }

        subscription.ApplyChanges();
        InitialisiereKranfahrtAuftragTelegrammNoLock();
    }

    private void InitialisiereKranfahrtAuftragTelegrammNoLock()
    {
        if (!TryGetKranfahrtAuftragNode(Event102TriggerNodeName, out EventNodeConfiguration telegrammNode))
        {
            return;
        }

        try
        {
            OpcValue value = opcClient!.ReadNode(telegrammNode.OpcNode);
            if (!value.Status.IsGood || value.Value is null)
            {
                LogWarning(
                    $"0146|Initialer KranfahrtAuftrag-Telegrammstand konnte nicht gelesen werden. " +
                    $"Node={telegrammNode.OpcNode}, Status={value.Status.Code}, Beschreibung={value.Status.Description}. " +
                    "Simulator wartet trotzdem auf die naechste Aenderung.");
                return;
            }

            int telegrammNummer = Convert.ToInt32(value.Value, CultureInfo.InvariantCulture);
            Dictionary<string, object?> payload = ReadEventValuesNoLock(kranfahrtAuftragNodes);

            if (IstInitialerKranfahrtAuftragBereitsBeendetNoLock(payload, out string begruendung))
            {
                letzteVerarbeiteteAuftragTelegrammNummer = telegrammNummer;
                Log($"0147|Warten auf naechste Fahrt. Letztes KranfahrtAuftrag-Telegramm war {telegrammNummer}. {begruendung}");
                return;
            }

            Log($"0117|Initialer KranfahrtAuftrag wird als offener Auftrag verarbeitet. TelegrammNummer={telegrammNummer}. {begruendung}");
            SchreibeKranfahrtBeendetZuordnungNoLock(payload, triggerErhoehen: false);
            VerarbeiteKranfahrtAuftragPayload(telegrammNummer, payload, "Initialer KranfahrtAuftrag");
        }
        catch (Exception ex)
        {
            LogWarning($"0118|Initialer KranfahrtAuftrag-Telegrammstand konnte nicht gelesen werden. Fehler={ex.GetType().Name}: {ex.Message}");
        }
    }

    private void HandleOpcDataChange(object sender, OpcDataChangeReceivedEventArgs e)
    {
        try
        {
            string changedNodeId = e.MonitoredItem.NodeId.ToString();
            object? rawValue = e.Item.Value.Value;

            LogOpcReceive($"0119|OPC Empfang: Node={changedNodeId}, Wert={rawValue}");

            if (string.Equals(changedNodeId, event101NodeId, StringComparison.Ordinal))
            {
                int value = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                SetFalcomLebensZaehlerFromBackground(value, DateTime.Now);
                return;
            }

            EventNodeConfiguration? event104Trigger = event104Nodes.FirstOrDefault(
                node => string.Equals(node.NodeName, Event104TriggerNodeName, StringComparison.OrdinalIgnoreCase));
            if (event104Trigger is not null
                && string.Equals(changedNodeId, event104Trigger.OpcNode, StringComparison.Ordinal))
            {
                int responseZaehler = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                Dictionary<string, object?> payload;
                lock (opcSyncRoot)
                {
                    payload = ReadEventValuesNoLock(event104Nodes);
                }

                SetEventValues(event104Values, payload);
                Log($"01E2|Event_104 empfangen: AntwortZaehler={responseZaehler}, Bunkerwerte={payload.Count - 1}.");
                return;
            }

            EventNodeConfiguration? event105Trigger = event105Nodes.FirstOrDefault(
                node => string.Equals(node.NodeName, Event105TriggerNodeName, StringComparison.OrdinalIgnoreCase));
            if (event105Trigger is not null
                && string.Equals(changedNodeId, event105Trigger.OpcNode, StringComparison.Ordinal))
            {
                int responseZaehler = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                Dictionary<string, object?> payload;
                lock (opcSyncRoot)
                {
                    payload = ReadEventValuesNoLock(event105Nodes);
                }

                SetEventValues(event105Values, payload);
                Log($"0214|Event_105 empfangen: AntwortZaehler={responseZaehler}, Materialwerte={payload.Count - 1}.");
                return;
            }
            if (TryGetKranfahrtAuftragNode(Event102TriggerNodeName, out EventNodeConfiguration telegrammNode)
                && string.Equals(changedNodeId, telegrammNode.OpcNode, StringComparison.Ordinal))
            {
                int telegrammNummer = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                VerarbeiteKranfahrtAuftragTelegramm(telegrammNummer);
            }
        }
        catch (Exception ex)
        {
            LogError($"011A|OPC Callback konnte nicht verarbeitet werden. Fehler={ex.GetType().Name}: {ex.Message}");
        }
    }

    private void VerarbeiteKranfahrtAuftragTelegramm(int telegrammNummer)
    {
        if (demoModeAktiv)
        {
            letzteVerarbeiteteAuftragTelegrammNummer = telegrammNummer;
            Log($"006E|KranfahrtAuftrag im Demo-Modus empfangen und ignoriert. Es werden nur Kranpositionswerte geschrieben. TelegrammNummer={telegrammNummer}.");
            return;
        }

        if (letzteVerarbeiteteAuftragTelegrammNummer == telegrammNummer)
        {
            return;
        }

        if (letzteVerarbeiteteAuftragTelegrammNummer is null)
        {
            letzteVerarbeiteteAuftragTelegrammNummer = telegrammNummer;
            Log($"011B|Initialer KranfahrtAuftrag-Telegrammstand aus Callback uebernommen. Wert={telegrammNummer}. Simulator wartet auf die naechste Aenderung.");
            return;
        }

        Dictionary<string, object?> payload;
        lock (opcSyncRoot)
        {
            if (opcClient?.State != OpcClientState.Connected)
            {
                return;
            }

            payload = ReadEventValuesNoLock(kranfahrtAuftragNodes);
            SchreibeKranfahrtBeendetZuordnungNoLock(payload, triggerErhoehen: false);
        }

        VerarbeiteKranfahrtAuftragPayload(telegrammNummer, payload, "KranfahrtAuftrag aus OPC-Aenderung");
    }

    private void VerarbeiteKranfahrtAuftragPayload(
        int telegrammNummer,
        IReadOnlyDictionary<string, object?> payload,
        string quelle)
    {
        letzteVerarbeiteteAuftragTelegrammNummer = telegrammNummer;
        lock (letzteKranfahrtAuftragPayload)
        {
            letzteKranfahrtAuftragPayload.Clear();
            foreach (KeyValuePair<string, object?> item in payload)
            {
                letzteKranfahrtAuftragPayload[item.Key] = item.Value;
            }
        }

        SetEventValues(kranfahrtAuftragValues, payload);
        Log($"011C|{quelle}: Event-1-Zuordnung ohne Trigger angewendet. TelegrammNummer={telegrammNummer}.");

        AktuelleFahrtSimulation? fahrt = ErstelleFahrtAusKranfahrtAuftragPayload(telegrammNummer, payload);
        if (fahrt is null)
        {
            return;
        }

        StarteFahrtZurQuelle(fahrt, DateTime.UtcNow);
    }

    private bool IstInitialerKranfahrtAuftragBereitsBeendetNoLock(
        IReadOnlyDictionary<string, object?> auftragPayload,
        out string begruendung)
    {
        begruendung = "Initialer Event-2-Fahrauftrag gilt als offen.";

        if (!TryGetPayloadInt32(auftragPayload, Event102TriggerNodeName, out int auftragTelegrammNummer)
            || auftragTelegrammNummer <= 0)
        {
            begruendung = "Event_102 enthaelt keinen plausiblen Triggerwert; Initialauftrag wird nicht gefahren.";
            return true;
        }

        if (!TryGetPayloadInt64(auftragPayload, AuftragNummerNodeName, out long auftragId)
            || !TryGetPayloadInt32(auftragPayload, AuftragTeilfahrtNodeName, out int auftragTeilfahrt)
            || !TryGetPayloadInt64(auftragPayload, "Quelle", out long quellePositionId)
            || !TryGetPayloadInt64(auftragPayload, "Ziel", out long zielPositionId))
        {
            begruendung = $"Event_102 Trigger={auftragTelegrammNummer}; Payload ist unvollstaendig. Initialauftrag wird nicht gefahren.";
            return true;
        }

        if (!HatPassendeOffeneAktuelleFahrt(
                auftragId,
                auftragTeilfahrt,
                quellePositionId,
                zielPositionId,
                out string aktuelleFahrtBegruendung))
        {
            begruendung =
                $"Event_102 Trigger={auftragTelegrammNummer}; Auftrag={auftragId}, Teilfahrt={auftragTeilfahrt}, " +
                $"Quelle={quellePositionId}, Ziel={zielPositionId}. {aktuelleFahrtBegruendung} Initialauftrag wird nicht gefahren.";
            return true;
        }

        begruendung =
            $"Event_102 Trigger={auftragTelegrammNummer}; Auftrag={auftragId}, Teilfahrt={auftragTeilfahrt}, " +
            $"Quelle={quellePositionId}, Ziel={zielPositionId}. {aktuelleFahrtBegruendung}";
        return false;
    }

    private bool HatPassendeOffeneAktuelleFahrt(
        long auftragId,
        int auftragTeilfahrt,
        long quellePositionId,
        long zielPositionId,
        out string begruendung)
    {
        try
        {
            using var connection = new SqlConnection(databaseConnectionString);
            using var command = new SqlCommand(
                """
                SELECT TOP (1)
                   ID,
                   Status
                FROM dbo.FALCOM_AKTUELLE_FAHRT
                WHERE AuftragID = @AuftragID
                  AND AuftragTeilfahrt = @AuftragTeilfahrt
                  AND ISNULL(QuellePositionID, -1) = @QuellePositionID
                  AND ISNULL(ZielPositionID, -1) = @ZielPositionID
                  AND FertigDatumZeit IS NULL
                  AND UPPER(LTRIM(RTRIM(COALESCE(Status, N'')))) NOT IN (N'FERTIG', N'ABGESCHLOSSEN')
                ORDER BY ID DESC;
                """,
                connection)
            {
                CommandTimeout = 10
            };

            command.Parameters.Add("@AuftragID", System.Data.SqlDbType.BigInt).Value = auftragId;
            command.Parameters.Add("@AuftragTeilfahrt", System.Data.SqlDbType.Int).Value = auftragTeilfahrt;
            command.Parameters.Add("@QuellePositionID", System.Data.SqlDbType.BigInt).Value = quellePositionId;
            command.Parameters.Add("@ZielPositionID", System.Data.SqlDbType.BigInt).Value = zielPositionId;

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                begruendung = "Keine passende offene Fahrt in FALCOM_AKTUELLE_FAHRT gefunden.";
                return false;
            }

            begruendung =
                $"Passende offene Fahrt in FALCOM_AKTUELLE_FAHRT gefunden: " +
                $"AktuelleFahrtID={Convert.ToInt64(reader["ID"], CultureInfo.InvariantCulture)}, Status={Convert.ToString(reader["Status"], CultureInfo.InvariantCulture) ?? "-"}.";
            return true;
        }
        catch (Exception ex)
        {
            begruendung = $"Aktuelle Fahrt konnte nicht gegen die Datenbank geprueft werden: {ex.GetType().Name}: {ex.Message}.";
            return false;
        }
    }
    private AktuelleFahrtSimulation? ErstelleFahrtAusKranfahrtAuftragPayload(
        int telegrammNummer,
        IReadOnlyDictionary<string, object?> payload)
    {
        if (!TryGetPayloadInt64(payload, "Quelle", out long quellePositionId)
            || !TryGetPayloadInt64(payload, "Ziel", out long zielPositionId))
        {
            LogWarning($"011D|KranfahrtAuftrag kann nicht gefahren werden. Quelle oder Ziel fehlt. TelegrammNummer={telegrammNummer}.");
            return null;
        }

        if (!positionenById.TryGetValue(quellePositionId, out SimKranPosition? quelle))
        {
            LogWarning($"011E|KranfahrtAuftrag kann nicht gefahren werden. QuellePositionID={quellePositionId} ist in FALCOM_KRAN_POSITION nicht konfiguriert oder hat keine Abwurfposition. TelegrammNummer={telegrammNummer}.");
            return null;
        }

        if (!positionenById.TryGetValue(zielPositionId, out SimKranPosition? ziel))
        {
            LogWarning($"011F|KranfahrtAuftrag kann nicht gefahren werden. ZielPositionID={zielPositionId} ist in FALCOM_KRAN_POSITION nicht konfiguriert oder hat keine Abwurfposition. TelegrammNummer={telegrammNummer}.");
            return null;
        }

        TryGetPayloadInt64(payload, AuftragNummerNodeName, out long auftragNummer);
        TryGetPayloadInt32(payload, AuftragTeilfahrtNodeName, out int auftragTeilfahrt);
        TryGetPayloadInt32(payload, "QuelleUnterPos", out int quelleUnterposition);
        TryGetPayloadInt32(payload, "ZielUnterPos", out int zielUnterposition);
        TryGetPayloadDecimal(payload, "SollMasse", out decimal sollMasse);

        KranPositionGroundPosition quelleFahrposition = ResolveFahrposition(
            quelle,
            quelleUnterposition,
            "Quelle",
            telegrammNummer);
        KranPositionGroundPosition zielFahrposition = ResolveFahrposition(
            ziel,
            zielUnterposition,
            "Ziel",
            telegrammNummer);

        var fahrt = new AktuelleFahrtSimulation(
            naechsteInterneFahrtId++,
            telegrammNummer,
            "OPC_EVENT_2",
            auftragNummer,
            auftragTeilfahrt,
            "EMPFANGEN",
            quellePositionId,
            zielPositionId,
            quelleUnterposition,
            zielUnterposition,
            sollMasse,
            quelle.Bezeichnung,
            ziel.Bezeichnung,
            quelleFahrposition,
            zielFahrposition);

        Log(
            "0120|KranfahrtAuftrag in Simulatorfahrt umgesetzt: " +
            $"TelegrammNummer={telegrammNummer}, Auftrag={auftragNummer}, Teilfahrt={auftragTeilfahrt}, " +
            $"Quelle={quelle.Bezeichnung} ({quellePositionId}), QuelleUnterPos={quelleUnterposition}, " +
            $"Ziel={ziel.Bezeichnung} ({zielPositionId}), ZielUnterPos={zielUnterposition}, SollMasse={sollMasse:0.###}.");

        return fahrt;
    }

    private KranPositionGroundPosition ResolveFahrposition(
        SimKranPosition position,
        int unterposition,
        string rolle,
        int telegrammNummer)
    {
        if (unterposition > 0
            && anfahrpunkteByPositionId.TryGetValue(position.PositionID, out IReadOnlyDictionary<int, KranPositionGroundPosition>? punkte)
            && punkte.ContainsKey(unterposition))
        {
            KranPositionGroundPosition punkt = punkte[unterposition];
            Log(
                $"0158|{rolle}-Unterposition auf Anfahrpunkt umgesetzt: " +
                $"TelegrammNummer={telegrammNummer}, PositionID={position.PositionID}, UnterPos={unterposition}, " +
                $"PosKran={punkt.PosKranX}, PosKatze={punkt.PosKatzeY}, PosHub={punkt.PosHubZ}.");
            return punkt;
        }

        if (unterposition > 0)
        {
            LogWarning(
                $"0159|{rolle}-Unterposition nicht in Anfahrpunkten gefunden. Nutze Objekt-Abwurfposition. " +
                $"TelegrammNummer={telegrammNummer}, PositionID={position.PositionID}, UnterPos={unterposition}.");
        }

        return position.Position;
    }

    private static bool TryGetPayloadInt64(
        IReadOnlyDictionary<string, object?> payload,
        string name,
        out long value)
    {
        value = 0;
        if (!payload.TryGetValue(name, out object? raw) || raw is null)
        {
            return false;
        }

        try
        {
            value = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPayloadInt32(
        IReadOnlyDictionary<string, object?> payload,
        string name,
        out int value)
    {
        value = 0;
        if (!payload.TryGetValue(name, out object? raw) || raw is null)
        {
            return false;
        }

        try
        {
            value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPayloadDecimal(
        IReadOnlyDictionary<string, object?> payload,
        string name,
        out decimal value)
    {
        value = 0;
        if (!payload.TryGetValue(name, out object? raw) || raw is null)
        {
            return false;
        }

        try
        {
            value = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private OpcValue ReadRequiredOpcPayloadWithNullRetryNoLock(
        EventNodeConfiguration node)
    {
        OpcValue value = opcClient!.ReadNode(node.OpcNode);

        if (!value.Status.IsGood)
        {
            throw new InvalidOperationException(
                $"OPC-Lesen fehlgeschlagen. Variable={node.NodeName}, Node={node.OpcNode}, {DescribeOpcValue(value)}");
        }

        if (value.Value is not null)
        {
            return value;
        }

        LogError($"006F|OPC Payloadwert ist NULL. Variable={node.NodeName}, Node={node.OpcNode}, {DescribeOpcValue(value)}. Warte 500 ms und lese denselben Node erneut.");

        Thread.Sleep(500);

        OpcValue retryValue = opcClient.ReadNode(node.OpcNode);

        if (!retryValue.Status.IsGood)
        {
            LogError($"0121|OPC Payloadwert-Reload nach 500 ms fehlgeschlagen. Variable={node.NodeName}, Node={node.OpcNode}, {DescribeOpcValue(retryValue)}.");
            throw new InvalidOperationException(
                $"OPC-Lesen nach NULL-Retry fehlgeschlagen. Variable={node.NodeName}, Node={node.OpcNode}, {DescribeOpcValue(retryValue)}");
        }

        if (retryValue.Value is null)
        {
            LogError($"0122|OPC Payloadwert ist auch nach 500 ms NULL. Variable={node.NodeName}, Node={node.OpcNode}, {DescribeOpcValue(retryValue)}. Event wird nicht weiterverarbeitet.");
            throw new InvalidOperationException(
                $"OPC Payloadwert ist auch nach 500 ms NULL. Variable={node.NodeName}, Node={node.OpcNode}");
        }

        Log($"0123|OPC Payloadwert nach NULL-Retry erfolgreich gelesen. Variable={node.NodeName}, Node={node.OpcNode}, {DescribeOpcValue(retryValue)}.");
        return retryValue;
    }

    private static string DescribeOpcValue(OpcValue value)
    {
        return $"Status={value.Status.Code}, Beschreibung={value.Status.Description}, Wert={(value.Value is null ? "<null>" : value.Value)}, DataType={value.DataType}, SourceTimestamp={value.SourceTimestamp:O}, ServerTimestamp={value.ServerTimestamp:O}";
    }
    private Dictionary<string, object?> ReadEventValuesNoLock(IReadOnlyList<EventNodeConfiguration> nodes)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (EventNodeConfiguration node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.OpcNode))
            {
                continue;
            }

            OpcValue value = ReadRequiredOpcPayloadWithNullRetryNoLock(node);

            values[node.NodeName] = value.Value;
            LogOpcReceive($"0124|OPC Event lesen: {node.NodeName}={value.Value}");
        }

        return values;
    }

    private void SchreibeKranfahrtBeendetZuordnungNoLock(
        IReadOnlyDictionary<string, object?> auftragValues,
        bool triggerErhoehen)
    {
        int geschrieben = 0;
        int uebersprungen = 0;
        var triggerMappings = new List<SimEventMappingConfiguration>();

        foreach (SimEventMappingConfiguration mapping in kranfahrtBeendetZuordnungen)
        {
            string info = mapping.Info ?? mapping.TargetNode.NodeName;
            string typ = mapping.Zuordnungstyp.Trim().ToUpperInvariant();

            if (string.Equals(typ, "TRIGGER_ERHOEHEN", StringComparison.OrdinalIgnoreCase))
            {
                triggerMappings.Add(mapping);
                continue;
            }

            if (string.Equals(typ, "DIREKT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(mapping.SourceNodeName)
                    || !auftragValues.TryGetValue(mapping.SourceNodeName, out object? sourceValue))
                {
                    LogError($"007D|SIM-Zuordnung abgebrochen, weil Quellwert fehlt. ID={mapping.ID}, Info={info}, SourceNode={mapping.SourceNodeName ?? "-"}.");
                    throw new InvalidOperationException(
                        $"SIM-Zuordnung abgebrochen, weil Quellwert fehlt. ID={mapping.ID}, Info={info}, SourceNode={mapping.SourceNodeName ?? "-"}");
                }

                if (sourceValue is null)
                {
                    LogError($"0125|SIM-Zuordnung abgebrochen, weil Quellwert NULL ist. ID={mapping.ID}, Info={info}, SourceNode={mapping.SourceNodeName ?? "-"}.");
                    throw new InvalidOperationException(
                        $"SIM-Zuordnung abgebrochen, weil Quellwert NULL ist. ID={mapping.ID}, Info={info}, SourceNode={mapping.SourceNodeName ?? "-"}");
                }

                object? targetValue = BegrenzeIstGewichtWennNoetig(mapping.TargetNode, sourceValue);
                Log($"007E|SIM-Zuordnung DIREKT: {info}, Wert={FormatOpcValue(targetValue)}.");
                WriteKranfahrtBeendetNodeNoLock(mapping.TargetNode, targetValue);
                geschrieben++;
                continue;
            }

            if (string.Equals(typ, "FIXWERT", StringComparison.OrdinalIgnoreCase))
            {
                Log($"007F|SIM-Zuordnung FIXWERT: {info}, Wert={mapping.Fixwert ?? "NULL"}.");
                WriteKranfahrtBeendetNodeNoLock(mapping.TargetNode, mapping.Fixwert);
                geschrieben++;
                continue;
            }

            uebersprungen++;
            LogWarning($"0081|SIM-Zuordnung hat unbekannten Typ. ID={mapping.ID}, Typ={mapping.Zuordnungstyp}, Info={info}.");
        }

        foreach (SimEventMappingConfiguration mapping in triggerMappings)
        {
            string info = mapping.Info ?? mapping.TargetNode.NodeName;

            if (!triggerErhoehen)
            {
                uebersprungen++;
                Log($"0080|SIM-Zuordnung Trigger bleibt noch unveraendert: {info}.");
                continue;
            }

            // Der Trigger wird bewusst erst nach allen Payload-Zuordnungen geschrieben.
            ErhoeheKranfahrtBeendetTriggerNoLock(mapping.TargetNode, info);
            geschrieben++;
        }

        Log($"0082|SIM-Zuordnungen fuer KranfahrtBeendet angewendet. Geschrieben={geschrieben}, Uebersprungen={uebersprungen}, TriggerErhoehen={triggerErhoehen}.");
    }
    private object? BegrenzeIstGewichtWennNoetig(
        EventNodeConfiguration targetNode,
        object? value)
    {
        if (!string.Equals(targetNode.NodeName, IstMasseNodeName, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value is null)
        {
            return value;
        }

        decimal istGewicht;
        try
        {
            istGewicht = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            LogWarning($"0126|IstGewicht konnte fuer Begrenzung nicht interpretiert werden. Wert={FormatOpcValue(value)}, Fehler={ex.GetType().Name}: {ex.Message}");
            return value;
        }

        if (aktiveSimulationsFahrt?.IstGewichtKg is decimal istGewichtAusQuelle)
        {
            Log($"0152|Istgewicht fuer KranfahrtBeendet stammt aus Quellenaufnahme: IstGewicht={istGewichtAusQuelle:0.###} kg, Ursprungswert={istGewicht:0.###} kg.");
            return istGewichtAusQuelle;
        }

        if (IstAktiveFahrtEinlagerfahrt())
        {
            decimal einlagerIstGewicht = ErzeugeEinlagerIstGewichtKg();
            if (aktiveSimulationsFahrt is not null)
            {
                aktiveSimulationsFahrt.IstGewichtKg = einlagerIstGewicht;
            }

            Log($"0153|Istgewicht war beim Eventversand noch nicht gesetzt. Fallback fuer Einlagerfahrt: IstGewicht={einlagerIstGewicht:0.###} kg, Bereich={EinlagerIstGewichtMinKg}..{EinlagerIstGewichtMaxKg} kg, Ursprungswert={istGewicht:0.###} kg.");
            return einlagerIstGewicht;
        }

        if (istGewicht <= MaxChargierIstGewichtKg)
        {
            return value;
        }

        Log($"0128|IstGewicht fuer KranfahrtBeendet wird auf maximal {MaxChargierIstGewichtKg:0.###} kg begrenzt. Ursprungswert={istGewicht:0.###} kg.");
        return MaxChargierIstGewichtKg;
    }

    private void AktualisiereIstGewichtBeimQuellenErreichen()
    {
        if (aktiveSimulationsFahrt is null)
        {
            return;
        }

        if (IstAktiveFahrtEinlagerfahrt())
        {
            decimal istGewicht = ErzeugeEinlagerIstGewichtKg();
            aktiveSimulationsFahrt.IstGewichtKg = istGewicht;
            masseNetto = (int)Math.Round(istGewicht, MidpointRounding.AwayFromZero);
            SetEvent203Value(
                MasseNettoNodeName,
                masseNetto.ToString(CultureInfo.InvariantCulture));
            Log($"0150|Istgewicht an Quelle ermittelt: Typ=EINLAGERN, IstGewicht={istGewicht:0.###} kg, Bereich={EinlagerIstGewichtMinKg}..{EinlagerIstGewichtMaxKg} kg, Quelle={aktiveSimulationsFahrt.QuelleBezeichnung} ({aktiveSimulationsFahrt.QuellePositionID}).");
            return;
        }

        decimal chargierIstGewicht = aktiveSimulationsFahrt.SollMengeKg > 0
            ? Math.Min(aktiveSimulationsFahrt.SollMengeKg, MaxChargierIstGewichtKg)
            : MaxChargierIstGewichtKg;

        aktiveSimulationsFahrt.IstGewichtKg = chargierIstGewicht;
        masseNetto = (int)Math.Round(chargierIstGewicht, MidpointRounding.AwayFromZero);
        SetEvent203Value(
            MasseNettoNodeName,
            masseNetto.ToString(CultureInfo.InvariantCulture));
        Log($"0151|Istgewicht an Quelle ermittelt: Typ=CHARGIEREN, IstGewicht={chargierIstGewicht:0.###} kg, SollMenge={aktiveSimulationsFahrt.SollMengeKg:0.###} kg, Max={MaxChargierIstGewichtKg:0.###} kg, Quelle={aktiveSimulationsFahrt.QuelleBezeichnung} ({aktiveSimulationsFahrt.QuellePositionID}).");
    }

    private decimal ErzeugeEinlagerIstGewichtKg()
    {
        lock (demoRandom)
        {
            return demoRandom.Next(EinlagerIstGewichtMinKg, EinlagerIstGewichtMaxKg + 1);
        }
    }

    private bool IstAktiveFahrtEinlagerfahrt()
    {
        if (aktiveSimulationsFahrt is null)
        {
            return false;
        }

        if (!positionenById.TryGetValue(aktiveSimulationsFahrt.QuellePositionID, out SimKranPosition? quelle)
            || !positionenById.TryGetValue(aktiveSimulationsFahrt.ZielPositionID, out SimKranPosition? ziel))
        {
            return false;
        }

        return string.Equals(quelle.PositionsTyp, "LKW_PLATZ", StringComparison.OrdinalIgnoreCase)
               && string.Equals(ziel.PositionsTyp, "LAGERBOX", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteKranfahrtBeendetNodeNoLock(string nodeName, object? value)
    {
        if (!TryGetKranfahrtBeendetNode(nodeName, out EventNodeConfiguration node))
        {
            LogWarning($"0083|KranfahrtBeendet.{nodeName} ist nicht konfiguriert. Wert wurde nicht geschrieben.");
            return;
        }

        WriteKranfahrtBeendetNodeNoLock(node, value);
    }

    private void WriteKranfahrtBeendetNodeNoLock(EventNodeConfiguration node, object? value)
    {
        object? converted = ConvertValueForOpc(value, node.DataType);
        OpcStatus status = opcClient!.WriteNode(node.OpcNode, converted);
        if (status.IsBad && ShouldTryNumericPositionFallback(node.NodeName, converted, value))
        {
            object numericFallback = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            LogWarning(
                $"0077|KranfahrtBeendet.{node.NodeName} wurde laut DB als {node.DataType} geschrieben, vom OPC-Server aber abgelehnt. " +
                $"Versuche denselben Wert numerisch. Node={node.OpcNode}, Wert={FormatOpcValue(converted)}, Status={status.Code}, Beschreibung={status.Description}");

            status = opcClient.WriteNode(node.OpcNode, numericFallback);
            if (!status.IsBad)
            {
                converted = numericFallback;
            }
        }

        if (status.IsBad)
        {
            throw new InvalidOperationException(
                $"OPC-Schreiben fehlgeschlagen. Event=KranfahrtBeendet, Variable={node.NodeName}, DB-Datentyp={node.DataType}, Node={node.OpcNode}, Wert={FormatOpcValue(converted)}, Status={status.Code}, Beschreibung={status.Description}");
        }

        SetEventValue(kranfahrtBeendetValues, node.NodeName, converted);
        LogOpcSend($"0070|OPC Senden KranfahrtBeendet: {node.NodeName}={FormatOpcValue(converted)}");
        LogOpcSend($"0071|OPC Schreiben vom OPC-Server angenommen. Event=KranfahrtBeendet, Variable={node.NodeName}, Node={node.OpcNode}, Wert={FormatOpcValue(converted)}, Status={status.Code}");
    }

    private void ErhoeheKranfahrtBeendetTriggerNoLock(
        EventNodeConfiguration triggerNode,
        string info)
    {
        int currentTelegramm = 0;
        try
        {
            OpcValue current = opcClient!.ReadNode(triggerNode.OpcNode);
            if (current.Status.IsGood && current.Value is not null)
            {
                currentTelegramm = Convert.ToInt32(current.Value, CultureInfo.InvariantCulture);
            }
            else if (!current.Status.IsGood)
            {
                LogWarning($"0072|KranfahrtBeendet Trigger konnte vor dem Erhoehen nicht gut gelesen werden. Node={triggerNode.OpcNode}, Status={current.Status.Code}, Beschreibung={current.Status.Description}. Starte bei 0.");
            }
        }
        catch (Exception ex)
        {
            LogWarning($"0129|KranfahrtBeendet Trigger konnte vor dem Erhoehen nicht gelesen werden. Starte bei 0. Node={triggerNode.OpcNode}, Fehler={ex.GetType().Name}: {ex.Message}");
        }

        int nextTelegramm = currentTelegramm == int.MaxValue ? 1 : currentTelegramm + 1;
        Log($"007C|KranfahrtBeendet: Trigger {info} wird von {currentTelegramm} auf {nextTelegramm} erhoeht.");
        object converted = ConvertValueForOpc(nextTelegramm, triggerNode.DataType) ?? nextTelegramm;
        OpcStatus writeStatus = opcClient!.WriteNode(triggerNode.OpcNode, converted);
        if (writeStatus.IsBad)
        {
            throw new InvalidOperationException(
                $"OPC-Schreiben fehlgeschlagen. Event=KranfahrtBeendet, Trigger={triggerNode.NodeName}, Node={triggerNode.OpcNode}, Wert={FormatOpcValue(converted)}, Status={writeStatus.Code}, Beschreibung={writeStatus.Description}");
        }

        SetEventValue(kranfahrtBeendetValues, triggerNode.NodeName, converted);
        LogOpcSend($"0073|OPC Senden KranfahrtBeendet Trigger: {triggerNode.NodeName}={FormatOpcValue(converted)}");
        LogOpcSend($"0074|OPC Schreiben vom OPC-Server angenommen. Event=KranfahrtBeendet, Trigger={triggerNode.NodeName}, Node={triggerNode.OpcNode}, Wert={FormatOpcValue(converted)}, Status={writeStatus.Code}");
        Log($"012A|KranfahrtBeendet Trigger gesendet. {info}, NeuerWert={FormatOpcValue(converted)}.");
    }

    private void SendeKranfahrtBeendetTelegramm()
    {
        Dictionary<string, object?> auftragPayload;
        lock (letzteKranfahrtAuftragPayload)
        {
            auftragPayload = new Dictionary<string, object?>(letzteKranfahrtAuftragPayload, StringComparer.OrdinalIgnoreCase);
        }

        lock (opcSyncRoot)
        {
            if (opcClient?.State != OpcClientState.Connected)
            {
                LogWarning("0078|KranfahrtBeendet kann nicht gesendet werden, weil OPC nicht verbunden ist.");
                return;
            }

            Log($"0079|KranfahrtBeendet Telegrammaufbau gestartet. Auftragspayload-Werte={auftragPayload.Count}, Zuordnungen={kranfahrtBeendetZuordnungen.Count}.");
            SchreibeKranfahrtBeendetZuordnungNoLock(auftragPayload, triggerErhoehen: true);
            Log("012B|KranfahrtBeendet gesendet. Alle aktiven SIM-Zuordnungen inklusive Trigger wurden verarbeitet.");
        }
    }

    private void PruefeUndSendeLkwPlatzLeerEvent(AktuelleFahrtSimulation fahrt)
    {
        if (!positionenById.TryGetValue(fahrt.QuellePositionID, out SimKranPosition? quelle)
            || !string.Equals(quelle.PositionsTyp, "LKW_PLATZ", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int lkwPlatzPositionId = checked((int)quelle.PositionID);

        if (!einlagerLeerSimulationen.TryGetValue(fahrt.AuftragID, out EinlagerLeerSimulationState? state))
        {
            int zielFahrten;
            lock (demoRandom)
            {
                zielFahrten = demoRandom.Next(5, 9);
            }

            state = new EinlagerLeerSimulationState(zielFahrten);
            einlagerLeerSimulationen[fahrt.AuftragID] = state;
            Log($"01B7|Event_207 Simulation initialisiert: Auftrag={fahrt.AuftragID}, LkwPlatzPositionID={lkwPlatzPositionId}, Leer-Meldung nach insgesamt {zielFahrten} tatsaechlichen Fahrten.");
        }

        EinlagerTeilfahrtStand? datenbankStand = LadeEinlagerTeilfahrtStand(
            fahrt.AuftragID,
            fahrt.AuftragTeilfahrt);
        int lokalMitAktuellerFahrt = state.AbgeschlosseneFahrten == int.MaxValue
            ? int.MaxValue
            : state.AbgeschlosseneFahrten + 1;

        if (datenbankStand is not null)
        {
            long tatsaechlicherStand = datenbankStand.HistorisierteTeilfahrten
                + (datenbankStand.AktuelleTeilfahrtBereitsHistorisiert ? 0L : 1L);
            int datenbankMitAktuellerFahrt = tatsaechlicherStand >= int.MaxValue
                ? int.MaxValue
                : (int)tatsaechlicherStand;
            state.AbgeschlosseneFahrten = Math.Max(
                lokalMitAktuellerFahrt,
                datenbankMitAktuellerFahrt);

            Log($"01BF|Event_207 Teilfahrtstand aus Datenbank uebernommen: Auftrag={fahrt.AuftragID}, Historisiert={datenbankStand.HistorisierteTeilfahrten}, AktuelleTeilfahrt={fahrt.AuftragTeilfahrt}, AktuelleBereitsHistorisiert={datenbankStand.AktuelleTeilfahrtBereitsHistorisiert}, Gesamtstand={state.AbgeschlosseneFahrten}.");
        }
        else
        {
            state.AbgeschlosseneFahrten = lokalMitAktuellerFahrt;
        }

        Log($"01B8|Einlagerfahrt fuer Event_207 gezaehlt: Auftrag={fahrt.AuftragID}, Teilfahrt={fahrt.AuftragTeilfahrt}, LkwPlatzPositionID={lkwPlatzPositionId}, Stand={state.AbgeschlosseneFahrten}/{state.ZielFahrten}.");

        if (state.EventGesendet || state.AbgeschlosseneFahrten < state.ZielFahrten)
        {
            return;
        }

        lock (opcSyncRoot)
        {
            if (opcClient?.State != OpcClientState.Connected)
            {
                LogWarning($"01B9|Event_207 kann noch nicht gesendet werden, weil OPC nicht verbunden ist. Auftrag={fahrt.AuftragID}, LkwPlatzPositionID={lkwPlatzPositionId}.");
                return;
            }

            EventNodeConfiguration trigger = GetRequiredEvent207Node(Event207TriggerNodeName);
            WriteEvent207NodeNoLock(GetRequiredEvent207Node(AuftragNummerNodeName), fahrt.AuftragID);
            WriteEvent207NodeNoLock(GetRequiredEvent207Node(AuftragTeilfahrtNodeName), fahrt.AuftragTeilfahrt);
            WriteEvent207NodeNoLock(GetRequiredEvent207Node(Event207LkwPlatzNodeName), lkwPlatzPositionId);
            int triggerValue = IncrementEvent207TriggerNoLock(trigger);

            state.EventGesendet = true;
            Log($"01BA|Event_207 gesendet: Auftrag={fahrt.AuftragID}, Teilfahrt={fahrt.AuftragTeilfahrt}, LkwPlatzPositionID={lkwPlatzPositionId}, Trigger={triggerValue}, Fahrten={state.AbgeschlosseneFahrten}.");
        }
    }

    private void BunkerMaterialAnfordern_Click(object sender, RoutedEventArgs e)
    {
        SendeEvent204Anforderung("Manuelle Anforderung");
    }

    private bool SendeEvent204Anforderung(string grund)
    {
        int theoretischerTrigger = event204AnforderungsZaehler == int.MaxValue
            ? 0
            : event204AnforderungsZaehler + 1;
        event204AnforderungsZaehler = theoretischerTrigger;
        Log($"0212|Event_204-Anforderung ist im Simulator derzeit deaktiviert. Telegramm waere jetzt gekommen: Grund={grund}, Zeitpunkt={DateTime.Now:dd.MM.yyyy HH:mm:ss}, TheoretischerTrigger={theoretischerTrigger}.");
        return false;

        // return SendeTriggerAnforderung(
        //     event204Nodes,
        //     event204Values,
        //     Event204TriggerNodeName,
        //     ref event204AnforderungsZaehler,
        //     "Event_204",
        //     "01E3",
        //     "01E4",
        //     grund);
    }

    private void MaterialEigenschaftenAnfordern_Click(object sender, RoutedEventArgs e)
    {
        SendeEvent205Anforderung("Manuelle Anforderung");
    }

    private bool SendeEvent205Anforderung(string grund)
    {
        int theoretischerTrigger = event205AnforderungsZaehler == int.MaxValue
            ? 0
            : event205AnforderungsZaehler + 1;
        event205AnforderungsZaehler = theoretischerTrigger;
        Log($"0213|Event_205-Anforderung ist im Simulator derzeit deaktiviert. Telegramm waere jetzt gekommen: Grund={grund}, Zeitpunkt={DateTime.Now:dd.MM.yyyy HH:mm:ss}, Fahrzustand={fahrzustand}, AuftragID={aktiveSimulationsFahrt?.AuftragID}, Teilfahrt={aktiveSimulationsFahrt?.AuftragTeilfahrt}, TheoretischerTrigger={theoretischerTrigger}.");
        return false;

        // if (IstKranGeradeAktiv())
        // {
        //     LogWarning($"020D|Event_205 wird nicht gesendet, weil der Kran gerade aktiv ist. Grund={grund}, Fahrzustand={fahrzustand}, AuftragID={aktiveSimulationsFahrt?.AuftragID}, Teilfahrt={aktiveSimulationsFahrt?.AuftragTeilfahrt}.");
        //     return false;
        // }

        // return SendeTriggerAnforderung(
        //     event205Nodes,
        //     event205Values,
        //     Event205TriggerNodeName,
        //     ref event205AnforderungsZaehler,
        //     "Event_205",
        //     "020E",
        //     "020F",
        //     grund);
    }

    private bool IstKranGeradeAktiv()
    {
        return aktiveSimulationsFahrt is not null
               || aktuelleBewegung is not null
               || fahrzustand is SimulationsFahrzustand.FahreZurQuelle or SimulationsFahrzustand.FahreZumZiel;
    }
    private void KranPositionenAnfordern_Click(object sender, RoutedEventArgs e)
    {
        SendeEvent206Anforderung("Manuelle Anforderung");
    }

    private bool SendeEvent206Anforderung(string grund)
    {
        int theoretischerTrigger = event206AnforderungsZaehler == int.MaxValue
            ? 0
            : event206AnforderungsZaehler + 1;
        event206AnforderungsZaehler = theoretischerTrigger;
        Log($"0214|Event_206-Anforderung ist im Simulator derzeit deaktiviert. Telegramm waere jetzt gekommen: Grund={grund}, Zeitpunkt={DateTime.Now:dd.MM.yyyy HH:mm:ss}, TheoretischerTrigger={theoretischerTrigger}.");
        return false;

        // return SendeTriggerAnforderung(
        //     event206Nodes,
        //     event206Values,
        //     Event206TriggerNodeName,
        //     ref event206AnforderungsZaehler,
        //     "Event_206",
        //     "01FB",
        //     "01FC",
        //     grund);
    }

    private bool SendeTriggerAnforderung(
        IReadOnlyList<EventNodeConfiguration> nodes,
        Dictionary<string, string> values,
        string triggerNodeName,
        ref int anforderungsZaehler,
        string eventName,
        string successLogCode,
        string errorLogCode,
        string grund)
    {
        try
        {
            EventNodeConfiguration trigger = nodes.First(
                node => string.Equals(node.NodeName, triggerNodeName, StringComparison.OrdinalIgnoreCase));

            lock (opcSyncRoot)
            {
                if (opcClient?.State != OpcClientState.Connected)
                {
                    throw new InvalidOperationException("OPC ist nicht verbunden.");
                }

                if (anforderungsZaehler == 0)
                {
                    OpcValue current = opcClient.ReadNode(trigger.OpcNode);
                    if (current.Status.IsGood && current.Value is not null)
                    {
                        anforderungsZaehler = Convert.ToInt32(
                            current.Value,
                            CultureInfo.InvariantCulture);
                    }
                }

                anforderungsZaehler = anforderungsZaehler == int.MaxValue
                    ? 1
                    : anforderungsZaehler + 1;
                OpcStatus status = opcClient.WriteNode(trigger.OpcNode, anforderungsZaehler);
                if (status.IsBad)
                {
                    throw new InvalidOperationException(
                        $"OPC-Schreiben abgelehnt. Status={status.Code}, Beschreibung={status.Description}");
                }
            }

            SetEventValue(values, triggerNodeName, anforderungsZaehler);
            Log($"{successLogCode}|{eventName} gesendet: AnforderungsZaehler={anforderungsZaehler}, Grund={grund}.");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"{errorLogCode}|{eventName} konnte nicht gesendet werden. Grund={grund}, Fehler={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private EinlagerTeilfahrtStand? LadeEinlagerTeilfahrtStand(
        long einlagerAuftragId,
        int auftragTeilfahrt)
    {
        try
        {
            using var connection = new SqlConnection(databaseConnectionString);
            using var command = new SqlCommand(
                "dbo.FALCOM_SIM_GetEinlagerTeilfahrtStand",
                connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure,
                CommandTimeout = 10
            };

            command.Parameters.Add("@EinlagerAuftragID", System.Data.SqlDbType.BigInt).Value = einlagerAuftragId;
            command.Parameters.Add("@AuftragTeilfahrt", System.Data.SqlDbType.Int).Value = auftragTeilfahrt;

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                LogError($"01C0|Datenbank lieferte keinen Event_207-Teilfahrtstand. Auftrag={einlagerAuftragId}, Teilfahrt={auftragTeilfahrt}. Es wird mit dem internen Zaehler weitergearbeitet.");
                return null;
            }

            return new EinlagerTeilfahrtStand(
                Convert.ToInt64(reader["HistorisierteTeilfahrten"], CultureInfo.InvariantCulture),
                Convert.ToBoolean(reader["AktuelleTeilfahrtBereitsHistorisiert"], CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            LogError($"01C1|Event_207-Teilfahrtstand konnte nicht aus der Datenbank gelesen werden. Auftrag={einlagerAuftragId}, Teilfahrt={auftragTeilfahrt}, Fehler={ex.GetType().Name}: {ex.Message}. Es wird mit dem internen Zaehler weitergearbeitet.");
            return null;
        }
    }

    private EventNodeConfiguration GetRequiredEvent207Node(string nodeName)
    {
        EventNodeConfiguration? node = event207Nodes.FirstOrDefault(
            candidate => string.Equals(candidate.NodeName, nodeName, StringComparison.OrdinalIgnoreCase));
        return node ?? throw new InvalidOperationException(
            $"{Event207Name}.{nodeName} ist in der Datenbank nicht konfiguriert.");
    }

    private void WriteEvent207NodeNoLock(EventNodeConfiguration node, object value)
    {
        object converted = ConvertValueForOpc(value, node.DataType) ?? value;
        OpcStatus status = opcClient!.WriteNode(node.OpcNode, converted);
        if (status.IsBad
            && string.Equals(
                node.NodeName,
                Event207LkwPlatzNodeName,
                StringComparison.OrdinalIgnoreCase)
            && converted is not string)
        {
            string stringFallback = Convert.ToString(
                value,
                CultureInfo.InvariantCulture) ?? string.Empty;
            LogWarning(
                $"01C2|Event_207.LKWPlatz akzeptiert den Integer aktuell nicht. Der Zahlenwert wird tolerant als String geschrieben. Node={node.OpcNode}, Wert={stringFallback}, Status={status.Code}, Beschreibung={status.Description}");

            status = opcClient.WriteNode(node.OpcNode, stringFallback);
            if (!status.IsBad)
            {
                converted = stringFallback;
                LogOpcSend(
                    $"01C3|OPC Schreiben vom OPC-Server als String angenommen. Event={Event207Name}, Variable={node.NodeName}, Node={node.OpcNode}, Wert={FormatOpcValue(converted)}, Status={status.Code}");
            }
        }

        if (status.IsBad)
        {
            throw new InvalidOperationException(
                $"OPC-Schreiben fehlgeschlagen. Event={Event207Name}, Variable={node.NodeName}, Node={node.OpcNode}, Wert={FormatOpcValue(converted)}, Status={status.Code}, Beschreibung={status.Description}");
        }

        LogOpcSend($"01BB|OPC Schreiben vom OPC-Server angenommen. Event={Event207Name}, Variable={node.NodeName}, Node={node.OpcNode}, Wert={FormatOpcValue(converted)}, Status={status.Code}");
    }

    private int IncrementEvent207TriggerNoLock(EventNodeConfiguration trigger)
    {
        int current = 0;
        OpcValue value = opcClient!.ReadNode(trigger.OpcNode);
        if (value.Status.IsGood && value.Value is not null)
        {
            current = Convert.ToInt32(value.Value, CultureInfo.InvariantCulture);
        }

        int next = current == int.MaxValue ? 1 : current + 1;
        WriteEvent207NodeNoLock(trigger, next);
        return next;
    }

    private bool TryGetKranfahrtAuftragNode(string nodeName, out EventNodeConfiguration node)
    {
        return kranfahrtAuftragNodesByName.TryGetValue(nodeName, out node!);
    }

    private bool TryGetKranfahrtBeendetNode(string nodeName, out EventNodeConfiguration node)
    {
        return kranfahrtBeendetNodesByName.TryGetValue(nodeName, out node!);
    }
    private static bool ShouldTryNumericPositionFallback(
        string nodeName,
        object? convertedValue,
        object? originalValue)
    {
        if (!string.Equals(nodeName, "Quelle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(nodeName, "Ziel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (convertedValue is not string)
        {
            return false;
        }

        return int.TryParse(
            Convert.ToString(originalValue, CultureInfo.InvariantCulture),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out _);
    }

    private static string FormatOpcValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        return value is string text
            ? $"\"{text}\" (String)"
            : $"{Convert.ToString(value, CultureInfo.InvariantCulture)} ({value.GetType().Name})";
    }

    private static object? ConvertValueForOpc(object? value, string dataType)
    {
        if (value is null)
        {
            return null;
        }

        return dataType.Trim().ToUpperInvariant() switch
        {
            "INT16" => Convert.ToInt16(value, CultureInfo.InvariantCulture),
            "INT32" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            "INT64" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            "DOUBLE" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            "FLOAT" or "SINGLE" => Convert.ToSingle(value, CultureInfo.InvariantCulture),
            "BOOLEAN" or "BOOL" => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
            "STRING" => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value
        };
    }

    private void SetEventValues(
        Dictionary<string, string> target,
        IReadOnlyDictionary<string, object?> values)
    {
        lock (target)
        {
            foreach (KeyValuePair<string, object?> item in values)
            {
                target[item.Key] = Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        Dispatcher.BeginInvoke(RefreshEventView);
    }

    private void SetEventValue(
        Dictionary<string, string> target,
        string nodeName,
        object? value)
    {
        lock (target)
        {
            target[nodeName] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        Dispatcher.BeginInvoke(RefreshEventView);
    }
    private void StartLebensZaehlerLoop()
    {
        StartEvent203Loop();
        StartEvent201Loop();
        StartEvent204AnforderungsLoop();
        StartEvent205AnforderungsLoop();
        StartEvent206AnforderungsLoop();
    }

    private void StartEvent204AnforderungsLoop()
    {
        if (!event204Nodes.Any(
                node => string.Equals(node.NodeName, Event204TriggerNodeName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(node.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(node.OpcNode)))
        {
            LogError("01E5|Event_204.Event_204 ist nicht gueltig konfiguriert. Die minuetliche Bunkerdaten-Anforderung wird nicht gestartet.");
            return;
        }

        _ = Task.Run(
            async () =>
            {
                CancellationToken cancellationToken = lebensZaehlerCancellation.Token;

                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
                    while (await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        if (opcClient?.State != OpcClientState.Connected)
                        {
                            LogWarning("01E6|Minuetliche Event_204-Anforderung ausgesetzt, weil OPC nicht verbunden ist.");
                            continue;
                        }

                        SendeEvent204Anforderung("Minuetliche automatische Anforderung");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            },
            lebensZaehlerCancellation.Token);
    }
    private void StartEvent205AnforderungsLoop()
    {
        if (!event205Nodes.Any(
                node => string.Equals(node.NodeName, Event205TriggerNodeName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(node.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(node.OpcNode)))
        {
            LogError("0210|Event_205.Event_205 ist nicht gueltig konfiguriert. Die minuetliche Materialeigenschaften-Anforderung wird nicht gestartet.");
            return;
        }

        _ = Task.Run(
            async () =>
            {
                CancellationToken cancellationToken = lebensZaehlerCancellation.Token;

                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
                    while (await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        if (opcClient?.State != OpcClientState.Connected)
                        {
                            LogWarning("0211|Minuetliche Event_205-Anforderung ausgesetzt, weil OPC nicht verbunden ist.");
                            continue;
                        }

                        SendeEvent205Anforderung("Minuetliche automatische Anforderung");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            },
            lebensZaehlerCancellation.Token);
    }    private void StartEvent206AnforderungsLoop()
    {
        if (!event206Nodes.Any(
                node => string.Equals(node.NodeName, Event206TriggerNodeName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(node.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(node.OpcNode)))
        {
            LogError("01FD|Event_206.Event_206 ist nicht gueltig konfiguriert. Die minuetliche Kranpositions-Anforderung wird nicht gestartet.");
            return;
        }

        _ = Task.Run(
            async () =>
            {
                CancellationToken cancellationToken = lebensZaehlerCancellation.Token;

                try
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
                    while (await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        if (opcClient?.State != OpcClientState.Connected)
                        {
                            LogWarning("01FE|Minuetliche Event_206-Anforderung ausgesetzt, weil OPC nicht verbunden ist.");
                            continue;
                        }

                        SendeEvent206Anforderung("Minuetliche automatische Anforderung");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            },
            lebensZaehlerCancellation.Token);
    }

    private void StartEvent203Loop()
    {
        bool triggerConfigured = event203Nodes.Any(
            node => string.Equals(node.NodeName, Event203TriggerNodeName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(node.NodeRole, "Trigger", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(node.OpcNode));
        if (!triggerConfigured)
        {
            LogError("01A0|Event_203.Event_203 ist in der Datenbank nicht gueltig konfiguriert. Der zyklische Status-Trigger wird nicht geschrieben.");
            return;
        }

        _ = Task.Run(
            async () =>
            {
                CancellationToken cancellationToken = lebensZaehlerCancellation.Token;

                try
                {
                    while (!disposed && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                        if (opcClient?.State != OpcClientState.Connected)
                        {
                            continue;
                        }

                        AktualisiereFahrposition(DateTime.UtcNow);
                        int event203Value = NextEvent203Zaehler();

                        try
                        {
                            lock (opcSyncRoot)
                            {
                                if (opcClient?.State != OpcClientState.Connected)
                                {
                                    continue;
                                }

                                WriteEvent203PayloadNodesNoLock();
                                WriteEvent203NodeNoLock(Event203TriggerNodeName, event203Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            SetOpcReconnectFromBackground("Reconnect laeuft");
                            StartBackgroundReconnectLoop("Event_203 konnte nicht geschrieben werden");

                            if (DateTime.UtcNow >= nextEvent203ErrorLogUtc)
                            {
                                LogError(
                                    $"01A1|Event_203 konnte nicht geschrieben werden. Zaehler={event203Value}, Fehler={ex.GetType().Name}: {ex.Message}");
                                nextEvent203ErrorLogUtc = DateTime.UtcNow.AddMinutes(1);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            },
            lebensZaehlerCancellation.Token);
    }

    private void StartEvent201Loop()
    {
        if (string.IsNullOrWhiteSpace(event201NodeId))
        {
            return;
        }

        _ = Task.Run(
            async () =>
            {
                CancellationToken cancellationToken = lebensZaehlerCancellation.Token;

                try
                {
                    while (!disposed && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                        if (opcClient?.State != OpcClientState.Connected)
                        {
                            continue;
                        }

                        int value = NextSpsLebensZaehler();

                        try
                        {
                            lock (opcSyncRoot)
                            {
                                if (opcClient?.State != OpcClientState.Connected)
                                {
                                    continue;
                                }

                                OpcStatus status = opcClient.WriteNode(
                                    event201NodeId,
                                    value);

                                if (status.IsBad)
                                {
                                    throw new InvalidOperationException(
                                        $"OPC-Schreiben fehlgeschlagen. Node={event201NodeId}, Status={status.Code}, Beschreibung={status.Description}");
                                }

                                LogOpcSend($"012C|OPC Schreiben vom OPC-Server angenommen. Event=Event_201, Node={event201NodeId}, Wert={value}, Status={status.Code}");
                            }

                            letzterSpsLebensZaehler = value;
                            letzterSpsLebensZaehlerGesendetAm = DateTime.Now;
                            SetSpsLebensZaehlerFromBackground(value, letzterSpsLebensZaehlerGesendetAm.Value);
                            LogOpcSend($"012D|OPC Senden Event_201: Node={event201NodeId}, Wert={value}");
                        }
                        catch (Exception ex)
                        {
                            SetOpcReconnectFromBackground("Reconnect laeuft");
                            StartBackgroundReconnectLoop("Event_201 konnte nicht geschrieben werden");

                            if (DateTime.UtcNow >= nextLebensZaehlerErrorLogUtc)
                            {
                                LogError(
                                    $"SPS->FALCOM Event_201 konnte nicht geschrieben werden. Node={event201NodeId}, Wert={value}, Fehler={ex.GetType().Name}: {ex.Message}");
                                nextLebensZaehlerErrorLogUtc = DateTime.UtcNow.AddMinutes(1);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            },
            lebensZaehlerCancellation.Token);
    }

    private void FahreGrundstellungAn()
    {
        posKranX = grundstellung.PosKranX;
        posKatzeY = grundstellung.PosKatzeY;
        posHubZ = grundstellung.PosHubZ;

        SetEvent203Value(
            PosKranNodeName,
            posKranX.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value(
            PosKatzeNodeName,
            posKatzeY.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value(
            PosHubNodeName,
            posHubZ.ToString(CultureInfo.InvariantCulture));
        masseNetto = 0;
        SetEvent203Value(
            MasseNettoNodeName,
            masseNetto.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value(Event203TriggerNodeName, event203Zaehler.ToString(CultureInfo.InvariantCulture));

        SetMagnetAn(0, "Grundstellung angefahren");

        Log(
            "012E|Grundstellung angefahren: " +
            $"PosKran={posKranX}, PosKatze={posKatzeY}, PosHub={posHubZ}. " +
            "Position liegt auf der konfigurierten Grundstellung.");
    }

    private void DemoModeButton_Click(object sender, RoutedEventArgs e)
    {
        demoModeAktiv = !demoModeAktiv;
        DemoModeButton.Content = demoModeAktiv
            ? "Demo stoppen"
            : "Simulation/Demo";
        DemoModeButton.Background = demoModeAktiv
            ? Brushes.DarkOrange
            : Brushes.Transparent;

        if (demoModeAktiv)
        {
            Log("008C|Demo-Modus gestartet. Der Simulator schreibt ausschliesslich Kranpositionen. Event-1/Event-2-Quellen, Ziele und Trigger werden nicht geschrieben.");
            StarteNaechsteDemoFahrt(DateTime.UtcNow);
            return;
        }

        Log("008D|Demo-Modus beendet. Konfigurierte Grundstellung wird angefahren.");
        aktiveSimulationsFahrt = null;
        aktuelleBewegung = null;
        warteAufNeueFahrtBisUtc = null;
        StarteBewegung(
            grundstellung,
            DateTime.UtcNow,
            SimulationsFahrzustand.FahreZurGrundstellung,
            "012F|Demo-Modus beendet. Konfigurierte Grundstellung wird angefahren.");
    }

    private void StarteNaechsteDemoFahrt(DateTime nowUtc)
    {
        AktuelleFahrtSimulation? fahrt = ErzeugeDemoFahrt();
        if (fahrt is null)
        {
            LogWarning("0130|Demo-Modus kann keine plausible Fahrt bilden. Es fehlen Lagerboxen, LKW-Plaetze oder Chargierwagen in FALCOM_KRAN_POSITION.");
            demoModeAktiv = false;
            Dispatcher.BeginInvoke(() =>
            {
                DemoModeButton.Content = "Simulation/Demo";
                DemoModeButton.Background = Brushes.Transparent;
            });
            return;
        }

        StarteFahrtZurQuelle(fahrt, nowUtc);
    }

    private AktuelleFahrtSimulation? ErzeugeDemoFahrt()
    {
        List<SimKranPosition> lagerboxen = GetDemoPositionen("LAGERBOX");
        List<SimKranPosition> lkwPlaetze = GetDemoPositionen("LKW_PLATZ");
        List<SimKranPosition> chargierwagen = GetDemoPositionen("CHARGIERWAGEN");

        var moeglicheFahrten = new List<(SimKranPosition Quelle, SimKranPosition Ziel, string Typ)>();
        foreach (SimKranPosition lagerbox in lagerboxen)
        {
            foreach (SimKranPosition cw in chargierwagen)
            {
                moeglicheFahrten.Add((lagerbox, cw, "Lagerbox -> Chargierwagen"));
            }
        }

        foreach (SimKranPosition lkw in lkwPlaetze)
        {
            foreach (SimKranPosition lagerbox in lagerboxen)
            {
                moeglicheFahrten.Add((lkw, lagerbox, "LKW -> Lagerbox"));
            }
        }

        if (moeglicheFahrten.Count == 0)
        {
            return null;
        }

        (SimKranPosition quelle, SimKranPosition ziel, string typ) =
            moeglicheFahrten[demoRandom.Next(moeglicheFahrten.Count)];

        return new AktuelleFahrtSimulation(
            naechsteInterneFahrtId++,
            DemoTelegrammNummer,
            $"DEMO {typ}",
            0,
            0,
            "DEMO",
            quelle.PositionID,
            ziel.PositionID,
            0,
            0,
            0,
            quelle.Bezeichnung,
            ziel.Bezeichnung,
            quelle.Position,
            ziel.Position);
    }

    private List<SimKranPosition> GetDemoPositionen(string positionsTyp)
    {
        return positionenById.Values
            .Where(position => string.Equals(position.PositionsTyp, positionsTyp, StringComparison.OrdinalIgnoreCase))
            .OrderBy(position => position.PositionsNr)
            .ToList();
    }

    private void AktualisiereFahrposition(DateTime nowUtc)
    {
        if (aktuelleBewegung is not null)
        {
            AktualisiereAktuelleBewegung(nowUtc);
            return;
        }

        if (fahrzustand == SimulationsFahrzustand.WarteAufNeueFahrt)
        {
            if (warteAufNeueFahrtBisUtc is not null && nowUtc >= warteAufNeueFahrtBisUtc.Value)
            {
                StarteBewegung(
                    grundstellung,
                    nowUtc,
                    SimulationsFahrzustand.FahreZurGrundstellung,
                    "0131|Nach 10 Sekunden ohne neue Fahrt wird die konfigurierte Grundstellung angefahren.");
            }

            return;
        }
    }

    private void StarteFahrtZurQuelle(
        AktuelleFahrtSimulation fahrt,
        DateTime nowUtc)
    {
        aktiveSimulationsFahrt = fahrt;
        warteAufNeueFahrtBisUtc = null;
        StarteBewegung(
            fahrt.Quelle,
            nowUtc,
            SimulationsFahrzustand.FahreZurQuelle,
            "0068|Aktuelle Fahrt wird abgefahren: " +
            $"SimulatorFahrtID={fahrt.ID}, TelegrammNummer={fahrt.TelegrammNummer}, AuftragID={fahrt.AuftragID}, Teilfahrt={fahrt.AuftragTeilfahrt}, " +
            $"Quelle={fahrt.QuelleBezeichnung} ({fahrt.QuellePositionID}), QuelleUnterPos={fahrt.QuelleUnterposition}, " +
            $"Ziel={fahrt.ZielBezeichnung} ({fahrt.ZielPositionID}), ZielUnterPos={fahrt.ZielUnterposition}.");
    }

    private void AktualisiereAktuelleBewegung(DateTime nowUtc)
    {
        KranMovement movement = aktuelleBewegung!;
        double progress = Math.Clamp(
            (nowUtc - movement.StartUtc).TotalSeconds / movement.Duration.TotalSeconds,
            0.0,
            1.0);

        posKranX = InterpolateInt(
            movement.Start.PosKranX,
            movement.Target.PosKranX,
            progress);
        posKatzeY = InterpolateInt(
            movement.Start.PosKatzeY,
            movement.Target.PosKatzeY,
            progress);
        posHubZ = InterpolateInt(
            movement.Start.PosHubZ,
            movement.Target.PosHubZ,
            progress);

        if (progress < 1.0)
        {
            return;
        }

        aktuelleBewegung = null;

        if (fahrzustand == SimulationsFahrzustand.FahreZurQuelle && aktiveSimulationsFahrt is not null)
        {
            AktualisiereIstGewichtBeimQuellenErreichen();

            SetMagnetAn(
                1,
                $"Quelle erreicht: {aktiveSimulationsFahrt.QuelleBezeichnung} ({aktiveSimulationsFahrt.QuellePositionID}), QuelleUnterPos={aktiveSimulationsFahrt.QuelleUnterposition}, Ziel={aktiveSimulationsFahrt.ZielBezeichnung} ({aktiveSimulationsFahrt.ZielPositionID}), ZielUnterPos={aktiveSimulationsFahrt.ZielUnterposition}");

            StarteBewegung(
                aktiveSimulationsFahrt.Ziel,
                nowUtc,
                SimulationsFahrzustand.FahreZumZiel,
                "0069|Quelle erreicht. Ziel wird angefahren: " +
                $"{aktiveSimulationsFahrt.ZielBezeichnung} ({aktiveSimulationsFahrt.ZielPositionID}), ZielUnterPos={aktiveSimulationsFahrt.ZielUnterposition}.");
            return;
        }

        if (fahrzustand == SimulationsFahrzustand.FahreZumZiel && aktiveSimulationsFahrt is not null)
        {
            SetMagnetAn(
                0,
                $"Ziel erreicht: {aktiveSimulationsFahrt.ZielBezeichnung} ({aktiveSimulationsFahrt.ZielPositionID}), ZielUnterPos={aktiveSimulationsFahrt.ZielUnterposition}, Quelle={aktiveSimulationsFahrt.QuelleBezeichnung} ({aktiveSimulationsFahrt.QuellePositionID}), QuelleUnterPos={aktiveSimulationsFahrt.QuelleUnterposition}");

            if (demoModeAktiv)
            {
                Log(
                    "0132|Demo-Ziel erreicht. Es wurde kein KranfahrtBeendet-Event gesendet. " +
                    $"Quelle={aktiveSimulationsFahrt.QuelleBezeichnung} ({aktiveSimulationsFahrt.QuellePositionID}), QuelleUnterPos={aktiveSimulationsFahrt.QuelleUnterposition}, " +
                    $"Ziel={aktiveSimulationsFahrt.ZielBezeichnung} ({aktiveSimulationsFahrt.ZielPositionID}), ZielUnterPos={aktiveSimulationsFahrt.ZielUnterposition}.");
                aktiveSimulationsFahrt = null;
                StarteNaechsteDemoFahrt(nowUtc);
                return;
            }

            letzteAbgefahreneFahrt = aktiveSimulationsFahrt;
            Log(
                "006A|Ziel erreicht. Fahrt intern abgefahren: " +
                $"SimulatorFahrtID={aktiveSimulationsFahrt.ID}, TelegrammNummer={aktiveSimulationsFahrt.TelegrammNummer}, " +
                $"AuftragID={aktiveSimulationsFahrt.AuftragID}, Teilfahrt={aktiveSimulationsFahrt.AuftragTeilfahrt}. " +
                "KranfahrtBeendet wird jetzt vorbereitet.");

            try
            {
                Log(
                    "0075|KranfahrtBeendet wird vorbereitet: " +
                    $"FahrtID={aktiveSimulationsFahrt.ID}, AuftragID={aktiveSimulationsFahrt.AuftragID}, " +
                    $"Quelle={aktiveSimulationsFahrt.QuellePositionID}, QuelleUnterPos={aktiveSimulationsFahrt.QuelleUnterposition}, " +
                    $"Ziel={aktiveSimulationsFahrt.ZielPositionID}, ZielUnterPos={aktiveSimulationsFahrt.ZielUnterposition}.");
                SendeKranfahrtBeendetTelegramm();
                try
                {
                    PruefeUndSendeLkwPlatzLeerEvent(aktiveSimulationsFahrt);
                }
                catch (Exception ex)
                {
                    LogError($"01BC|Event_207 konnte nicht gesendet werden. Auftrag={aktiveSimulationsFahrt.AuftragID}, Teilfahrt={aktiveSimulationsFahrt.AuftragTeilfahrt}, Fehler={ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogError($"0076|KranfahrtBeendet konnte nicht gesendet werden. Fehler={ex.GetType().Name}: {ex.Message}");
            }

            aktiveSimulationsFahrt = null;
            fahrzustand = SimulationsFahrzustand.WarteAufNeueFahrt;
            warteAufNeueFahrtBisUtc = nowUtc.AddSeconds(10);
            Log(
                "0133|Warten auf naechste Fahrt. " +
                $"Letzte Fahrt war TelegrammNummer={letzteAbgefahreneFahrt.TelegrammNummer}, " +
                $"AuftragID={letzteAbgefahreneFahrt.AuftragID}, Teilfahrt={letzteAbgefahreneFahrt.AuftragTeilfahrt}. " +
                "Wenn 10 Sekunden nichts Neues kommt, wird die konfigurierte Grundstellung angefahren.");
            Dispatcher.BeginInvoke(() =>
            {
                SimulationStatusText.Text = "Warte auf neue Fahrt";
                SimulationDetailText.Text = "Fahrt abgefahren. Wenn 10 Sekunden nichts Neues kommt, wird die konfigurierte Grundstellung angefahren.";
            });
            return;
        }

        if (fahrzustand == SimulationsFahrzustand.FahreZurGrundstellung)
        {
            fahrzustand = SimulationsFahrzustand.Grundstellung;
            Log("006B|Konfigurierte Grundstellung erreicht.");
            Dispatcher.BeginInvoke(() =>
            {
                SimulationStatusText.Text = "Grundstellung";
                SimulationDetailText.Text = "Kran steht an der konfigurierten Grundstellung.";
            });
        }
    }

    private void StarteBewegung(
        KranPositionGroundPosition target,
        DateTime nowUtc,
        SimulationsFahrzustand zielZustand,
        string logMessage)
    {
        var start = new KranPositionGroundPosition(
            posKranX,
            posKatzeY,
            posHubZ);

        double durationSeconds = Math.Max(
            Math.Abs(target.PosKranX - start.PosKranX) / DemoKranSpeedMmPerSecond,
            Math.Abs(target.PosKatzeY - start.PosKatzeY) / DemoKatzeSpeedMmPerSecond);
        durationSeconds = Math.Max(
            durationSeconds,
            Math.Abs(target.PosHubZ - start.PosHubZ) / DemoHubSpeedMmPerSecond);
        durationSeconds = Math.Max(
            durationSeconds,
            2.0);

        aktuelleBewegung = new KranMovement(
            start,
            target,
            nowUtc,
            TimeSpan.FromSeconds(durationSeconds));
        fahrzustand = zielZustand;

        Log(logMessage);
        Log(
            "006C|Bewegung geplant: " +
            $"Start X={start.PosKranX}, Y={start.PosKatzeY}, Z={start.PosHubZ}; " +
            $"Ziel X={target.PosKranX}, Y={target.PosKatzeY}, Z={target.PosHubZ}; " +
            $"Dauer={durationSeconds:0.0}s.");

        Dispatcher.BeginInvoke(() =>
        {
            SimulationStatusText.Text = zielZustand switch
            {
                SimulationsFahrzustand.FahreZurQuelle => demoModeAktiv ? "Demo: Quelle" : "Fahrt: Quelle",
                SimulationsFahrzustand.FahreZumZiel => demoModeAktiv ? "Demo: Ziel" : "Fahrt: Ziel",
                SimulationsFahrzustand.FahreZurGrundstellung => "Grundstellung",
                _ => "Simulation"
            };
            SimulationDetailText.Text = logMessage.Length > 5
                ? logMessage[5..]
                : logMessage;
        });
    }

    private static int InterpolateInt(
        int start,
        int target,
        double progress)
    {
        return (int)Math.Round(
            start + ((target - start) * progress),
            MidpointRounding.AwayFromZero);
    }

    private void WriteEvent203PayloadNodesNoLock()
    {
        SetEvent203Value(PosKranNodeName, posKranX.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value(PosKatzeNodeName, posKatzeY.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value(PosHubNodeName, posHubZ.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value(MagnetAnNodeName, gewuenschterMagnetAnWert.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value(MasseNettoNodeName, masseNetto.ToString(CultureInfo.InvariantCulture));
        SetEvent203Value("GattierenAktiv", (!IstAktiveFahrtEinlagerfahrt() && aktiveSimulationsFahrt is not null).ToString());
        SetEvent203Value("UmlagernAktiv", IstAktiveFahrtEinlagerfahrt().ToString());

        foreach (EventNodeConfiguration node in event203Nodes.Where(
                     node => string.Equals(node.NodeRole, "Payload", StringComparison.OrdinalIgnoreCase)))
        {
            string configuredValue;
            lock (event203Values)
            {
                configuredValue = event203Values.TryGetValue(node.NodeName, out string? value)
                    ? value
                    : "0";
            }

            WriteEvent203NodeNoLock(node, ConvertEvent203Value(node, configuredValue));
        }
    }

    private void WriteEvent203NodeNoLock(
        string nodeName,
        object value)
    {
        EventNodeConfiguration? node = event203Nodes.FirstOrDefault(
            configuredNode => string.Equals(
                configuredNode.NodeName,
                nodeName,
                StringComparison.OrdinalIgnoreCase));
        if (node is null || string.IsNullOrWhiteSpace(node.OpcNode))
        {
            return;
        }

        WriteEvent203NodeNoLock(node, ConvertEvent203Value(node, value));
    }

    private void WriteEvent203NodeNoLock(
        EventNodeConfiguration node,
        object value)
    {
        if (string.IsNullOrWhiteSpace(node.OpcNode))
        {
            return;
        }

        OpcStatus status = opcClient!.WriteNode(
            node.OpcNode,
            value);

        if (status.IsBad)
        {
            throw new InvalidOperationException(
                $"OPC-Schreiben fehlgeschlagen. Variable={node.NodeName}, Node={node.OpcNode}, Wert={value}, Status={status.Code}, Beschreibung={status.Description}");
        }

        SetEvent203Value(
            node.NodeName,
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? "-");
        LogOpcSend($"0134|OPC Senden Event_203: {node.NodeName}={value}");
        LogOpcSend($"0135|OPC Schreiben vom OPC-Server angenommen. Event=Event_203, Variable={node.NodeName}, Node={node.OpcNode}, Wert={value}, Status={status.Code}");
    }

    private static object ConvertEvent203Value(EventNodeConfiguration node, object value)
    {
        if (string.Equals(node.DataType, "Bit", StringComparison.OrdinalIgnoreCase))
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return bool.TryParse(text, out bool parsedBool)
                ? parsedBool
                : Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
    private void SetMagnetAn(
        int value,
        string grund,
        bool force = false)
    {
        gewuenschterMagnetAnWert = value;
        if (value == 0)
        {
            masseNetto = 0;
            SetEvent203Value(
                MasseNettoNodeName,
                masseNetto.ToString(CultureInfo.InvariantCulture));
        }

        try
        {
            lock (opcSyncRoot)
            {
                if (opcClient?.State != OpcClientState.Connected)
                {
                    Log($"008E|MagnetAn vorgemerkt: Wert={value}, Grund={grund}. OPC ist aktuell nicht verbunden.");
                    return;
                }

                if (!force && letzterGesendeterMagnetAnWert == value)
                {
                    return;
                }

                WriteEvent203NodeNoLock(MagnetAnNodeName, value);
                letzterGesendeterMagnetAnWert = value;
                Log($"0136|MagnetAn gesetzt: Wert={value}, Grund={grund}.");
            }
        }
        catch (Exception ex)
        {
            LogWarning($"0137|MagnetAn konnte nicht geschrieben werden. Wert={value}, Grund={grund}, Event=Event_203, Variable={MagnetAnNodeName}, Fehler={ex.GetType().Name}: {ex.Message}");
            StartBackgroundReconnectLoop("MagnetAn konnte nicht geschrieben werden");
        }
    }

    private void SetEvent203Value(
        string nodeName,
        string value)
    {
        lock (event203Values)
        {
            event203Values[nodeName] = value;
        }

        Dispatcher.BeginInvoke(RefreshEventView);
    }

    private int NextSpsLebensZaehler()
    {
        if (spsLebensZaehler == int.MaxValue)
        {
            spsLebensZaehler = 0;
        }
        else
        {
            spsLebensZaehler++;
        }

        return spsLebensZaehler;
    }

    private int NextEvent203Zaehler()
    {
        event203Zaehler = event203Zaehler == int.MaxValue
            ? 0
            : event203Zaehler + 1;
        return event203Zaehler;
    }

    private void OnClientStateChanged(
        object? sender,
        OpcClientStateChangedEventArgs e)
    {
        Log($"0138|OPC Client Zustand geaendert von {e.OldState} zu {e.NewState}.");

        if (e.NewState == OpcClientState.Connected)
        {
            SetOpcConnectedFromBackground("OPC UA Client erfolgreich verbunden / wiederverbunden");
            SetMagnetAn(gewuenschterMagnetAnWert, "OPC verbunden / wiederverbunden", force: true);
            LogWarning("0139|OPC UA Client erfolgreich verbunden / wiederverbunden!");
        }
        else if (e.NewState == OpcClientState.Disconnected)
        {
            SetOpcDisconnectedFromBackground("OPC getrennt");
            LogError("013A|Die Verbindung zum OPC UA Server wurde getrennt!");
            StartBackgroundReconnectLoop("OPC Client meldet Disconnected");
        }
        else if (e.NewState == OpcClientState.Reconnecting)
        {
            SetOpcReconnectFromBackground("Reconnect laeuft");
            Log("013B|Verbindung verloren. Auto-Reconnect versucht gerade die Wiederverbindung...");
            StartBackgroundReconnectLoop("OPC Client meldet Reconnecting");
        }
    }

    private void ConfigureTraegerLicense()
    {
        string? licenseKey =
            Environment.GetEnvironmentVariable("FALCOM_TRAEGER_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            Opc.UaFx.Client.Licenser.LicenseKey = licenseKey;
        }
        else
        {
            Opc.UaFx.Client.Licenser.LicenseKey = ConfigManager.TraegerLicenseKey;
        }
    }

    private void SetOpcConnected(string status)
    {
        OpcLamp.Fill = Brushes.LimeGreen;
        opcStatusText = "Verbunden";
        opcStatusDetailText = status;
        SimulationStatusText.Text = "Simulation bereit";
        SimulationDetailText.Text = "OPC-Verbindung steht. SPS-Logik wird schrittweise ergaenzt.";
        RefreshStatusView();
    }

    private void SetOpcDisconnected(string status)
    {
        OpcLamp.Fill = Brushes.Firebrick;
        opcStatusText = "Getrennt";
        opcStatusDetailText = status;
        SimulationStatusText.Text = "OPC getrennt";
        SimulationDetailText.Text = "Simulator wartet auf Wiederverbindung.";
        RefreshStatusView();
    }

    private void SetOpcReconnect(string status)
    {
        OpcLamp.Fill = Brushes.DarkOrange;
        opcStatusText = "Reconnect";
        opcStatusDetailText = status;
        SimulationStatusText.Text = "Reconnect laeuft";
        SimulationDetailText.Text = "Der Simulator versucht zyklisch, den OPC-Server wieder zu erreichen.";
        RefreshStatusView();
    }

    private void SetOpcConnectedFromBackground(string status)
    {
        Dispatcher.BeginInvoke(() => SetOpcConnected(status));
    }

    private void SetOpcDisconnectedFromBackground(string status)
    {
        Dispatcher.BeginInvoke(() => SetOpcDisconnected(status));
    }

    private void SetOpcReconnectFromBackground(string status)
    {
        Dispatcher.BeginInvoke(() => SetOpcReconnect(status));
    }

    private void SetSpsLebensZaehlerFromBackground(int value, DateTime timestamp)
    {
        Dispatcher.BeginInvoke(() =>
        {
            letzterSpsLebensZaehler = value;
            letzterSpsLebensZaehlerGesendetAm = timestamp;
            RefreshStatusView();
        });
    }

    private void SetFalcomLebensZaehlerFromBackground(int value, DateTime timestamp)
    {
        Dispatcher.BeginInvoke(() =>
        {
            letzterFalcomLebensZaehler = value;
            letzterFalcomLebensZaehlerEmpfangenAm = timestamp;
            RefreshStatusView();
        });
    }

    private void Log(string message)
    {
        WriteLog(LogLevel.Information, message);
    }

    private void LogWarning(string message)
    {
        WriteLog(LogLevel.Warning, message);
    }

    private void LogError(string message)
    {
        WriteLog(LogLevel.Error, message);
    }

    private void LogOpcSend(string message)
    {
        WriteLog(LogLevel.Information, message);
    }

    private void LogOpcReceive(string message)
    {
        WriteLog(LogLevel.Information, message);
    }

    private void WriteLog(LogLevel logLevel, string message)
    {
        string line =
            $"{DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{ToShortLevelText(logLevel)}] {message}";

        uiLogSink.Write(
            logLevel,
            line);
        fileLogSink.Write(line);
    }

    private static string ToShortLevelText(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "---"
        };
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

    private void RefreshStatusView()
    {
        LastRefreshText.Text = $"Aktualisiert: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        OpcStatusText.Text = opcStatusText;
        OpcStatusDetailText.Text = string.IsNullOrWhiteSpace(opcStatusDetailText)
            ? opcEndpoint
            : $"{opcStatusDetailText} | {opcEndpoint}";
        bool falcomLifeFresh = letzterFalcomLebensZaehlerEmpfangenAm is not null
                               && DateTime.Now - letzterFalcomLebensZaehlerEmpfangenAm.Value < TimeSpan.FromSeconds(5);
        FalcomLifeLamp.Fill = falcomLifeFresh
            ? Brushes.LimeGreen
            : letzterFalcomLebensZaehlerEmpfangenAm is null
                ? Brushes.DimGray
                : Brushes.Firebrick;
        FalcomLifeValueText.Text = letzterFalcomLebensZaehler?.ToString(CultureInfo.InvariantCulture) ?? "-";
        FalcomLifeTimeText.Text = letzterFalcomLebensZaehlerEmpfangenAm is null
            ? "Noch nicht empfangen"
            : $"Empfangen {letzterFalcomLebensZaehlerEmpfangenAm.Value:dd.MM.yyyy HH:mm:ss}";

        bool spsLifeFresh = letzterSpsLebensZaehlerGesendetAm is not null
                            && DateTime.Now - letzterSpsLebensZaehlerGesendetAm.Value < TimeSpan.FromSeconds(3);
        SpsLifeLamp.Fill = spsLifeFresh
            ? Brushes.LimeGreen
            : letzterSpsLebensZaehlerGesendetAm is null
                ? Brushes.DimGray
                : Brushes.Firebrick;
        SpsLifeValueText.Text = letzterSpsLebensZaehler?.ToString(CultureInfo.InvariantCulture) ?? "-";
        SpsLifeTimeText.Text = letzterSpsLebensZaehlerGesendetAm is null
            ? "Noch nicht gesendet"
            : $"Gesendet {letzterSpsLebensZaehlerGesendetAm.Value:dd.MM.yyyy HH:mm:ss}";
    }

    private void RefreshEventView()
    {
        KranfahrtBeendetEventItems.ItemsSource = CreateEventItems(
            kranfahrtBeendetNodes,
            kranfahrtBeendetValues);
        KranfahrtAuftragEventItems.ItemsSource = CreateEventItems(
            kranfahrtAuftragNodes,
            kranfahrtAuftragValues);
        Event203EventItems.ItemsSource = CreateEventItems(
            event203Nodes,
            event203Values);
        Event104EventItems.ItemsSource = CreateEventItems(
            event104Nodes,
            event104Values);
        Event105EventItems.ItemsSource = CreateEventItems(
            event105Nodes,
            event105Values);
        Event204EventItems.ItemsSource = CreateEventItems(
            event204Nodes,
            event204Values);
        Event205EventItems.ItemsSource = CreateEventItems(
            event205Nodes,
            event205Values);
        Event206EventItems.ItemsSource = CreateEventItems(
            event206Nodes,
            event206Values);
    }

    private static List<EventVisualItem> CreateEventItems(
        IReadOnlyList<EventNodeConfiguration> nodes,
        Dictionary<string, string> values)
    {
        lock (values)
        {
            return nodes
                .OrderBy(node => string.Equals(
                    node.NodeRole,
                    "Trigger",
                    StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0)
                .ThenBy(node => node.NodeName)
                .Select(node => new EventVisualItem(
                    node.NodeName,
                    values.TryGetValue(node.NodeName, out string? value)
                        ? value
                        : "-"))
                .ToList();
        }
    }
    private static void ScrollToLastItem(ListBox listBox)
    {
        if (listBox.Items.Count == 0)
        {
            return;
        }

        listBox.ScrollIntoView(listBox.Items[^1]);
    }

    protected override void OnClosed(EventArgs e)
    {
        disposed = true;
        reconnectCancellation.Cancel();
        lebensZaehlerCancellation.Cancel();
        logRefreshTimer.Stop();
        statusRefreshTimer.Stop();

        lock (opcSyncRoot)
        {
            if (opcClient is not null)
            {
                opcClient.StateChanged -= OnClientStateChanged;
                try
                {
                    ResetSubscription();
                    opcClient.Disconnect();
                }
                catch
                {
                }

                opcClient.Dispose();
                opcClient = null;
            }
        }

        reconnectCancellation.Dispose();
        lebensZaehlerCancellation.Dispose();
        base.OnClosed(e);
    }

    private sealed record EventVisualItem(
        string Name,
        string Wert);

    private sealed record KranMovement(
        KranPositionGroundPosition Start,
        KranPositionGroundPosition Target,
        DateTime StartUtc,
        TimeSpan Duration);

    private sealed class EinlagerLeerSimulationState
    {
        public EinlagerLeerSimulationState(int zielFahrten)
        {
            ZielFahrten = zielFahrten;
        }

        public int ZielFahrten { get; }
        public int AbgeschlosseneFahrten { get; set; }
        public bool EventGesendet { get; set; }
    }

    private sealed record EinlagerTeilfahrtStand(
        long HistorisierteTeilfahrten,
        bool AktuelleTeilfahrtBereitsHistorisiert);

    private enum SimulationsFahrzustand
    {
        Grundstellung,
        FahreZurQuelle,
        FahreZumZiel,
        WarteAufNeueFahrt,
        FahreZurGrundstellung
    }
}
















