using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Opc.UaFx;
using Opc.UaFx.Client;

namespace KranSimulator;

public partial class MainWindow : Window
{
    private const string NodeAuftragId = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.AuftragID";
    private const string NodeQuelle = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.KranQuelle";
    private const string NodeToleranz = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Toleranz";
    private const string NodeIstGewicht = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.IstGewicht";
    private const string NodeFehlercode = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Fehlercode";
    private const string NodeKranfahrtBeendet = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Comands.Stop";

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

    private async void BtnUpdateValues_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInput(out CranePayload payload))
        {
            return;
        }

        await ExecuteOpcActionAsync(
            "Nutzdaten werden geschrieben...",
            client => WritePayload(client, payload),
            $"Nutzdaten fuer Auftrag {payload.AuftragId} erfolgreich geschrieben.");
    }

    private async void BtnStopSignal_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInput(out CranePayload payload))
        {
            return;
        }

        await ExecuteOpcActionAsync(
            "Kranfahrt-Ende wird simuliert...",
            client =>
            {
                string? error = WritePayload(client, payload);
                if (error is not null)
                {
                    return error;
                }

                error = WriteNodeChecked(client, NodeKranfahrtBeendet, false, "Kranfahrt beendet (Reset)");
                if (error is not null)
                {
                    return error;
                }

                Thread.Sleep(1200);
                return WriteNodeChecked(client, NodeKranfahrtBeendet, true, "Kranfahrt beendet");
            },
            $"Kranfahrt beendet: Auftrag {payload.AuftragId}, Gewicht {payload.IstGewicht:N2} kg.");
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
            string? error = await Task.Run(() =>
            {
                using var client = new OpcClient(endpoint);
                client.Connect();
                return action(client);
            });

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

    private static string? WritePayload(OpcClient client, CranePayload payload)
    {
        return WriteNodeChecked(client, NodeAuftragId, payload.AuftragId, "Auftrag-ID")
            ?? WriteNodeChecked(client, NodeQuelle, payload.Quelle, "Kranquelle")
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

        if (!int.TryParse(TxtAuftragId.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int auftragId))
        {
            return ShowValidationError("Die Auftrag-ID muss eine ganze Zahl sein.", TxtAuftragId);
        }

        if (string.IsNullOrWhiteSpace(TxtQuelle.Text))
        {
            return ShowValidationError("Bitte eine Kranquelle eingeben.", TxtQuelle);
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

        payload = new CranePayload(auftragId, TxtQuelle.Text.Trim(), toleranz, gewicht, fehlercode);
        return true;
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool ShowValidationError(string message, System.Windows.Controls.TextBox textBox)
    {
        MessageBox.Show(message, "Eingabe pruefen", MessageBoxButton.OK, MessageBoxImage.Warning);
        textBox.Focus();
        textBox.SelectAll();
        return false;
    }

    private void SetBusy(bool isBusy, string status)
    {
        BtnUpdateValues.IsEnabled = !isBusy;
        BtnStopSignal.IsEnabled = !isBusy;
        TxtEndpoint.IsEnabled = !isBusy;
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
        double Toleranz,
        double IstGewicht,
        int Fehlercode);
}
