# Kran-Simulator: Koordinaten und Zielpositionen

Alle Positionswerte werden in Millimetern geführt.

## Bewegungsgrenzen

| Achse | Minimum | Maximum |
|---|---:|---:|
| PosKranX | 1.000 | 24.000 |
| PosKatzeY | 500 | 23.500 |
| PosHubZ | 200 (oben) | 8.500 (unten) |

Die X- und Y-Koordinaten werden linear über das gesamte sichtbare
Bewegungsfeld des Krans abgebildet. Die Z-Koordinate wird linear zwischen
der oberen und unteren Hakenposition abgebildet.

## Quell- und Zielpositionen

| Position | PosKranX | PosKatzeY | PosHubZ |
|---|---:|---:|---:|
| LKW1 | 5.609 | 1.938 | 7.193 |
| LKW2 | 12.500 | 1.938 | 7.193 |
| LKW3 | 19.391 | 1.938 | 7.193 |
| BOX 1 | 8.193 | 16.043 | 7.454 |
| BOX 2 | 12.500 | 15.594 | 7.454 |
| BOX 3 | 16.807 | 15.594 | 7.454 |
| BOX 4 | 8.193 | 13.348 | 7.454 |
| BOX 5 | 12.500 | 12.000 | 7.454 |
| BOX 6 | 16.807 | 12.000 | 7.454 |
| BOX 7 | 8.193 | 10.652 | 7.454 |
| BOX 8 | 12.500 | 8.406 | 7.454 |
| BOX 9 | 16.807 | 8.406 | 7.454 |
| BOX 10 | 8.193 | 7.957 | 7.454 |
| CW1 | 5.609 | 22.063 | 7.128 |
| CW2 | 12.500 | 22.063 | 7.128 |
| CW3 | 19.391 | 22.063 | 7.128 |

Die Werte werden zentral in `KranSimulator/CraneSimulation.cs` erzeugt.
Animation und Statusanzeige verwenden dieselbe Positionsquelle.
