/**
 * Performance optimization utilities
 * Provides helpers for improving React component performance
 */

/**
 * Debounce function execution
 * Delays function execution until specified time has passed
 * @param fn - Function to debounce
 * @param delay - Delay in milliseconds
 * @returns Debounced function
 */
export const debounce = <T extends (...args: any[]) => any>(
  fn: T,
  delay: number = 300
): (...args: Parameters<T>) => void => {
  let timeoutId: NodeJS.Timeout | null = null;

  return (...args: Parameters<T>) => {
    if (timeoutId) {
      clearTimeout(timeoutId);
    }

    timeoutId = setTimeout(() => {
      fn(...args);
      timeoutId = null;
    }, delay);
  };
};

/**
 * Throttle function execution
 * Limits function execution to once per specified interval
 * @param fn - Function to throttle
 * @param limit - Time limit in milliseconds
 * @returns Throttled function
 */
export const throttle = <T extends (...args: any[]) => any>(
  fn: T,
  limit: number = 300
): (...args: Parameters<T>) => void => {
  let inThrottle = false;

  return (...args: Parameters<T>) => {
    if (!inThrottle) {
      fn(...args);
      inThrottle = true;

      setTimeout(() => {
        inThrottle = false;
      }, limit);
    }
  };
};

/**
 * Memoize function results
 * Caches function results based on arguments
 * @param fn - Function to memoize
 * @returns Memoized function
 */
export const memoize = <T extends (...args: any[]) => any>(fn: T): T => {
  const cache = new Map();

  return ((...args: Parameters<T>) => {
    const key = JSON.stringify(args);

    if (cache.has(key)) {
      return cache.get(key);
    }

    const result = fn(...args);
    cache.set(key, result);
    return result;
  }) as T;
};

/**
 * Request idle callback polyfill
 * Schedules work to be done when browser is idle
 * @param fn - Function to call
 * @returns Callback ID
 */
export const idle = (fn: () => void): number => {
  if (typeof requestIdleCallback !== 'undefined') {
    return requestIdleCallback(fn);
  }

  // Fallback for browsers without requestIdleCallback
  return setTimeout(fn, 1) as unknown as number;
};

/**
 * Cancel idle callback
 * @param id - Callback ID to cancel
 */
export const cancelIdle = (id: number): void => {
  if (typeof cancelIdleCallback !== 'undefined') {
    cancelIdleCallback(id);
  } else {
    clearTimeout(id as any);
  }
};

/**
 * Sleep/delay helper
 * @param ms - Milliseconds to sleep
 * @returns Promise that resolves after delay
 */
export const sleep = (ms: number): Promise<void> => {
  return new Promise(resolve => setTimeout(resolve, ms));
};
