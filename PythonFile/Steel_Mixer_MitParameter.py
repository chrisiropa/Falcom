import numpy as np
import time
from scipy.optimize import minimize

# Zielfunktion: Summe der quadrierten Abweichungen von den Zielwerten
def objective_function(x, data):
    errechnet_cu = np.sum(x * data['cu'])  # Berechneter Cu-Wert
    errechnet_mn = np.sum(x * data['mn'])  # Berechneter Mn-Wert

    # Zielfunktion: Summe der quadrierten Differenzen zu den Zielwerten
    return (data['ziel_cu'] - errechnet_cu)**2 + (data['ziel_mn'] - errechnet_mn)**2

# Nichtlineare Einschränkungen: Berechnete Werte von Cu und Mn müssen die Zielwerte nicht überschreiten
def constraints(data):
    cons = []

    # Einschränkung: x[15] (entspricht x(16) in MATLAB) muss zwischen 40% und 45% der Gesamtsumme von x liegen
    cons.append({
        'type': 'ineq',
        'fun': lambda x: x[15] - 0.40 * np.sum(x)  # x[15] >= 40% der Gesamtsumme
    })
    
    cons.append({
        'type': 'ineq',
        'fun': lambda x: 0.45 * np.sum(x) - x[15]  # x[15] <= 45% der Gesamtsumme
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
def steel_mixer():
    # Anfangswerte für die Optimierungsvariablen
    x0 = np.ones(16)
    lb = np.zeros(16)  # Untere Schranken
    ub = np.ones(16)   # Obere Schranken

    # Cu- und Mn-Daten
    data = {
        'cu': [0.084, 0.026, 0.014, 0.69, 0.037, 0.015, 0.016, 0.017, 0.5, 0.081, 0.017, 0.014, 0.015, 0.054, 0.5, 0.5],
        'mn': [1.05, 0.431, 0.623, 1.633, 0.394, 0.18, 0.282, 0.5, 1.659, 0.157, 0.147, 0.217, 0.14, 0.407, 1.649, 1.5],
        'ziel_cu': 0.08,
        'ziel_mn': 0.20
    }

    # Schranken für die Variablen
    bounds = [(lb[i], ub[i]) for i in range(16)]

    # Optimierungsoptionen
    options = {
        'disp': True,
        'maxiter': 20000,
        'ftol': 1e-12
    }

    # Optimierung durchführen
    result = minimize(objective_function, x0, args=(data,), method='SLSQP',
                      bounds=bounds, constraints=constraints(data), options=options)

    # Überprüfung, ob die Optimierung erfolgreich war
    if result.success:
        print("Optimierung erfolgreich.")
    else:
        print("Optimierung nicht erfolgreich.")
    
    x_opt = result.x

    # Sicherstellen, dass alle Ergebnisse positiv sind
    if np.all(x_opt >= 0):
        print("16 Ergebnisse. Alle sind positiv. Das ist gut.")
    else:
        print("Negative Ergebnisse gefunden. Das ist nicht gut.")

    # Berechnete Cu- und Mn-Mengen anzeigen
    errechnet_cu = np.sum(x_opt * data['cu'])
    errechnet_mn = np.sum(x_opt * data['mn'])

    print("Optimierte Anteile Lagerplätze:")
    for i in range(len(data['cu'])):
        formatted_number = f"{x_opt[i]:.6f}".replace('.', ',')
        print(formatted_number)

    print("\nErrechneter Kupfer-Anteil (Cu):")
    print(f"{errechnet_cu:.4f}")

    print("Errechneter Mangan-Anteil (Mn):")
    print(f"{errechnet_mn:.4f}")

# Ausführen des Hauptprogramms
if __name__ == "__main__":
    # Startzeit der Stoppuhr
    start_time = time.time()
    
    # Aufruf der steel_mixer-Funktion
    steel_mixer()
    
    # Endzeit der Stoppuhr
    end_time = time.time()
    
    # Berechne die verstrichene Zeit
    elapsed_time = end_time - start_time

    # Zeit in Stunden, Minuten und Sekunden umrechnen
    hours, rem = divmod(elapsed_time, 3600)
    minutes, seconds = divmod(rem, 60)

    # Ausgabe der verstrichenen Zeit
    print(f"Verstrichene Zeit: {int(hours)} Stunden, {int(minutes)} Minuten, {seconds:.2f} Sekunden")
