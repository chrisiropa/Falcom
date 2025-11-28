import sys
import json
import numpy as np
from scipy.optimize import minimize

# Zielfunktion: Summe der quadrierten Abweichungen von den Zielwerten
def objective_function(x, data):
    errechnet_cu = np.sum(x * data['cu'])  # Berechneter Cu-Wert
    errechnet_mn = np.sum(x * data['mn'])  # Berechneter Mn-Wert

    # Zielfunktion: Summe der quadrierten Differenzen zu den Zielwerten
    return (data['ziel_cu'] - errechnet_cu)**2 + (data['ziel_mn'] - errechnet_mn)**2

# Nichtlineare Einschränkungen: Berechnete Werte von Cu und Mn müssen obere Grenzen einhalten
def constraints(data):
    cons = []

    # Einschränkung: x[15] (entspricht x(16) in MATLAB) muss zwischen 40% und 50% der Gesamtsumme von x liegen
    cons.append({
        'type': 'ineq',
        'fun': lambda x: x[15] - 0.40 * np.sum(x)  # x[15] >= 40% der Gesamtsumme
    })
    
    cons.append({
        'type': 'ineq',
        'fun': lambda x: 0.45 * np.sum(x) - x[15]  # x[15] <= 50% der Gesamtsumme
    })
    
    # Zusätzliche Einschränkungen: Cu und Mn dürfen die Zielwerte nicht überschreiten
    cons.append({
        'type': 'ineq',
        'fun': lambda x: data['ziel_cu'] - np.sum(x * data['cu'])  # errechnetes Cu <= Ziel-Cu
    })
    
    cons.append({
        'type': 'ineq',
        'fun': lambda x: data['ziel_mn'] - np.sum(x * data['mn'])  # errechnetes Mn <= Ziel-Mn
    })

    return cons

# Hauptfunktion: Optimierung mit scipy.optimize.minimize
def steel_mixer(cu_values, mn_values, ziel_cu, ziel_mn):
    # Anfangswerte für die Optimierungsvariablen
    x0 = np.ones(16)
    lb = np.zeros(16)  # Untere Schranken
    ub = np.ones(16)   # Obere Schranken

    # Cu- und Mn-Daten
    data = {
        'cu': cu_values,
        'mn': mn_values,
        'ziel_cu': ziel_cu,
        'ziel_mn': ziel_mn
    }

    # Schranken für die Variablen
    bounds = [(lb[i], ub[i]) for i in range(16)]

    # Optimierungsoptionen
    options = {
        'disp': False,  # Keine Info über das Ergebnis der Optimierung ausgeben
        'maxiter': 10000,
        'ftol': 1e-9
    }

    # Optimierung durchführen
    result = minimize(objective_function, x0, args=(data,), method='SLSQP', bounds=bounds, constraints=constraints(data), options=options)

    x_opt = result.x

    # Berechnete Cu- und Mn-Mengen anzeigen
    errechnet_cu = np.sum(x_opt * data['cu'])
    errechnet_mn = np.sum(x_opt * data['mn'])

    # Erstelle das JSON-Ergebnis
    result_dict = {
        "x_opt": [f"{val:.6f}" for val in x_opt],  # Optimierte Werte
        "errechnet_cu": round(errechnet_cu, 4),    # Gerundete Cu-Werte
        "errechnet_mn": round(errechnet_mn, 4)     # Gerundete Mn-Werte
    }

    # Ausgabe als JSON
    print(json.dumps(result_dict))

# Ausführen des Hauptprogramms
if __name__ == "__main__":
    # Argumente von C# (über sys.argv einlesen)
    args = sys.argv[1:]

    # Ersten 16 Argumente sind Cu-Werte
    cu_values = list(map(float, args[0:16]))

    # Nächsten 16 Argumente sind Mn-Werte
    mn_values = list(map(float, args[16:32]))

    # Die letzten beiden Argumente sind Zielwerte
    ziel_cu = float(args[32])
    ziel_mn = float(args[33])

    # Aufruf der steel_mixer Funktion
    steel_mixer(cu_values, mn_values, ziel_cu, ziel_mn)
