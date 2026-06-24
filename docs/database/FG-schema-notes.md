# FG SQL Server Schema Notes

Quelle: `C:\Projekte\Hundhausen 2026\Dokumente\FG_alsScript.sql`

Stand des Skripts: 2026-06-10 15:07:10

## Umfang

- Datenbank: `FG`
- Tabellen: 105
- Views: 15
- Funktionen: 31
- Prozeduren: 10
- Primary Keys: 100
- Foreign Keys: 97

## Falcom Tabellen

### `dbo.FALCOM_AUFTRAG`

Auftragskopf fuer Chargierwagen-Auftraege.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `Reihenfolge` int
- `ChargierwagenNr` int, Check: `1`, `2`, `3`
- `EisensorteID` bigint
- Sollwerte: `Soll_Mn`, `Soll_Cu`, `Soll_C`, `Soll_Si`, `Soll_Cr`, `Soll_Mg`
- `ZielgewichtKg` bigint, Default `13800`
- `Status` nvarchar(30), Default `ANGELEGT`, Check: `ANGELEGT`, `FREIGEGEBEN`, `IN_ARBEIT`, `FERTIG`, `ABGEBROCHEN`, `FEHLER`
- `Gesperrt` bit, Default `0`
- `BearbeitungGestartet` bit, Default `0`
- Zeitfelder: `EingabeDatumZeit`, `StartDatumZeit`, `FertigDatumZeit`, `GeaendertDatumZeit`, `DatumZeit_Freigegeben`
- `Bemerkung` nvarchar(1024)

### `dbo.FALCOM_AUFTRAG_BERECHNET`

Berechnete Material-/Box-Zuordnung zu einem Auftrag.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `BoxID` bigint, FK zu `FALCOM_LAGER.ID`
- `Menge` bigint
- `AuftragID` bigint, FK zu `FALCOM_AUFTRAG.ID`
- `PositionsNr` int
- `MindestProzent` decimal(6,3)
- `IstRandbedingung` bit, Default `0`
- `BerechnetProzent` decimal(9,6)
- `BerechnetKg` decimal(12,3)
- `AnalyseQuelleID` bigint, nullable FK zu `FALCOM_LAGER.ID`
- `BerechnungAktiv` bit, Default `0`

Index:

- `UX_FALCOM_AUFTRAG_BERECHNET_AUFTRAG_BOX` unique nonclustered

### `dbo.FALCOM_AUFTRAG_PRODUKTION`

Produktionsauftrag bzw. auszufuehrender Materialschritt zu einem Auftrag.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `AuftragID` bigint, FK zu `FALCOM_AUFTRAG.ID`
- `PositionsNr` int
- `QuellTyp` nvarchar(30), Check: `LAGERBOX`, `KREISLAUF`, `REINKOMPONENTE`
- `QuellID` bigint
- `SollMengeKg` decimal(12,3)
- `Status` nvarchar(30), Default `OFFEN`, Check: `OFFEN`, `IN_ARBEIT`, `FERTIG`, `ABGEBROCHEN`, `FEHLER`
- Zeitfelder: `ErstelltDatumZeit`, `StartDatumZeit`, `FertigDatumZeit`
- `Bemerkung` nvarchar(1024)

### `dbo.FALCOM_AUFTRAG_PRODUKTION_DETAIL`

Ist-Daten einzelner Abwuerfe zu einer Produktion.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `ProduktionID` bigint, FK zu `FALCOM_AUFTRAG_PRODUKTION.ID`
- `AbwurfNr` int
- `AbwurfZeit` datetime, Default `getdate()`
- `BoxID` bigint nullable
- `IstMengeKg` decimal(12,3)
- Analysewerte: `Analyse_Mn`, `Analyse_Cu`, `Analyse_C`, `Analyse_Si`, `Analyse_Cr`, `Analyse_Mg`

### `dbo.FALCOM_EINLAGER_AUFTRAG`

Einlagerauftrag von LKW-Platz zu Zielbox.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `LkwPlatzNr` int
- `ZielBoxID` bigint
- `SollMengeKg` decimal(12,3)
- `IstMengeKg` decimal(12,3)
- Analysewerte: `Analyse_Mn`, `Analyse_Cu`, `Analyse_C`, `Analyse_Si`, `Analyse_Cr`, `Analyse_Mg`
- `EingabeZeit` datetime2(0), Default `sysdatetime()`
- `Bemerkung` nvarchar(500)

### `dbo.FALCOM_EINLAGER_AUFTRAG_DETAIL`

Ist-Daten einzelner Abwuerfe zu einem Einlagerauftrag.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `LagerAuftragID` bigint, FK zu `FALCOM_EINLAGER_AUFTRAG.ID`
- `AbwurfNr` int
- `IstMengeKg` decimal(12,3)
- `AbwurfZeit` datetime2(0), Default `sysdatetime()`

### `dbo.FALCOM_KRAN_POSITION`

Definition von Kranpositionen.

Wichtige Felder:

- `ID` bigint, Primary Key
- `PositionsTyp` nvarchar(32), Check: `CHARGIERWAGEN`, `LAGERBOX`, `LKW_PLATZ`
- `PositionsNr` int
- `Bezeichnung` nvarchar(128)
- `Bemerkung` nvarchar(512), Default leerer String

### `dbo.FALCOM_AKTUELLE_FAHRT`

Aktuelle Fahrt beziehungsweise aktuell anstehender Kran-Fahrbefehl.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `Reihenfolge` int
- `Prioritaet` int, Default `100`
- `AuftragsTyp` nvarchar(30), Check: `CHARGIEREN`, `EINLAGERN`
- `AuftragID` bigint
- `ErstelltDatumZeit` datetime, Default `sysdatetime()`
- `FertigDatumZeit` datetime nullable
- `Bemerkung` nvarchar(500)

### `dbo.FALCOM_LAGER`

Lagerplaetze, Boxen und Analysewerte.

Wichtige Felder:

- `ID` bigint, Primary Key
- `Lagerplatz` bigint
- `Bezeichnung` text nullable
- `Aktiv` bit, Default `1`
- `Restmenge` bigint, Default `25000`
- Analysewerte: `Cu`, `Mn`, `C`, `Si`
- Grenzwerte: `Cu_Min`, `Cu_Max`, `Mn_Min`, `Mn_Max`
- `PlatzTyp` nvarchar(30), Default `LAGERBOX`, Check: `LAGERBOX`, `LKW_PLATZ`
- `ZielLagerplatz` bigint nullable
- `ZugeordneteLagerID` bigint nullable, Self-FK zu `FALCOM_LAGER.ID`

Hinweis zum aktuellen C#-Code:

- `Lager.cs` liest aus `FALCOM_Lager` zusaetzlich die Spalten `S` und `Mg`.
- Diese Spalten sind im Skript `FG_alsScript.sql` nicht in `FALCOM_LAGER` definiert.
- Vor Anpassungen an `Lager.cs` oder SQL-Abfragen klaeren, ob das Skript unvollstaendig ist oder der Code noch auf eine aeltere/andere Tabellenstruktur zielt.

### `dbo.FALCOM_PARAMETER`

Key-Value-Parameter fuer Falcom.

Wichtige Felder:

- `ID` bigint identity, Primary Key
- `Name` nvarchar(256)
- `Wert` nvarchar(1024)
- `Beschreibung` nvarchar(1024)

Aktueller Code nutzt:

- `select * from FALCOM_PARAMETER where Name like 'PYTHON_EXE'`

## Beziehungen Falcom

- `FALCOM_AUFTRAG_BERECHNET.AuftragID` -> `FALCOM_AUFTRAG.ID`
- `FALCOM_AUFTRAG_BERECHNET.BoxID` -> `FALCOM_LAGER.ID`
- `FALCOM_AUFTRAG_BERECHNET.AnalyseQuelleID` -> `FALCOM_LAGER.ID`
- `FALCOM_AUFTRAG_PRODUKTION.AuftragID` -> `FALCOM_AUFTRAG.ID`
- `FALCOM_AUFTRAG_PRODUKTION_DETAIL.ProduktionID` -> `FALCOM_AUFTRAG_PRODUKTION.ID`
- `FALCOM_EINLAGER_AUFTRAG_DETAIL.LagerAuftragID` -> `FALCOM_EINLAGER_AUFTRAG.ID`
- `FALCOM_LAGER.ZugeordneteLagerID` -> `FALCOM_LAGER.ID`

## Wichtige Nicht-Falcom Bereiche

- `LEG_*`: Legierungs-/Analyse-/Eisensortenbereich
- `AAD_*`: Analyse-/Spektrometerdaten
- `ORG_*`: Anlagen, Rechner, Rechte und Benutzer
- `DC_*`: Datenclient-/SPS-Kommunikation
- `ST_*`: Alarm-/Stoerungs-/Verdichtungsdaten
- `MDDS_*`: Kanal-/Modul-/Systemdaten
- `WEB_*` und `WeBS_*`: Web-Sessions und Web-Konfiguration

## Arbeitsregeln fuer kommende Programmieraufgaben

- Fuer neue Falcom-Queries zuerst diese Datei und danach bei Bedarf das Originalskript pruefen.
- Status- und Typwerte nur gemaess Check Constraints verwenden.
- Bei Schreiboperationen Defaults und Foreign Keys beachten.
- Bei Lagerzugriffen die Abweichung `S`/`Mg` gegen das aktuelle Zielsystem pruefen.
