using System.IO;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace KranSPS_Simulator;

internal sealed record SimulatorConfiguration(
    string ConnectionString,
    string OpcEndpoint,
    string LogfilePath,
    IReadOnlyList<OpcNodeSubstitution> SimulatorSubstitutionen,
    string KranSpsLebensZaehlerNodeId,
    string FalcomLebensZaehlerNodeId,
    IReadOnlyList<EventNodeConfiguration> KranfahrtBeendetNodes,
    IReadOnlyList<EventNodeConfiguration> KranfahrtAuftragNodes,
    IReadOnlyList<SimEventMappingConfiguration> KranfahrtBeendetZuordnungen,
    IReadOnlyList<EventNodeConfiguration> KranPositionNodes,
    KranPositionGroundPosition Grundstellung,
    IReadOnlyDictionary<long, SimKranPosition> Positionen);

internal sealed record OpcNodeSubstitution(
    string Suchtext,
    string Ersatztext);

internal sealed record EventNodeConfiguration(
    string NodeName,
    string NodeRole,
    string DataType,
    string OpcNode);

internal sealed record SimEventMappingConfiguration(
    long ID,
    string? SourceEventName,
    string? SourceNodeName,
    EventNodeConfiguration TargetNode,
    string Zuordnungstyp,
    string? Fixwert,
    string? Info);

internal sealed record KranPositionGroundPosition(
    int PosKranX,
    int PosKatzeY,
    int PosHubZ);

internal sealed record SimKranPosition(
    long PositionID,
    string PositionsTyp,
    int PositionsNr,
    string Bezeichnung,
    KranPositionGroundPosition Position);

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

        IReadOnlyDictionary<long, SimKranPosition> simPositionen =
            LoadSimPositionen(builder.ConnectionString);
        IReadOnlyList<OpcNodeSubstitution> simulatorSubstitutionen =
            LoadSimulatorSubstitutionen(builder.ConnectionString);

        return new SimulatorConfiguration(
            builder.ConnectionString,
            LoadOpcEndpoint(builder.ConnectionString),
            LoadLogfilePath(settings),
            simulatorSubstitutionen,
            SubstituteOpcNode(LoadKranSpsLebensZaehlerNodeId(builder.ConnectionString), simulatorSubstitutionen),
            SubstituteOpcNode(LoadFalcomLebensZaehlerNodeId(builder.ConnectionString), simulatorSubstitutionen),
            LoadEventOpcNodes(
                builder.ConnectionString,
                "KranfahrtBeendet",
                "KRAN_SPS->FALCOM",
                simulatorSubstitutionen),
            LoadEventOpcNodes(
                builder.ConnectionString,
                "KranfahrtAuftrag",
                "FALCOM->KRAN_SPS",
                simulatorSubstitutionen),
            LoadSimEventZuordnungen(builder.ConnectionString, simulatorSubstitutionen),
            LoadEventOpcNodes(
                builder.ConnectionString,
                "KranPosition",
                "KRAN_SPS->FALCOM",
                simulatorSubstitutionen),
            LoadGrundstellung(
                simPositionen,
                new KranPositionGroundPosition(
                    9000,
                    12000,
                    8500)),
            simPositionen);
    }

    private static string LoadLogfilePath(JsonElement settings)
    {
        if (settings.TryGetProperty("KranSpsSimulationLogfilePath", out JsonElement configuredPath))
        {
            string? value = configuredPath.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return @"C:\Projekte\LOGS\FALCOM\KranSPS_Simulation\KranSPS_Simulation.log";
    }

    private static string LoadOpcEndpoint(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(
            "dbo.FALCOM_GetParameterValue",
            connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 10
        };

        command.Parameters.Add(
            "@Name",
            System.Data.SqlDbType.NVarChar,
            256).Value = "OpcServer";

        connection.Open();

        using SqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return "opc.tcp://localhost:4840";
        }

        return Convert.ToString(reader["Wert"])?.Trim()
               ?? "opc.tcp://localhost:4840";
    }

    private static IReadOnlyList<OpcNodeSubstitution> LoadSimulatorSubstitutionen(string connectionString)
    {
        string rawValue = LoadParameterValue(connectionString, "SimulatorSubstitution", string.Empty);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Array.Empty<OpcNodeSubstitution>();
        }

        string[] parts = rawValue
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var substitutionen = new List<OpcNodeSubstitution>();
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (string.IsNullOrWhiteSpace(parts[i]))
            {
                continue;
            }

            substitutionen.Add(new OpcNodeSubstitution(parts[i], parts[i + 1]));
        }

        return substitutionen;
    }

    private static string LoadParameterValue(
        string connectionString,
        string name,
        string fallback)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(
            "dbo.FALCOM_GetParameterValue",
            connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 10
        };

        command.Parameters.Add(
            "@Name",
            System.Data.SqlDbType.NVarChar,
            256).Value = name;

        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return fallback;
        }

        string? value = Convert.ToString(reader["Wert"])?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string SubstituteOpcNode(
        string opcNode,
        IReadOnlyList<OpcNodeSubstitution> substitutionen)
    {
        if (string.IsNullOrWhiteSpace(opcNode) || substitutionen.Count == 0)
        {
            return opcNode;
        }

        string substituted = opcNode;
        foreach (OpcNodeSubstitution substitution in substitutionen)
        {
            substituted = substituted.Replace(
                substitution.Suchtext,
                substitution.Ersatztext,
                StringComparison.OrdinalIgnoreCase);
        }

        return substituted;
    }

    private static string LoadKranSpsLebensZaehlerNodeId(string connectionString)
    {
        return LoadLebensZaehlerNodeId(
            connectionString,
            "LebensZaehlerKran",
            "KRAN_SPS->FALCOM");
    }

    private static string LoadFalcomLebensZaehlerNodeId(string connectionString)
    {
        return LoadLebensZaehlerNodeId(
            connectionString,
            "LebensZaehlerFalcom",
            "FALCOM->KRAN_SPS");
    }

    private static string LoadLebensZaehlerNodeId(
        string connectionString,
        string eventName,
        string direction)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(
            "dbo.FALCOM_GetEventOpcNodes",
            connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 30
        };

        command.Parameters.Add(
            "@EventName",
            System.Data.SqlDbType.NVarChar,
            128).Value = eventName;
        command.Parameters.Add(
            "@Direction",
            System.Data.SqlDbType.NVarChar,
            64).Value = direction;

        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            string nodeName = Convert.ToString(reader["NodeName"])?.Trim() ?? string.Empty;
            if (!string.Equals(nodeName, "LebensZaehler", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string nodeId = Convert.ToString(reader["OPC_Node"])?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(nodeId)
                && !nodeId.StartsWith("NOCH_ZU_KONFIGURIEREN.", StringComparison.OrdinalIgnoreCase))
            {
                return nodeId;
            }
        }

        return string.Empty;
    }


    private static IReadOnlyList<SimEventMappingConfiguration> LoadSimEventZuordnungen(
        string connectionString,
        IReadOnlyList<OpcNodeSubstitution> substitutionen)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(
            "dbo.FALCOM_Kran_SPS_SIM_GetEventZuordnung",
            connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 30
        };

        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();

        var mappings = new List<SimEventMappingConfiguration>();
        while (reader.Read())
        {
            string targetOpcNode = Convert.ToString(reader["TargetOpcNode"])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetOpcNode)
                || targetOpcNode.StartsWith("NOCH_ZU_KONFIGURIEREN.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            mappings.Add(
                new SimEventMappingConfiguration(
                    Convert.ToInt64(reader["ID"]),
                    reader["SourceEventName"] == DBNull.Value
                        ? null
                        : Convert.ToString(reader["SourceEventName"])?.Trim(),
                    reader["SourceNodeName"] == DBNull.Value
                        ? null
                        : Convert.ToString(reader["SourceNodeName"])?.Trim(),
                    new EventNodeConfiguration(
                        Convert.ToString(reader["TargetNodeName"])?.Trim() ?? string.Empty,
                        Convert.ToString(reader["TargetNodeRole"])?.Trim() ?? string.Empty,
                        Convert.ToString(reader["TargetDataType"])?.Trim() ?? string.Empty,
                        SubstituteOpcNode(targetOpcNode, substitutionen)),
                    Convert.ToString(reader["Zuordnungstyp"])?.Trim() ?? string.Empty,
                    reader["Fixwert"] == DBNull.Value
                        ? null
                        : Convert.ToString(reader["Fixwert"])?.Trim(),
                    reader["Info"] == DBNull.Value
                        ? null
                        : Convert.ToString(reader["Info"])?.Trim()));
        }

        return mappings;
    }
    private static IReadOnlyList<EventNodeConfiguration> LoadEventOpcNodes(
        string connectionString,
        string eventName,
        string direction,
        IReadOnlyList<OpcNodeSubstitution> substitutionen)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(
            "dbo.FALCOM_GetEventOpcNodes",
            connection)
        {
            CommandType = System.Data.CommandType.StoredProcedure,
            CommandTimeout = 30
        };

        command.Parameters.Add(
            "@EventName",
            System.Data.SqlDbType.NVarChar,
            128).Value = eventName;
        command.Parameters.Add(
            "@Direction",
            System.Data.SqlDbType.NVarChar,
            64).Value = direction;

        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();

        var nodes = new List<EventNodeConfiguration>();
        while (reader.Read())
        {
            string opcNode = Convert.ToString(reader["OPC_Node"])?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(opcNode)
                || opcNode.StartsWith("NOCH_ZU_KONFIGURIEREN.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            nodes.Add(
                new EventNodeConfiguration(
                    Convert.ToString(reader["NodeName"])?.Trim() ?? string.Empty,
                    Convert.ToString(reader["NodeRole"])?.Trim() ?? string.Empty,
                    Convert.ToString(reader["DataType"])?.Trim() ?? string.Empty,
                    SubstituteOpcNode(opcNode, substitutionen)));
        }

        return nodes;
    }

    private static IReadOnlyDictionary<long, SimKranPosition> LoadSimPositionen(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        using var command = new SqlCommand(
            """
            SELECT
                ID AS PositionID,
                PositionsTyp,
                PositionsNr,
                Bezeichnung,
                CONVERT(int, AbwurfPosKranY) AS PosKranX,
                CONVERT(int, AbwurfPosKatzeX) AS PosKatzeY,
                8500 AS PosHubZ
            FROM dbo.FALCOM_KRAN_POSITION
            WHERE AbwurfPosKranY IS NOT NULL
              AND AbwurfPosKatzeX IS NOT NULL
            ORDER BY ID;
            """,
            connection)
        {
            CommandTimeout = 10
        };

        connection.Open();
        using SqlDataReader reader = command.ExecuteReader();
        var positionen = new Dictionary<long, SimKranPosition>();
        while (reader.Read())
        {
            long positionId = Convert.ToInt64(reader["PositionID"]);
            positionen[positionId] = new SimKranPosition(
                positionId,
                Convert.ToString(reader["PositionsTyp"])?.Trim() ?? string.Empty,
                Convert.ToInt32(reader["PositionsNr"]),
                Convert.ToString(reader["Bezeichnung"])?.Trim() ?? string.Empty,
                new KranPositionGroundPosition(
                    Convert.ToInt32(reader["PosKranX"]),
                    Convert.ToInt32(reader["PosKatzeY"]),
                    Convert.ToInt32(reader["PosHubZ"])));
        }

        return positionen;
    }

    private static KranPositionGroundPosition LoadGrundstellung(
        IReadOnlyDictionary<long, SimKranPosition> positionen,
        KranPositionGroundPosition fallback)
    {
        SimKranPosition? lagerbox8 = positionen.Values.FirstOrDefault(
            position => string.Equals(position.PositionsTyp, "LAGERBOX", StringComparison.OrdinalIgnoreCase)
                        && position.PositionsNr == 8);
        if (lagerbox8 is null)
        {
            return fallback;
        }

        return lagerbox8.Position;
    }
}

internal sealed record AktuelleFahrtSimulation(
    long ID,
    int TelegrammNummer,
    string AuftragsTyp,
    long AuftragID,
    int AuftragTeilfahrt,
    string Status,
    long QuellePositionID,
    long ZielPositionID,
    decimal SollMengeKg,
    string QuelleBezeichnung,
    string ZielBezeichnung,
    KranPositionGroundPosition Quelle,
    KranPositionGroundPosition Ziel)
{
    public decimal? IstGewichtKg { get; set; }
}

