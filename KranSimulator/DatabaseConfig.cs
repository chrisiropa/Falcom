using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace KranSimulator;

internal static class DatabaseConfig
{
    public static string LoadConnectionString()
    {
        string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        JsonElement settings = document.RootElement.GetProperty("Appsettings");

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.GetProperty("Server").GetString(),
            InitialCatalog = settings.GetProperty("DatabaseName").GetString(),
            UserID = settings.GetProperty("User").GetString(),
            Password = settings.GetProperty("Password").GetString(),
            Encrypt = false
        };

        return builder.ConnectionString;
    }
}
