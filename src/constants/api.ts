/**
 * Constants for API configuration and endpoints
 */

/**
 * Backend server base URL
 */
export const BACKEND_SERVER_URL = process.env.REACT_APP_API_BASE_URL || 'http://localhost:5169';

/**
 * API endpoints
 */
export const API_ENDPOINTS = {
  /** Chat API endpoint for sending messages */
  CHAT: '/api/Chat',
  
  /** File upload endpoint */
  UPLOAD: '/api/Upload',
  
  /** Document management endpoints */
  DOCUMENTS: '/api/Documents',
  
  /** Calendar integration endpoint */
  CALENDAR: '/api/Calendar',
  
  /** Confluence integration endpoint */
  CONFLUENCE: '/api/Confluence',
  
  /** Search endpoint */
  SEARCH: '/api/Search',
} as const;

/**
 * Type for API endpoint keys
 */
export type ApiEndpoint = keyof typeof API_ENDPOINTS;

/**
 * API request configuration
 */
export const API_CONFIG = {
  /** Default request timeout in milliseconds */
  TIMEOUT: 30000,
  
  /** Maximum number of retries for failed requests */
  MAX_RETRIES: 3,
  
  /** Delay between retries in milliseconds */
  RETRY_DELAY: 1000,
  
  /** Default headers for all requests */
  DEFAULT_HEADERS: {
    'Content-Type': 'application/json',
  },
} as const;

/**
 * Error messages for common API errors
 */
export const API_ERROR_MESSAGES = {
  CONNECTION_FAILED: 'Cannot connect to backend server. Please ensure your backend is running.',
  TIMEOUT: 'Request timeout. The server took too long to respond.',
  INVALID_REQUEST: 'Invalid request format.',
  UNAUTHORIZED: 'Unauthorized access.',
  FORBIDDEN: 'Access forbidden.',
  NOT_FOUND: 'Resource not found.',
  SERVER_ERROR: 'Server error. Please try again later.',
  UNKNOWN_ERROR: 'An unknown error occurred.',
} as const;

/**
 * Get full URL for an endpoint
 * @param endpoint - The endpoint key
 * @returns Full URL for the endpoint
 */
export const getEndpointUrl = (endpoint: ApiEndpoint): string => {
  return `${BACKEND_SERVER_URL}${API_ENDPOINTS[endpoint]}`;
};

/**
 * Get all endpoint URLs
 */
export const getAllEndpointUrls = (): Record<ApiEndpoint, string> => {
  return Object.keys(API_ENDPOINTS).reduce((acc, key) => {
    acc[key as ApiEndpoint] = getEndpointUrl(key as ApiEndpoint);
    return acc;
  }, {} as Record<ApiEndpoint, string>);
};
