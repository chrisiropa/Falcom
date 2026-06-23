using Microsoft.Data.SqlClient;

namespace KranSPS_Simulator;

internal static class SpsStatusConfiguration
{
    public const string EventName = "KranSpsStatus";
    public const string NodeName = "Status";
    public const string FallbackNodeId =
        "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Status.SimulatorStatus";

    public static StatusNodeConfiguration Load(string connectionString)
    {
        const string sql = """
            SELECT TOP (1) nodes.OPC_Node
            FROM dbo.FALCOM_EVENTS AS events
            INNER JOIN dbo.FALCOM_EVENT_OPC_NODES AS nodes
                ON nodes.EventID = events.ID
            WHERE events.EventName = @EventName
              AND events.Direction = N'FALCOM->KRAN_SPS'
              AND nodes.NodeName = @NodeName
              AND events.IsActive = 1;
            """;

        try
        {
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@EventName", EventName);
            command.Parameters.AddWithValue("@NodeName", NodeName);
            connection.Open();

            string? nodeId = Convert.ToString(command.ExecuteScalar())?.Trim();
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                return new StatusNodeConfiguration(nodeId, true);
            }
        }
        catch
        {
            // Der Simulator bleibt mit klar gekennzeichnetem Fallback nutzbar.
        }

        return new StatusNodeConfiguration(FallbackNodeId, false);
    }
}

internal sealed record StatusNodeConfiguration(
    string NodeId,
    bool LoadedFromDatabase);
