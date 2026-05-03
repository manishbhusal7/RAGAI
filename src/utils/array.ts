/**
 * Array utilities for common array operations
 * Provides type-safe array manipulation functions
 */

/**
 * Remove duplicates from array
 * @param array - The array to deduplicate
 * @param key - Optional key function for complex objects
 * @returns Array with duplicates removed
 */
export const removeDuplicates = <T>(
  array: T[],
  key?: (item: T) => unknown
): T[] => {
  if (key) {
    const seen = new Set();
    return array.filter(item => {
      const keyValue = key(item);
      if (seen.has(keyValue)) {
        return false;
      }
      seen.add(keyValue);
      return true;
    });
  }

  return Array.from(new Set(array));
};

/**
 * Chunk array into smaller arrays
 * @param array - The array to chunk
 * @param size - Size of each chunk
 * @returns Array of chunks
 */
export const chunk = <T>(array: T[], size: number): T[][] => {
  const chunks: T[][] = [];
  for (let i = 0; i < array.length; i += size) {
    chunks.push(array.slice(i, i + size));
  }
  return chunks;
};

/**
 * Flatten nested array
 * @param array - The array to flatten
 * @param depth - Depth to flatten (default 1)
 * @returns Flattened array
 */
export const flatMap = <T>(array: T[], depth: number = 1): T[] => {
  return array.flat(depth) as T[];
};

/**
 * Find index of item in array
 * @param array - The array to search
 * @param predicate - Function to check items
 * @returns Index of found item or -1
 */
export const findIndex = <T>(
  array: T[],
  predicate: (item: T) => boolean
): number => {
  return array.findIndex(predicate);
};

/**
 * Get last item in array
 * @param array - The array
 * @returns Last item or undefined
 */
export const last = <T>(array: T[]): T | undefined => {
  return array[array.length - 1];
};

/**
 * Get first item in array
 * @param array - The array
 * @returns First item or undefined
 */
export const first = <T>(array: T[]): T | undefined => {
  return array[0];
};

/**
 * Check if array contains item
 * @param array - The array to search
 * @param item - Item to find
 * @returns true if array contains item
 */
export const contains = <T>(array: T[], item: T): boolean => {
  return array.includes(item);
};

/**
 * Count items matching predicate
 * @param array - The array to count
 * @param predicate - Condition to count
 * @returns Count of matching items
 */
export const count = <T>(
  array: T[],
  predicate: (item: T) => boolean
): number => {
  return array.filter(predicate).length;
};
