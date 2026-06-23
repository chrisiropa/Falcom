using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Opc.UaFx;
using Opc.UaFx.Client;

namespace KranSPS_Simulator;

public partial class MainWindow : Window
{
    private readonly StatusNodeConfiguration statusNodeConfiguration;

    public MainWindow()
    {
        InitializeComponent();

        SimulatorConfiguration configuration = DatabaseConfig.Load();
        statusNodeConfiguration = SpsStatusConfiguration.Load(
            configuration.ConnectionString);

        TxtEndpoint.Text = configuration.OpcEndpoint;
        TxtStatusNode.Text = statusNodeConfiguration.NodeId;
        TxtConfigurationSource.Text = statusNodeConfiguration.LoadedFromDatabase
            ? "Node aus FG.dbo.FALCOM_EVENTS geladen."
            : "Fallback-Node aktiv – später in FG.dbo.FALCOM_EVENTS konfigurieren.";

        CmbStatus.ItemsSource = SpsStatus.All;
        CmbStatus.SelectedIndex = 0;
        Log("Kran-SPS-Simulator bereit.");
    }

    private async void BtnWriteStatus_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (CmbStatus.SelectedItem is not SpsStatus selectedStatus)
        {
            MessageBox.Show(
                "Bitte einen SPS-Status auswählen.",
                "Status prüfen",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!TryValidateEndpoint(out string endpoint))
        {
            return;
        }

        string nodeId = TxtStatusNode.Text.Trim();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            MessageBox.Show(
                "Bitte eine OPC Node-ID für die SPS-Statusvariable eingeben.",
                "Statusvariable prüfen",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TxtStatusNode.Focus();
            return;
        }

        SetBusy(true, $"{selectedStatus.Name} wird geschrieben...");

        try
        {
            string? error = await Task.Run(
                () => WriteStatus(endpoint, nodeId, selectedStatus));

            if (error is not null)
            {
                ConnectionIndicator.Fill = Brushes.Firebrick;
                TxtApplicationStatus.Text = "OPC-Schreibvorgang fehlgeschlagen";
                Log($"FEHLER: {error}");
                return;
            }

            ConnectionIndicator.Fill = Brushes.ForestGreen;
            TxtApplicationStatus.Text =
                $"SPS-Status: {selectedStatus.Name} ({selectedStatus.Value})";
            Log(
                $"Status geschrieben: {selectedStatus.Name} " +
                $"({selectedStatus.Value}) -> {nodeId}");
        }
        catch (Exception ex)
        {
            ConnectionIndicator.Fill = Brushes.Firebrick;
            TxtApplicationStatus.Text = "OPC-Schreibvorgang fehlgeschlagen";
            Log($"FEHLER: {ex.Message}");
        }
        finally
        {
            SetBusy(false, TxtApplicationStatus.Text);
        }
    }

    private void CmbStatus_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        TxtStatusDescription.Text =
            (CmbStatus.SelectedItem as SpsStatus)?.Description
            ?? string.Empty;
    }

    private string? WriteStatus(
        string endpoint,
        string nodeId,
        SpsStatus status)
    {
        try
        {
            string? licenseKey =
                Environment.GetEnvironmentVariable("FALCOM_TRAEGER_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(licenseKey))
            {
                Opc.UaFx.Client.Licenser.LicenseKey = licenseKey;
            }

            using var client = new OpcClient(endpoint)
            {
                OperationTimeout = 5_000,
                SessionTimeout = 5_000
            };
            client.Connect();

            OpcStatus writeStatus = client.WriteNode(
                nodeId,
                status.Value);
            return writeStatus.IsBad
                ? $"Status '{status.Name}' konnte nicht geschrieben werden. " +
                  $"OPC-Status: {writeStatus.Code}, {writeStatus.Description}"
                : null;
        }
        catch (Exception ex)
        {
            return $"Verbindung zu '{endpoint}' fehlgeschlagen: {ex.Message}";
        }
    }

    private bool TryValidateEndpoint(out string endpoint)
    {
        endpoint = TxtEndpoint.Text.Trim();

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
            && string.Equals(
                uri.Scheme,
                "opc.tcp",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        MessageBox.Show(
            "Der OPC-UA Endpoint muss dem Format opc.tcp://Server:Port entsprechen.",
            "Verbindung prüfen",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        TxtEndpoint.Focus();
        TxtEndpoint.SelectAll();
        return false;
    }

    private void SetBusy(bool isBusy, string status)
    {
        BtnWriteStatus.IsEnabled = !isBusy;
        CmbStatus.IsEnabled = !isBusy;
        TxtEndpoint.IsEnabled = !isBusy;
        TxtStatusNode.IsEnabled = !isBusy;
        TxtApplicationStatus.Text = status;
    }

    private void Log(string message)
    {
        TxtLog.AppendText(
            $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        TxtLog.ScrollToEnd();
    }
}
