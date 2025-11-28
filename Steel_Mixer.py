import sys
import numpy as np
from scipy.optimize import minimize

# Zielfunktion: Summe der quadrierten Abweichungen von den Zielwerten
def objective_function(x, data):
    errechnet_cu = np.sum(x * data['cu'])  # Berechneter Cu-Wert
    errechnet_mn = np.sum(x * data['mn'])  # Berechneter Mn-Wert

    # Zielfunktion: Summe der quadrierten Differenzen zu den Zielwerten
    return (data['ziel_cu'] - errechnet_cu)**2 + (data['ziel_mn'] - errechnet_mn)**2

# Nebenbedingung: Die Summe der Anteile muss 1 (100%) sein
def sum_constraint(x):
    return np.sum(x) - 1  # Die Summe der Anteile muss genau 1 (100%) sein

# Constraint: Mindestens 40% müssen von Platz 16 kommen
def min_40_percent_platz_16_constraint(x):
    return x[15] - 0.40  # Platz 16 muss mindestens 40% betragen

# Obergrenze für Kupfer: Kupferwert muss kleiner oder gleich der Obergrenze sein
def cu_upper_constraint(x, data):
    upper_cu = np.sum(x * data['cu'])
    return data['obergrenze_cu'] - upper_cu

# Obergrenze für Mangan: Manganwert muss kleiner oder gleich der Obergrenze sein
def mn_upper_constraint(x, data):
    upper_mn = np.sum(x * data['mn'])
    return data['obergrenze_mn'] - upper_mn

def min_10_percent_platz_01_constraint(x):  #zum üben
    return x[0] - 0.05

# Hauptfunktion: Optimierung mit scipy.optimize.minimize
# def steel_mixer(cu_values, mn_values, ziel_cu, ziel_mn, obergrenze_cu, obergrenze_mn):
def steel_mixer():
    x0 = np.ones(16) / 16  # Anfangswerte für die Optimierungsvariablen (gleichmäßig verteilt)
    lb = np.zeros(16)  # Untere Schranken
    ub = np.ones(16)   # Obere Schranken
    # ub = np.full(16, 20)  # Erstellt ein Array mit 16 Elementen, alle mit dem Wert 20

    # Cu- und Mn-Daten
    data = {
        'cu': cu_values,
        'mn': mn_values,
        'ziel_cu': ziel_cu,
        'ziel_mn': ziel_mn,
        'obergrenze_cu': obergrenze_cu,
        'obergrenze_mn': obergrenze_mn
    }

    # Schranken für die Variablen
    bounds = [(lb[i], ub[i]) for i in range(16)]

    # Nebenbedingungen: Summe der Anteile muss 1 sein (100%), mindestens 40% von Platz 16, und Obergrenzen für Cu und Mn
    constraints = [{'type': 'eq', 'fun': sum_constraint},
                   # {'type': 'ineq', 'fun': min_10_percent_platz_01_constraint}, # zum üben
                   {'type': 'ineq', 'fun': min_40_percent_platz_16_constraint},
                   {'type': 'ineq', 'fun': cu_upper_constraint, 'args': (data,)},
                   {'type': 'ineq', 'fun': mn_upper_constraint, 'args': (data,)}]

    # Optimierung durchführen
    result = minimize(objective_function, x0, args=(data,), method='SLSQP',
                      bounds=bounds, constraints=constraints, options={'disp': False})

    if result.success:
        return result.x  # Optimierte Anteile zurückgeben
    else:
        print("Optimierung nicht erfolgreich.")
        # print(result);
        return result.x



if __name__ == "__main__":
    # Beispielaufruf über Kommandozeile, dann kann man es auch hier testen
    # 16 Kupferwerte, 16 Manganwerte Am Ende die Zielwert-Vorgabe für Kupfer und Mangan
    # %Run Steel_Mixer.py 0.084 0.026 0.014 0.69 0.037 0.015 0.016 0.017 0.5 0.081 0.017 0.014 0.015 0.054 0.5 0.077 1.05 0.432 0.623 1.633 0.394 0.18 0.282 0.5 1.659 0.157 0.147 0.217 0.14 0.407 1.649 0.12 0.08 0.15
    
    # Argumente von C# (über sys.argv einlesen)
    args = sys.argv[1:]

    # Ersten 16 Argumente sind Cu-Werte
    cu_values = list(map(float, args[0:16]))

    # Nächsten 16 Argumente sind Mn-Werte
    mn_values = list(map(float, args[16:32]))

    # Die letzten beiden Argumente sind Zielwerte
    ziel_cu = float(args[32])
    ziel_mn = float(args[33])
    
    obergrenze_cu = ziel_cu  # Beispiel-Obergrenze für Kupfer
    obergrenze_mn = ziel_mn  # Beispiel-Obergrenze für Mangan

    # Aufruf der steel_mixer Funktion
    x_opt = steel_mixer()
    
    # Ausgabe der optimierten Anteile und der berechneten Werte mit Komma anstatt Punkt
if x_opt is not None:
    # print("Optimierte Anteile (auf 4 Nachkommastellen formatiert mit Komma):")
    for value in x_opt:
        print(f"{value:.4f}".replace('.', ','))

    # Berechnung der Kupfer- und Mangananteile
    berechnet_cu = np.sum(x_opt * cu_values)
    berechnet_mn = np.sum(x_opt * mn_values)

    # Ausgabe der berechneten Werte mit Komma
    print(f"{berechnet_cu:.4f}".replace('.', ','))
    print(f"{berechnet_mn:.4f}".replace('.', ','))
    

