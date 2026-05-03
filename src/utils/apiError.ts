/**
 * API Error Handling Utilities
 * Provides consistent error handling and messaging for API operations
 */

import { API_ERROR_MESSAGES } from '../constants/api';

/**
 * Custom API error class
 */
export class ApiError extends Error {
  constructor(
    message: string,
    public statusCode?: number,
    public originalError?: unknown
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * Parse HTTP error status to human-readable message
 * @param status - HTTP status code
 * @returns Error message
 */
export const getErrorMessageForStatus = (status: number): string => {
  switch (status) {
    case 400:
      return API_ERROR_MESSAGES.INVALID_REQUEST;
    case 401:
      return API_ERROR_MESSAGES.UNAUTHORIZED;
    case 403:
      return API_ERROR_MESSAGES.FORBIDDEN;
    case 404:
      return API_ERROR_MESSAGES.NOT_FOUND;
    case 500:
    case 502:
    case 503:
    case 504:
      return API_ERROR_MESSAGES.SERVER_ERROR;
    default:
      return API_ERROR_MESSAGES.UNKNOWN_ERROR;
  }
};

/**
 * Check if error is a network error
 * @param error - The error to check
 * @returns true if error is a network error
 */
export const isNetworkError = (error: unknown): error is TypeError => {
  return (
    error instanceof TypeError &&
    error.message.includes('fetch')
  );
};

/**
 * Check if error is a timeout error
 * @param error - The error to check
 * @returns true if error is a timeout error
 */
export const isTimeoutError = (error: unknown): boolean => {
  return (
    error instanceof Error &&
    error.name === 'AbortError'
  );
};

/**
 * Format error for user display
 * @param error - The error to format
 * @param defaultMessage - Default message if error can't be formatted
 * @returns User-friendly error message
 */
export const formatErrorForDisplay = (
  error: unknown,
  defaultMessage: string = 'An error occurred'
): string => {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (isNetworkError(error)) {
    return API_ERROR_MESSAGES.CONNECTION_FAILED;
  }

  if (isTimeoutError(error)) {
    return API_ERROR_MESSAGES.TIMEOUT;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return defaultMessage;
};

/**
 * Retry function with exponential backoff
 * @param fn - Function to retry
 * @param maxRetries - Maximum number of retries
 * @param delay - Initial delay in milliseconds
 * @returns Result of function call
 * @throws ApiError if all retries fail
 */
export const retryWithBackoff = async <T>(
  fn: () => Promise<T>,
  maxRetries: number = 3,
  delay: number = 1000
): Promise<T> => {
  let lastError: unknown;

  for (let i = 0; i <= maxRetries; i++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;

      // Don't retry on last attempt
      if (i < maxRetries) {
        // Exponential backoff
        const backoffDelay = delay * Math.pow(2, i);
        await new Promise(resolve => setTimeout(resolve, backoffDelay));
      }
    }
  }

  throw new ApiError(
    `Failed after ${maxRetries} retries`,
    undefined,
    lastError
  );
};
