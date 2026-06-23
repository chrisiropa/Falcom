using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace KranSPS_Simulator;

internal sealed record SimulatorConfiguration(
    string ConnectionString,
    string OpcEndpoint);

internal static class DatabaseConfig
{
    public static SimulatorConfiguration Load()
    {
        string settingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "appsettings.json");
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(settingsPath));
        JsonElement settings = document.RootElement.GetProperty("Appsettings");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.GetProperty("Server").GetString(),
            InitialCatalog = settings.GetProperty("DatabaseName").GetString(),
            UserID = settings.GetProperty("User").GetString(),
            Password = settings.GetProperty("Password").GetString(),
            Encrypt = false
        };

        return new SimulatorConfiguration(
            builder.ConnectionString,
            LoadOpcEndpoint(builder.ConnectionString));
    }

    private static string LoadOpcEndpoint(string connectionString)
    {
        const string sql = """
            SELECT TOP (1) Wert
            FROM dbo.FALCOM_PARAMETER
            WHERE Name = N'OpcServer';
            """;

        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(sql, connection);
        connection.Open();
        return Convert.ToString(command.ExecuteScalar())?.Trim()
            ?? "opc.tcp://localhost:4840";
    }
}
