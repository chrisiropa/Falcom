using Falcom;
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
    private const double DemoKranSpeedMmPerSecond = 960.0;
    private const double DemoKatzeSpeedMmPerSecond = 960.0;
    private const double DemoHubSpeedMmPerSecond = 720.0;
    private const int DemoTelegrammNummer = -1;
    private const decimal MaxIstGewichtKg = 1000m;

    private readonly FalcomUiLogSink uiLogSink = new();
    private readonly FalcomFileSink fileLogSink;
    private readonly DispatcherTimer logRefreshTimer = new();
    private readonly DispatcherTimer statusRefreshTimer = new();
    private readonly Random demoRandom = new();
    private readonly object opcSyncRoot = new();
    private readonly string opcEndpoint;
    private readonly string kranSpsLebensZaehlerNodeId;
    private readonly string falcomLebensZaehlerNodeId;
    private readonly IReadOnlyList<EventNodeConfiguration> kranfahrtBeendetNodes;
    private readonly IReadOnlyList<EventNodeConfiguration> kranfahrtAuftragNodes;
    private readonly IReadOnlyList<SimEventMappingConfiguration> kranfahrtBeendetZuordnungen;
    private readonly IReadOnlyList<EventNodeConfiguration> kranPositionNodes;
    private readonly Dictionary<string, EventNodeConfiguration> kranfahrtBeendetNodesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EventNodeConfiguration> kranfahrtAuftragNodesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> kranfahrtBeendetValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> kranfahrtAuftragValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> letzteKranfahrtAuftragPayload = new(StringComparer.OrdinalIgnoreCase);
    private int? letzteVerarbeiteteAuftragTelegrammNummer;
    private readonly Dictionary<string, string> kranPositionValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly KranPositionGroundPosition grundstellung;
    private readonly IReadOnlyDictionary<long, SimKranPosition> positionenById;
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
    private string opcStatusText = "Initialisierung";
    private string opcStatusDetailText = string.Empty;
    private int posKranX;
    private int posKatzeY;
    private int posHubZ;
    private int spsLebensZaehler;
    private int? letzterSpsLebensZaehler;
    private DateTime? letzterSpsLebensZaehlerGesendetAm;
    private int? letzterFalcomLebensZaehler;
    private DateTime? letzterFalcomLebensZaehlerEmpfangenAm;
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
        opcEndpoint = configuration.OpcEndpoint.Trim();
        kranSpsLebensZaehlerNodeId = configuration.KranSpsLebensZaehlerNodeId.Trim();
        falcomLebensZaehlerNodeId = configuration.FalcomLebensZaehlerNodeId.Trim();
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
        kranPositionNodes = configuration.KranPositionNodes;
        grundstellung = configuration.Grundstellung;
        positionenById = configuration.Positionen;
        fileLogSink = new FalcomFileSink(configuration.LogfilePath);

        ConfigureTraegerLicense();
        CreateClientInstance();

        Log($"Logdatei aktiv: {configuration.LogfilePath}");
        Log($"OPC Endpoint aus FALCOM_PARAMETER.OpcServer: {opcEndpoint}");
        if (string.IsNullOrWhiteSpace(kranSpsLebensZaehlerNodeId))
        {
            LogError("LebensZaehlerKran.LebensZaehler ist in der Datenbank nicht gültig konfiguriert. SPS->FALCOM Lebenszähler wird nicht geschrieben.");
        }
        else
        {
            Log($"LebensZaehlerKran.LebensZaehler Node: {kranSpsLebensZaehlerNodeId}");
        }

        if (string.IsNullOrWhiteSpace(falcomLebensZaehlerNodeId))
        {
            LogError("LebensZaehlerFalcom.LebensZaehler ist in der Datenbank nicht g�ltig konfiguriert. FALCOM->SPS Lebensz�hler wird nicht empfangen.");
        }
        else
        {
            Log($"LebensZaehlerFalcom.LebensZaehler Node: {falcomLebensZaehlerNodeId}");
        }

        Log($"Event 1 KranfahrtBeendet Variablen: {string.Join(", ", kranfahrtBeendetNodes.Select(node => node.NodeName))}");
        Log($"Event 1/2 Sim-Zuordnungen geladen: {kranfahrtBeendetZuordnungen.Count}. {string.Join("; ", kranfahrtBeendetZuordnungen.Select(mapping => mapping.Info ?? mapping.TargetNode.NodeName))}");
        Log($"Event 2 KranfahrtAuftrag Variablen: {string.Join(", ", kranfahrtAuftragNodes.Select(node => node.NodeName))}");
        Log($"Event 5 KranPosition Variablen: {string.Join(", ", kranPositionNodes.Select(node => node.NodeName))}");
        FahreGrundstellungAn();
        Log("Kran-SPS-Simulator bereit.");
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
            Log($"001A|Verbindung zu {opcEndpoint} wird aufgebaut.");
            ResetSubscription();
            opcClient?.Connect();
            ConfigureOpcSubscriptions();
            Log("001B|OPC-Verbindung und Kanalregistrierung sind bereit.");
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

        LogWarning($"005D|OPC-Hintergrund-Reconnect wird gestartet. Grund={reason}.");

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
                                    Log($"005E|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
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
                                Log($"001A|Verbindung zu {opcEndpoint} wird aufgebaut.");
                                opcClient!.Connect();
                                ConfigureOpcSubscriptions();
                                Log("001B|OPC-Verbindung und Kanalregistrierung sind bereit.");
                                SetOpcConnectedFromBackground(
                                    $"Verbunden mit {opcEndpoint}");
                                Log($"005E|OPC-Hintergrund-Reconnect erfolgreich. Versuch={attempt}.");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            SetOpcReconnectFromBackground("Reconnect läuft");

                            if (DateTime.UtcNow >= nextReconnectLogUtc)
                            {
                                LogWarning(
                                    $"005F|OPC-Hintergrund-Reconnect Versuch={attempt} noch nicht erfolgreich. " +
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

        if (!string.IsNullOrWhiteSpace(falcomLebensZaehlerNodeId))
        {
            var falcomLifeItem = new OpcMonitoredItem(falcomLebensZaehlerNodeId, OpcAttribute.Value)
            {
                Tag = "LebensZaehlerFalcom.LebensZaehler"
            };
            falcomLifeItem.DataChangeReceived += HandleOpcDataChange;
            subscription.AddMonitoredItem(falcomLifeItem);
            monitoredItems.Add(falcomLifeItem);
            Log($"0062|OPC Empfangskanal registriert. Event=LebensZaehlerFalcom, Node={falcomLebensZaehlerNodeId}");
        }

        if (TryGetKranfahrtAuftragNode("TelegrammNummer", out EventNodeConfiguration telegrammNode))
        {
            var telegrammItem = new OpcMonitoredItem(telegrammNode.OpcNode, OpcAttribute.Value)
            {
                Tag = "KranfahrtAuftrag.TelegrammNummer"
            };
            telegrammItem.DataChangeReceived += HandleOpcDataChange;
            subscription.AddMonitoredItem(telegrammItem);
            monitoredItems.Add(telegrammItem);
            Log($"006D|OPC Empfangskanal registriert. Event=KranfahrtAuftrag, Trigger=TelegrammNummer, Node={telegrammNode.OpcNode}");
        }
        else
        {
            LogWarning("006D|KranfahrtAuftrag.TelegrammNummer ist nicht konfiguriert. Event 2 kann nicht empfangen werden.");
        }

        subscription.ApplyChanges();
        InitialisiereKranfahrtAuftragTelegrammNoLock();
    }

    private void InitialisiereKranfahrtAuftragTelegrammNoLock()
    {
        if (!TryGetKranfahrtAuftragNode("TelegrammNummer", out EventNodeConfiguration telegrammNode))
        {
            return;
        }

        try
        {
            OpcValue value = opcClient!.ReadNode(telegrammNode.OpcNode);
            if (!value.Status.IsGood || value.Value is null)
            {
                LogWarning(
                    $"006D|Initialer KranfahrtAuftrag-Telegrammstand konnte nicht gelesen werden. " +
                    $"Node={telegrammNode.OpcNode}, Status={value.Status.Code}, Beschreibung={value.Status.Description}. " +
                    "Simulator wartet trotzdem auf die naechste Aenderung.");
                return;
            }

            int telegrammNummer = Convert.ToInt32(value.Value, CultureInfo.InvariantCulture);
            Dictionary<string, object?> payload = ReadEventValuesNoLock(kranfahrtAuftragNodes);

            if (IstInitialerKranfahrtAuftragBereitsBeendetNoLock(payload, out string begruendung))
            {
                letzteVerarbeiteteAuftragTelegrammNummer = telegrammNummer;
                Log($"006D|Warten auf naechste Fahrt. Letztes KranfahrtAuftrag-Telegramm war {telegrammNummer}. {begruendung}");
                return;
            }

            Log($"006D|Initialer KranfahrtAuftrag wird als offener Auftrag verarbeitet. TelegrammNummer={telegrammNummer}. {begruendung}");
            SchreibeKranfahrtBeendetZuordnungNoLock(payload, triggerErhoehen: false);
            VerarbeiteKranfahrtAuftragPayload(telegrammNummer, payload, "Initialer KranfahrtAuftrag");
        }
        catch (Exception ex)
        {
            LogWarning($"006D|Initialer KranfahrtAuftrag-Telegrammstand konnte nicht gelesen werden. Fehler={ex.GetType().Name}: {ex.Message}");
        }
    }

    private void HandleOpcDataChange(object sender, OpcDataChangeReceivedEventArgs e)
    {
        try
        {
            string changedNodeId = e.MonitoredItem.NodeId.ToString();
            object? rawValue = e.Item.Value.Value;

            LogOpcReceive($"0050|OPC Empfang: Node={changedNodeId}, Wert={rawValue}");

            if (string.Equals(changedNodeId, falcomLebensZaehlerNodeId, StringComparison.Ordinal))
            {
                int value = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                SetFalcomLebensZaehlerFromBackground(value, DateTime.Now);
                return;
            }

            if (TryGetKranfahrtAuftragNode("TelegrammNummer", out EventNodeConfiguration telegrammNode)
                && string.Equals(changedNodeId, telegrammNode.OpcNode, StringComparison.Ordinal))
            {
                int telegrammNummer = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                VerarbeiteKranfahrtAuftragTelegramm(telegrammNummer);
            }
        }
        catch (Exception ex)
        {
            LogWarning($"0063|OPC Callback konnte nicht verarbeitet werden. Fehler={ex.GetType().Name}: {ex.Message}");
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
            Log($"006D|Initialer KranfahrtAuftrag-Telegrammstand aus Callback uebernommen. Wert={telegrammNummer}. Simulator wartet auf die naechste Aenderung.");
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
        Log($"006E|{quelle}: Event-1-Zuordnung ohne Trigger angewendet. TelegrammNummer={telegrammNummer}.");

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
        begruendung = "Event 1 konnte nicht eindeutig mit Event 2 abgeglichen werden; Initialauftrag gilt als offen.";

        if (!TryGetPayloadInt64(auftragPayload, "AuftragNummer", out long auftragNummer)
            || !TryGetPayloadInt32(auftragPayload, "AuftragTeilfahrt", out int auftragTeilfahrt))
        {
            begruendung = "Event 2 enthaelt keine eindeutige AuftragNummer/AuftragTeilfahrt; Initialauftrag gilt als offen.";
            return false;
        }

        Dictionary<string, object?> beendetPayload = ReadEventValuesNoLock(kranfahrtBeendetNodes);
        SetEventValues(kranfahrtBeendetValues, beendetPayload);

        bool hasBeendetAuftrag = TryGetPayloadInt64(beendetPayload, "AuftragsNummer", out long beendetAuftragNummer);
        bool hasBeendetTeilfahrt = TryGetPayloadInt32(beendetPayload, "AuftragTeilfahrt", out int beendetTeilfahrt);

        if (!hasBeendetAuftrag || !hasBeendetTeilfahrt)
        {
            begruendung = $"Event 1 enthaelt keine eindeutige AuftragsNummer/AuftragTeilfahrt. Event2 Auftrag={auftragNummer}, Teilfahrt={auftragTeilfahrt} gilt als offen.";
            return false;
        }

        if (beendetAuftragNummer == auftragNummer && beendetTeilfahrt == auftragTeilfahrt)
        {
            begruendung = $"Event 1 bestaetigt bereits Auftrag={auftragNummer}, Teilfahrt={auftragTeilfahrt}; Initialwert wird nicht erneut gefahren.";
            return true;
        }

        begruendung = $"Event 1 passt nicht zu Event 2. Event2 Auftrag={auftragNummer}, Teilfahrt={auftragTeilfahrt}; Event1 Auftrag={beendetAuftragNummer}, Teilfahrt={beendetTeilfahrt}.";
        return false;
    }

    private AktuelleFahrtSimulation? ErstelleFahrtAusKranfahrtAuftragPayload(
        int telegrammNummer,
        IReadOnlyDictionary<string, object?> payload)
    {
        if (!TryGetPayloadInt64(payload, "Quelle", out long quellePositionId)
            || !TryGetPayloadInt64(payload, "Ziel", out long zielPositionId))
        {
            LogWarning($"006E|KranfahrtAuftrag kann nicht gefahren werden. Quelle oder Ziel fehlt. TelegrammNummer={telegrammNummer}.");
            return null;
        }

        if (!positionenById.TryGetValue(quellePositionId, out SimKranPosition? quelle))
        {
            LogWarning($"006E|KranfahrtAuftrag kann nicht gefahren werden. QuellePositionID={quellePositionId} ist in FALCOM_KRAN_POSITION nicht konfiguriert oder hat keine Abwurfposition. TelegrammNummer={telegrammNummer}.");
            return null;
        }

        if (!positionenById.TryGetValue(zielPositionId, out SimKranPosition? ziel))
        {
            LogWarning($"006E|KranfahrtAuftrag kann nicht gefahren werden. ZielPositionID={zielPositionId} ist in FALCOM_KRAN_POSITION nicht konfiguriert oder hat keine Abwurfposition. TelegrammNummer={telegrammNummer}.");
            return null;
        }

        TryGetPayloadInt64(payload, "AuftragNummer", out long auftragNummer);
        TryGetPayloadInt32(payload, "AuftragTeilfahrt", out int auftragTeilfahrt);
        TryGetPayloadDecimal(payload, "SollMasse", out decimal sollMasse);

        var fahrt = new AktuelleFahrtSimulation(
            naechsteInterneFahrtId++,
            telegrammNummer,
            "OPC_EVENT_2",
            auftragNummer,
            auftragTeilfahrt,
            "EMPFANGEN",
            quellePositionId,
            zielPositionId,
            sollMasse,
            quelle.Bezeichnung,
            ziel.Bezeichnung,
            quelle.Position,
            ziel.Position);

        Log(
            "006E|KranfahrtAuftrag in Simulatorfahrt umgesetzt: " +
            $"TelegrammNummer={telegrammNummer}, Auftrag={auftragNummer}, Teilfahrt={auftragTeilfahrt}, " +
            $"Quelle={quelle.Bezeichnung} ({quellePositionId}), Ziel={ziel.Bezeichnung} ({zielPositionId}), SollMasse={sollMasse:0.###}.");

        return fahrt;
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

    private Dictionary<string, object?> ReadEventValuesNoLock(IReadOnlyList<EventNodeConfiguration> nodes)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (EventNodeConfiguration node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.OpcNode))
            {
                continue;
            }

            OpcValue value = opcClient!.ReadNode(node.OpcNode);
            if (!value.Status.IsGood)
            {
                throw new InvalidOperationException(
                    $"OPC-Lesen fehlgeschlagen. Variable={node.NodeName}, Node={node.OpcNode}, Status={value.Status.Code}, Beschreibung={value.Status.Description}");
            }

            values[node.NodeName] = value.Value;
            LogOpcReceive($"006F|OPC Event lesen: {node.NodeName}={value.Value}");
        }

        return values;
    }

    private void SchreibeKranfahrtBeendetZuordnungNoLock(
        IReadOnlyDictionary<string, object?> auftragValues,
        bool triggerErhoehen)
    {
        int geschrieben = 0;
        int uebersprungen = 0;

        foreach (SimEventMappingConfiguration mapping in kranfahrtBeendetZuordnungen)
        {
            string info = mapping.Info ?? mapping.TargetNode.NodeName;
            string typ = mapping.Zuordnungstyp.Trim().ToUpperInvariant();

            if (string.Equals(typ, "DIREKT", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(mapping.SourceNodeName)
                    || !auftragValues.TryGetValue(mapping.SourceNodeName, out object? sourceValue))
                {
                    uebersprungen++;
                    LogWarning($"007D|SIM-Zuordnung nicht geschrieben, weil Quellwert fehlt. ID={mapping.ID}, Info={info}, SourceNode={mapping.SourceNodeName ?? "-"}.");
                    continue;
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

            if (string.Equals(typ, "TRIGGER_ERHOEHEN", StringComparison.OrdinalIgnoreCase))
            {
                if (!triggerErhoehen)
                {
                    uebersprungen++;
                    Log($"0080|SIM-Zuordnung Trigger bleibt noch unveraendert: {info}.");
                    continue;
                }

                ErhoeheKranfahrtBeendetTriggerNoLock(mapping.TargetNode, info);
                geschrieben++;
                continue;
            }

            uebersprungen++;
            LogWarning($"0081|SIM-Zuordnung hat unbekannten Typ. ID={mapping.ID}, Typ={mapping.Zuordnungstyp}, Info={info}.");
        }

        Log($"0082|SIM-Zuordnungen fuer KranfahrtBeendet angewendet. Geschrieben={geschrieben}, Uebersprungen={uebersprungen}, TriggerErhoehen={triggerErhoehen}.");
    }

    private object? BegrenzeIstGewichtWennNoetig(
        EventNodeConfiguration targetNode,
        object? value)
    {
        if (!string.Equals(targetNode.NodeName, "IstGewicht", StringComparison.OrdinalIgnoreCase))
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
            LogWarning($"0084|IstGewicht konnte fuer Begrenzung nicht interpretiert werden. Wert={FormatOpcValue(value)}, Fehler={ex.GetType().Name}: {ex.Message}");
            return value;
        }

        if (istGewicht <= MaxIstGewichtKg)
        {
            return value;
        }

        Log($"0085|IstGewicht fuer KranfahrtBeendet wird auf maximal {MaxIstGewichtKg:0.###} kg begrenzt. Ursprungswert={istGewicht:0.###} kg.");
        return MaxIstGewichtKg;
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
            LogWarning($"0072|KranfahrtBeendet Trigger konnte vor dem Erhoehen nicht gelesen werden. Starte bei 0. Node={triggerNode.OpcNode}, Fehler={ex.GetType().Name}: {ex.Message}");
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
        Log($"0074|KranfahrtBeendet Trigger gesendet. {info}, NeuerWert={FormatOpcValue(converted)}.");
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
            Log("0074|KranfahrtBeendet gesendet. Alle aktiven SIM-Zuordnungen inklusive Trigger wurden verarbeitet.");
        }
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
        if (!string.Equals(nodeName, "KranQuelle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(nodeName, "KranZiel", StringComparison.OrdinalIgnoreCase))
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
        if (string.IsNullOrWhiteSpace(kranSpsLebensZaehlerNodeId))
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
                        AktualisiereFahrposition(DateTime.UtcNow);

                        try
                        {
                            lock (opcSyncRoot)
                            {
                                if (opcClient?.State != OpcClientState.Connected)
                                {
                                    continue;
                                }

                                WriteKranPositionPayloadNodesNoLock();

                                OpcStatus status = opcClient.WriteNode(
                                    kranSpsLebensZaehlerNodeId,
                                    value);

                                if (status.IsBad)
                                {
                                    throw new InvalidOperationException(
                                        $"OPC-Schreiben fehlgeschlagen. Node={kranSpsLebensZaehlerNodeId}, Status={status.Code}, Beschreibung={status.Description}");
                                }
                            }

                            letzterSpsLebensZaehler = value;
                            letzterSpsLebensZaehlerGesendetAm = DateTime.Now;
                            SetKranPositionValue(
                                "LebensZaehler",
                                value.ToString(CultureInfo.InvariantCulture));
                            SetSpsLebensZaehlerFromBackground(value, letzterSpsLebensZaehlerGesendetAm.Value);
                            LogOpcSend($"0051|OPC Senden: Node={kranSpsLebensZaehlerNodeId}, Wert={value}");
                        }
                        catch (Exception ex)
                        {
                            SetOpcReconnectFromBackground("Reconnect läuft");
                            StartBackgroundReconnectLoop("SPS-LebensZaehler konnte nicht geschrieben werden");

                            if (DateTime.UtcNow >= nextLebensZaehlerErrorLogUtc)
                            {
                                LogError(
                                    $"SPS->FALCOM LebensZaehler konnte nicht geschrieben werden. Node={kranSpsLebensZaehlerNodeId}, Wert={value}, Fehler={ex.GetType().Name}: {ex.Message}");
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

        SetKranPositionValue(
            "PosKranX",
            posKranX.ToString(CultureInfo.InvariantCulture));
        SetKranPositionValue(
            "PosKatzeY",
            posKatzeY.ToString(CultureInfo.InvariantCulture));
        SetKranPositionValue(
            "PosHubZ",
            posHubZ.ToString(CultureInfo.InvariantCulture));
        SetKranPositionValue(
            "LebensZaehler",
            spsLebensZaehler.ToString(CultureInfo.InvariantCulture));

        Log(
            "0060|Grundstellung angefahren: " +
            $"PosKranX={posKranX}, PosKatzeY={posKatzeY}, PosHubZ={posHubZ}. " +
            "Position liegt ueber Lagerbox 8.");
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

        Log("008D|Demo-Modus beendet. Grundstellung ueber Lagerbox 8 wird angefahren.");
        aktiveSimulationsFahrt = null;
        aktuelleBewegung = null;
        warteAufNeueFahrtBisUtc = null;
        StarteBewegung(
            grundstellung,
            DateTime.UtcNow,
            SimulationsFahrzustand.FahreZurGrundstellung,
            "008D|Demo-Modus beendet. Grundstellung ueber Lagerbox 8 wird angefahren.");
    }

    private void StarteNaechsteDemoFahrt(DateTime nowUtc)
    {
        AktuelleFahrtSimulation? fahrt = ErzeugeDemoFahrt();
        if (fahrt is null)
        {
            LogWarning("008C|Demo-Modus kann keine plausible Fahrt bilden. Es fehlen Lagerboxen, LKW-Plaetze oder Chargierwagen in FALCOM_KRAN_POSITION.");
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
                    "0066|Nach 10 Sekunden ohne neue Fahrt wird die Grundstellung ueber Lagerbox 8 angefahren.");
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
            $"Quelle={fahrt.QuelleBezeichnung} ({fahrt.QuellePositionID}), " +
            $"Ziel={fahrt.ZielBezeichnung} ({fahrt.ZielPositionID}).");
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
            StarteBewegung(
                aktiveSimulationsFahrt.Ziel,
                nowUtc,
                SimulationsFahrzustand.FahreZumZiel,
                "0069|Quelle erreicht. Ziel wird angefahren: " +
                $"{aktiveSimulationsFahrt.ZielBezeichnung} ({aktiveSimulationsFahrt.ZielPositionID}).");
            return;
        }

        if (fahrzustand == SimulationsFahrzustand.FahreZumZiel && aktiveSimulationsFahrt is not null)
        {
            if (demoModeAktiv)
            {
                Log(
                    "008C|Demo-Ziel erreicht. Es wurde kein KranfahrtBeendet-Event gesendet. " +
                    $"Quelle={aktiveSimulationsFahrt.QuelleBezeichnung} ({aktiveSimulationsFahrt.QuellePositionID}), " +
                    $"Ziel={aktiveSimulationsFahrt.ZielBezeichnung} ({aktiveSimulationsFahrt.ZielPositionID}).");
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
                    $"Quelle={aktiveSimulationsFahrt.QuellePositionID}, Ziel={aktiveSimulationsFahrt.ZielPositionID}.");
                SendeKranfahrtBeendetTelegramm();
            }
            catch (Exception ex)
            {
                LogError($"0076|KranfahrtBeendet konnte nicht gesendet werden. Fehler={ex.GetType().Name}: {ex.Message}");
            }

            aktiveSimulationsFahrt = null;
            fahrzustand = SimulationsFahrzustand.WarteAufNeueFahrt;
            warteAufNeueFahrtBisUtc = nowUtc.AddSeconds(10);
            Log(
                "006A|Warten auf naechste Fahrt. " +
                $"Letzte Fahrt war TelegrammNummer={letzteAbgefahreneFahrt.TelegrammNummer}, " +
                $"AuftragID={letzteAbgefahreneFahrt.AuftragID}, Teilfahrt={letzteAbgefahreneFahrt.AuftragTeilfahrt}. " +
                "Wenn 10 Sekunden nichts Neues kommt, wird Lagerbox 8 als Grundstellung angefahren.");
            Dispatcher.BeginInvoke(() =>
            {
                SimulationStatusText.Text = "Warte auf neue Fahrt";
                SimulationDetailText.Text = "Fahrt abgefahren. Wenn 10 Sekunden nichts Neues kommt, wird Lagerbox 8 als Grundstellung angefahren.";
            });
            return;
        }

        if (fahrzustand == SimulationsFahrzustand.FahreZurGrundstellung)
        {
            fahrzustand = SimulationsFahrzustand.Grundstellung;
            Log("006B|Grundstellung ueber Lagerbox 8 erreicht.");
            Dispatcher.BeginInvoke(() =>
            {
                SimulationStatusText.Text = "Grundstellung";
                SimulationDetailText.Text = "Kran steht mittig ueber Lagerbox 8.";
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

    private void WriteKranPositionPayloadNodesNoLock()
    {
        WriteKranPositionNodeNoLock(
            "PosKranX",
            posKranX);
        WriteKranPositionNodeNoLock(
            "PosKatzeY",
            posKatzeY);
        WriteKranPositionNodeNoLock(
            "PosHubZ",
            posHubZ);
    }

    private void WriteKranPositionNodeNoLock(
        string nodeName,
        int value)
    {
        EventNodeConfiguration? node = kranPositionNodes.FirstOrDefault(
            configuredNode => string.Equals(
                configuredNode.NodeName,
                nodeName,
                StringComparison.OrdinalIgnoreCase));
        if (node is null || string.IsNullOrWhiteSpace(node.OpcNode))
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

        SetKranPositionValue(
            nodeName,
            value.ToString(CultureInfo.InvariantCulture));
        LogOpcSend($"0061|OPC Senden KranPosition: {node.NodeName}={value}");
    }

    private void SetKranPositionValue(
        string nodeName,
        string value)
    {
        lock (kranPositionValues)
        {
            kranPositionValues[nodeName] = value;
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

    private void OnClientStateChanged(
        object? sender,
        OpcClientStateChangedEventArgs e)
    {
        Log($"002A|OPC Client Zustand geändert von {e.OldState} zu {e.NewState}.");

        if (e.NewState == OpcClientState.Connected)
        {
            SetOpcConnectedFromBackground("OPC UA Client erfolgreich verbunden / wiederverbunden");
            LogWarning("002B|OPC UA Client erfolgreich verbunden / wiederverbunden!");
        }
        else if (e.NewState == OpcClientState.Disconnected)
        {
            SetOpcDisconnectedFromBackground("OPC getrennt");
            LogError("002C|Die Verbindung zum OPC UA Server wurde getrennt!");
            StartBackgroundReconnectLoop("OPC Client meldet Disconnected");
        }
        else if (e.NewState == OpcClientState.Reconnecting)
        {
            SetOpcReconnectFromBackground("Reconnect läuft");
            Log("002D|Verbindung verloren. Auto-Reconnect versucht gerade die Wiederverbindung...");
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
        SimulationDetailText.Text = "OPC-Verbindung steht. SPS-Logik wird schrittweise ergänzt.";
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
        SimulationStatusText.Text = "Reconnect läuft";
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
        KranPositionEventItems.ItemsSource = CreateEventItems(
            kranPositionNodes,
            kranPositionValues);
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

    private enum SimulationsFahrzustand
    {
        Grundstellung,
        FahreZurQuelle,
        FahreZumZiel,
        WarteAufNeueFahrt,
        FahreZurGrundstellung
    }
}


