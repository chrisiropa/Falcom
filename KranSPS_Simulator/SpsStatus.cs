namespace KranSPS_Simulator;

internal sealed record SpsStatus(
    int Value,
    string Name,
    string Description)
{
    public static IReadOnlyList<SpsStatus> All { get; } =
    [
        new(0, "Bereit", "SPS ist bereit und wartet auf einen Auftrag."),
        new(1, "Kranfahrt läuft", "Der Kran bearbeitet aktuell einen Fahrauftrag."),
        new(2, "Kranfahrt beendet", "Die Zielposition wurde erreicht."),
        new(3, "Schrott abgeworfen", "Das Material wurde am Ziel abgeworfen."),
        new(4, "Störung", "Die SPS meldet einen simulierten Fehlerzustand.")
    ];

    public string DisplayText => $"{Value} - {Name}";
}
