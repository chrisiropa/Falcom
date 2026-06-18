using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using Opc.UaFx;
using Opc.UaFx.Client;

namespace KranSimulator;

public partial class MainWindow : Window
{
    private const string NodeAuftragId = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Event.AuftragID";
    private const string NodeQuelle = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Event.KranQuelle";
    private const string NodeZiel = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Event.KranZiel";
    private const string NodeToleranz = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Event.Toleranz";
    private const string NodeIstGewicht = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Event.IstGewicht";
    private const string NodeFehlercode = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Event.FehlerCode";
    private const string NodeKranCMD = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Event.CmdID";

    public MainWindow()
    {
        InitializeComponent();

        string? licenseKey = ConfigManager.TraegerLicenseKey;
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            Opc.UaFx.Client.Licenser.LicenseKey = licenseKey;
        }

        Log(string.IsNullOrWhiteSpace(licenseKey)
            ? "Bereit. Kein Lizenzschluessel in FALCOM_TRAEGER_LICENSE_KEY gefunden."
            : "Bereit. Traeger-Lizenz aus der Umgebung geladen.");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Kranpositionen werden aus FG geladen...");

        try
        {
            IReadOnlyList<CranePosition> positions = await Task.Run(LoadCranePositions);
            List<CranePosition> sources = positions
                .Where(position => position.Art is "QUELLE" or "QUELLE_UND_ZIEL")
                .ToList();
            List<CranePosition> targets = positions
                .Where(position => position.Art is "ZIEL" or "QUELLE_UND_ZIEL")
                .ToList();

            CmbQuelle.ItemsSource = sources;
            CmbZiel.ItemsSource = targets;
            CmbQuelle.SelectedIndex = sources.Count > 0 ? 0 : -1;
            CmbZiel.SelectedIndex = targets.Count > 0 ? 0 : -1;

            TxtStatus.Text = "Kranpositionen geladen";
            Log($"{sources.Count} Kranquellen und {targets.Count} Kranziele aus FG geladen.");
        }
        catch (Exception ex)
        {
            ConnectionIndicator.Fill = Brushes.Firebrick;
            TxtStatus.Text = "Kranpositionen konnten nicht geladen werden";
            Log($"FEHLER beim Laden der Kranpositionen: {ex.Message}");
        }
        finally
        {
            SetBusy(false, TxtStatus.Text);
        }
    }

    private async void BtnWriteFalcomEvent_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInput(out CranePayload payload))
        {
            return;
        }

        await ExecuteOpcActionAsync(
            "FALCOM Event wird geschrieben...",
            client =>
            {
                string? error = WritePayload(client, payload);
                if (error is not null)
                {
                    return error;
                }

                error = WriteNodeChecked(client, NodeKranCMD, false, "Kran Komando zurückgesetzt");
                if (error is not null)
                {
                    return error;
                }

                Thread.Sleep(1200);
                return WriteNodeChecked(client, NodeKranCMD, true, "Kran Komando gesetzt");
            },
            $"FALCOM Event geschrieben: Auftrag {payload.AuftragId}, {payload.Quelle} -> {payload.Ziel}, Gewicht {payload.IstGewicht:N2} kg.");
    }

    private async Task ExecuteOpcActionAsync(
        string status,
        Func<OpcClient, string?> action,
        string successMessage)
    {
        SetBusy(true, status);

        try
        {
            string endpoint = TxtEndpoint.Text.Trim();
            string? error = await Task.Run(() => ExecuteOpcAction(endpoint, action));

            if (error is not null)
            {
                ConnectionIndicator.Fill = Brushes.Firebrick;
                TxtStatus.Text = "OPC-Aktion fehlgeschlagen";
                Log($"FEHLER: {error}");
                return;
            }

            ConnectionIndicator.Fill = Brushes.ForestGreen;
            TxtStatus.Text = "Letzte OPC-Aktion erfolgreich";
            Log(successMessage);
        }
        catch (Exception ex)
        {
            ConnectionIndicator.Fill = Brushes.Firebrick;
            TxtStatus.Text = "OPC-Aktion fehlgeschlagen";
            Log($"FEHLER: {ex.Message}");
        }
        finally
        {
            SetBusy(false, TxtStatus.Text);
        }
    }

    private static string? ExecuteOpcAction(
        string endpoint,
        Func<OpcClient, string?> action)
    {
        try
        {
            using var client = new OpcClient(endpoint)
            {
                OperationTimeout = 5000,
                SessionTimeout = 5000
            };

            client.Connect();
            return action(client);
        }
        catch (Exception ex)
        {
            return $"Verbindung zum OPC-UA Server '{endpoint}' fehlgeschlagen: {ex.Message}";
        }
    }

    private static string? WritePayload(OpcClient client, CranePayload payload)
    {
        return WriteNodeChecked(client, NodeAuftragId, payload.AuftragId, "Auftrag-ID")
            ?? WriteNodeChecked(client, NodeQuelle, payload.Quelle, "Kranquelle")
            ?? WriteNodeChecked(client, NodeZiel, payload.Ziel, "Kranziel")
            ?? WriteNodeChecked(client, NodeIstGewicht, payload.IstGewicht, "Istgewicht")
            ?? WriteNodeChecked(client, NodeFehlercode, payload.Fehlercode, "Fehlercode")
            ?? WriteNodeChecked(client, NodeToleranz, payload.Toleranz, "Toleranz");
    }

    private static string? WriteNodeChecked(
        OpcClient client,
        string nodeId,
        object value,
        string nodeName)
    {
        OpcStatus status = client.WriteNode(nodeId, value);

        if (!status.IsBad)
        {
            return null;
        }

        return $"Schreiben von '{nodeName}' fehlgeschlagen. " +
               $"Node: {nodeId}, Status: {status.Code}, Beschreibung: {status.Description}";
    }

    private bool TryReadInput(out CranePayload payload)
    {
        payload = default;

        if (string.IsNullOrWhiteSpace(TxtEndpoint.Text))
        {
            return ShowValidationError("Bitte einen OPC-UA Endpoint eingeben.", TxtEndpoint);
        }

        if (!Uri.TryCreate(TxtEndpoint.Text.Trim(), UriKind.Absolute, out Uri? endpoint)
            || !string.Equals(endpoint.Scheme, "opc.tcp", StringComparison.OrdinalIgnoreCase))
        {
            return ShowValidationError(
                "Der OPC-UA Endpoint muss dem Format opc.tcp://Server:Port entsprechen.",
                TxtEndpoint);
        }

        if (!int.TryParse(TxtAuftragId.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int auftragId))
        {
            return ShowValidationError("Die Auftrag-ID muss eine ganze Zahl sein.", TxtAuftragId);
        }

        string? quelle = GetSelectedValue(CmbQuelle);
        if (quelle is null)
        {
            return ShowValidationError("Bitte eine Kranquelle auswaehlen.", CmbQuelle);
        }

        string? ziel = GetSelectedValue(CmbZiel);
        if (ziel is null)
        {
            return ShowValidationError("Bitte ein Kranziel auswaehlen.", CmbZiel);
        }

        if (!TryParseDouble(TxtGewicht.Text, out double gewicht))
        {
            return ShowValidationError("Das Istgewicht ist keine gueltige Zahl.", TxtGewicht);
        }

        if (!TryParseDouble(TxtToleranz.Text, out double toleranz))
        {
            return ShowValidationError("Die Toleranz ist keine gueltige Zahl.", TxtToleranz);
        }

        if (!int.TryParse(TxtFehlercode.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int fehlercode))
        {
            return ShowValidationError("Der Fehlercode muss eine ganze Zahl sein.", TxtFehlercode);
        }

        payload = new CranePayload(auftragId, quelle, ziel, toleranz, gewicht, fehlercode);
        return true;
    }

    private static string? GetSelectedValue(ComboBox comboBox)
    {
        return comboBox.SelectedValue as string;
    }

    private static IReadOnlyList<CranePosition> LoadCranePositions()
    {
        const string sql = """
            SELECT PositionsTyp, PositionsNr, Bezeichnung, Art
            FROM dbo.FALCOM_KRAN_POSITION
            WHERE Art IN (N'QUELLE', N'ZIEL', N'QUELLE_UND_ZIEL')
            ORDER BY
                CASE PositionsTyp
                    WHEN N'LKW_PLATZ' THEN 1
                    WHEN N'LAGERBOX' THEN 2
                    WHEN N'CHARGIERWAGEN' THEN 3
                    ELSE 4
                END,
                PositionsNr;
            """;

        var positions = new List<CranePosition>();
        using var connection = new SqlConnection(DatabaseConfig.LoadConnectionString());
        using var command = new SqlCommand(sql, connection);
        connection.Open();

        using SqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string positionType = reader.GetString(0);
            int positionNumber = reader.GetInt32(1);
            string description = reader.GetString(2);
            string art = reader.GetString(3);
            string opcValue = CreateOpcPositionValue(positionType, positionNumber);

            positions.Add(new CranePosition(
                opcValue,
                $"{opcValue} - {description}",
                art));
        }

        return positions;
    }

    private static string CreateOpcPositionValue(string positionType, int positionNumber)
    {
        return positionType switch
        {
            "LKW_PLATZ" => $"LKW{positionNumber}",
            "LAGERBOX" => $"BOX {positionNumber}",
            "CHARGIERWAGEN" => $"CW{positionNumber}",
            _ => $"{positionType} {positionNumber}"
        };
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool ShowValidationError(string message, Control control)
    {
        MessageBox.Show(message, "Eingabe pruefen", MessageBoxButton.OK, MessageBoxImage.Warning);
        control.Focus();

        if (control is TextBox textBox)
        {
            textBox.SelectAll();
        }

        return false;
    }

    private void SetBusy(bool isBusy, string status)
    {
        BtnWriteFalcomEvent.IsEnabled = !isBusy;
        TxtEndpoint.IsEnabled = !isBusy;
        CmbQuelle.IsEnabled = !isBusy;
        CmbZiel.IsEnabled = !isBusy;
        TxtStatus.Text = status;
    }

    private void Log(string message)
    {
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        TxtLog.ScrollToEnd();
    }

    private readonly record struct CranePayload(
        int AuftragId,
        string Quelle,
        string Ziel,
        double Toleranz,
        double IstGewicht,
        int Fehlercode);

    private sealed record CranePosition(
        string OpcValue,
        string DisplayText,
        string Art);
}
