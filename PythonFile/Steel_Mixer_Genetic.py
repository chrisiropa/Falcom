import random
import numpy as np

# Zielfunktion: Summe der quadrierten Abweichungen von den Zielwerten
def objective_function(x, data):
    errechnet_cu = np.sum(x * data['cu'])  # Berechneter Cu-Wert
    errechnet_mn = np.sum(x * data['mn'])  # Berechneter Mn-Wert

    # Zielfunktion: Summe der quadrierten Differenzen zu den Zielwerten
    return (data['ziel_cu'] - errechnet_cu)**2 + (data['ziel_mn'] - errechnet_mn)**2

# Funktion für die Initialisierung der Population
def initialize_population(pop_size):
    population = []
    for _ in range(pop_size):
        individual = np.random.rand(16)
        individual /= np.sum(individual)  # Normalisierung
        population.append(individual)
    return population

# Funktion für die Roulette-Wheel-Selektion
def selection(population, fitnesses):
    total_fitness = sum(fitnesses)
    probabilities = [f / total_fitness for f in fitnesses]
    return random.choices(population, weights=probabilities, k=2)

# Funktion für den Ein-Punkt-Crossover
def crossover(parent1, parent2):
    crossover_point = random.randint(1, 15)
    child1 = np.concatenate((parent1[:crossover_point], parent2[crossover_point:]))
    child2 = np.concatenate((parent2[:crossover_point], parent1[crossover_point:]))   

    return child1, child2   


# Funktion für die Mutation
def mutation(individual, mutation_rate):
    for i in range(len(individual)):
        if random.random() < mutation_rate:
            individual[i] += np.random.normal(0,   
 0.1)
    individual /= np.sum(individual)  # Normalisierung
    return individual

# Hauptfunktion: Genetischer Algorithmus
def steel_mixer_genetic():
    # ... (Dein ursprünglicher Code für Datenvorbereitung etc.)

    # Initialisierung der Population
    population_size = 100
    population = initialize_population(population_size)

    # Evolutionszyklus
    generations = 100
    mutation_rate = 0.1
    for generation in range(generations):
        fitnesses = [-objective_function(individual, data) for individual in population]  # Maximierung der Fitness
        new_population = []
        for _ in range(population_size // 2):
            parent1, parent2 = selection(population, fitnesses)
            child1, child2 = crossover(parent1, parent2)
            child1 = mutation(child1, mutation_rate)
            child2 = mutation(child2, mutation_rate)
            new_population.extend([child1, child2])
        population = new_population   


    # Auswahl des besten Individuums
    best_index = np.argmax(fitnesses)
    best_solution = population[best_index]
    return best_solution

if __name__ == "__main__":
    # Beispielaufruf über Kommandozeile, dann kann man es auch hier testen
    # 16 Kupferwerte, 16 Manganwerte Am Ende die Zielwert-Vorgabe für Kupfer und Mangan
    # %Run Steel_Mixer.py 0.084 0.026 0.014 0.69 0.037 0.015 0.016 0.017 0.5 0.081 0.017 0.014 0.015 0.054 0.5 0.077 1.05 0.432 0.623 1.633 0.394 0.18 0.282 0.5 1.659 0.157 0.147 0.217 0.14 0.407 1.649 0.22 0.77 0.77
    
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
    x_opt = steel_mixer_genetic()
    
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
    

