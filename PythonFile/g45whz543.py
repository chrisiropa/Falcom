import numpy as np
from scipy.optimize import minimize
import matplotlib.pyplot as plt
from scipy.misc import derivative

def find_minima(func, x0, dx=1e-6):
    """
    Finds the local minima of a function using the derivative.

    Args:
        func: The function to find the minima of.
        x0: The initial guess for the x-coordinate of a minimum.
        dx: The step size for numerical differentiation.

    Returns:
        A list of x-coordinates of the found minima.
    """

    # Find the derivative of the function
    derivative_func = lambda x: derivative(func, x, dx=dx)

    # Use minimize to find the roots of the derivative (where the derivative is zero)
    result = minimize(derivative_func, x0)

    return result.x

# Example usage
my_function = lambda x: 0.5*x**4 - 3*x**3 + 5*x**2 - 2*x + 2

# Find minima starting from different initial guesses
minima = find_minima(my_function, -1)
minima = np.append(minima, find_minima(my_function, 1))

print(minima)