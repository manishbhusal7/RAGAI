/**
 * localStorage utilities for persisting application state
 * Provides type-safe operations for localStorage with error handling
 */

/**
 * Options for localStorage operations
 */
interface LocalStorageOptions {
  /** Whether to log errors to console */
  verbose?: boolean;
}

/**
 * Safely saves data to localStorage with error handling
 * @param key - The storage key
 * @param value - The value to store (will be JSON stringified)
 * @param options - Optional configuration
 * @returns true if successful, false otherwise
 */
export const saveToLocalStorage = <T>(
  key: string,
  value: T,
  options: LocalStorageOptions = {}
): boolean => {
  const { verbose = false } = options;

  try {
    const serialized = JSON.stringify(value);
    localStorage.setItem(key, serialized);
    return true;
  } catch (error) {
    if (verbose) {
      console.error(`Failed to save to localStorage (key: ${key}):`, error);
    }
    return false;
  }
};

/**
 * Safely retrieves data from localStorage with error handling
 * @param key - The storage key
 * @param fallback - Default value if key not found or parse fails
 * @param options - Optional configuration
 * @returns The parsed value or fallback
 */
export const getFromLocalStorage = <T>(
  key: string,
  fallback: T,
  options: LocalStorageOptions = {}
): T => {
  const { verbose = false } = options;

  try {
    const item = localStorage.getItem(key);
    if (item === null) {
      return fallback;
    }

    return JSON.parse(item) as T;
  } catch (error) {
    if (verbose) {
      console.error(`Failed to retrieve from localStorage (key: ${key}):`, error);
    }
    return fallback;
  }
};

/**
 * Removes an item from localStorage
 * @param key - The storage key to remove
 * @returns true if successful, false otherwise
 */
export const removeFromLocalStorage = (key: string): boolean => {
  try {
    localStorage.removeItem(key);
    return true;
  } catch {
    return false;
  }
};

/**
 * Clears all data from localStorage
 * @returns true if successful, false otherwise
 */
export const clearLocalStorage = (): boolean => {
  try {
    localStorage.clear();
    return true;
  } catch {
    return false;
  }
};

/**
 * Checks if localStorage is available
 * @returns true if localStorage is accessible
 */
export const isLocalStorageAvailable = (): boolean => {
  try {
    const testKey = '__localStorage_test__';
    localStorage.setItem(testKey, 'test');
    localStorage.removeItem(testKey);
    return true;
  } catch {
    return false;
  }
};
