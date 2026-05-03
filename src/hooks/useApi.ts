/**
 * Custom hook for API calls with loading and error states
 * Provides reusable logic for making API requests
 */

import { useState, useCallback } from 'react';
import { logger } from '../utils/logger';

interface UseApiOptions<T> {
  /** Callback when request succeeds */
  onSuccess?: (data: T) => void;
  /** Callback when request fails */
  onError?: (error: Error) => void;
  /** Whether to log API calls */
  verbose?: boolean;
}

interface UseApiReturn<T> {
  /** Current data from API */
  data: T | null;
  /** Loading state */
  loading: boolean;
  /** Error if request failed */
  error: Error | null;
  /** Trigger the API call */
  execute: (...args: any[]) => Promise<T | null>;
}

/**
 * Custom hook for API requests with state management
 * @param apiFunction - The API function to call
 * @param options - Configuration options
 * @returns Object with data, loading, error, and execute function
 */
export const useApi = <T>(
  apiFunction: (...args: any[]) => Promise<T>,
  options: UseApiOptions<T> = {}
): UseApiReturn<T> => {
  const { onSuccess, onError, verbose = false } = options;
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const execute = useCallback(
    async (...args: any[]): Promise<T | null> => {
      try {
        setLoading(true);
        setError(null);

        if (verbose) {
          logger.debug('API call started', { fn: apiFunction.name });
        }

        const result = await apiFunction(...args);
        setData(result);

        if (verbose) {
          logger.debug('API call succeeded', { fn: apiFunction.name });
        }

        onSuccess?.(result);
        return result;
      } catch (err) {
        const error = err instanceof Error ? err : new Error(String(err));
        setError(error);

        if (verbose) {
          logger.error('API call failed', error);
        }

        onError?.(error);
        return null;
      } finally {
        setLoading(false);
      }
    },
    [apiFunction, onSuccess, onError, verbose]
  );

  return { data, loading, error, execute };
};
