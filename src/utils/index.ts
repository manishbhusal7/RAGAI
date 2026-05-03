/**
 * Centralized exports for all utility functions
 * Simplifies imports throughout the application
 *
 * Usage: import { sanitizeText, debounce } from '@/utils'
 */

// Validation utilities
export {
  sanitizeText,
  isValidMessage,
  isErrorMessage,
  isValidChatMessage,
  filterValidMessages,
  ensureType,
} from './validation';

// localStorage utilities
export {
  saveToLocalStorage,
  getFromLocalStorage,
  removeFromLocalStorage,
  clearLocalStorage,
  isLocalStorageAvailable,
  type LocalStorageOptions,
} from './localStorage';

// Logger utility
export { Logger, LogLevel, logger, createLogger } from './logger';

// Markdown utilities
export {
  extractCodeBlocks,
  extractInlineCode,
  hasCodeBlocks,
  hasInlineCode,
  removeCodeBlocks,
  stripMarkdown,
  MARKDOWN_PATTERNS,
  CODE_BLOCK_EXTRACTION_CONFIG,
} from './markdown';

// Message handling
export {
  generateMessageId,
  createChatMessage,
  filterMessagesByLength,
  getRecentMessages,
  groupMessagesByTime,
  countMessageTypes,
  getConversationSummary,
  MESSAGE_FILTER_CONFIG,
} from './messageHandling';

// API error handling
export {
  ApiError,
  getErrorMessageForStatus,
  isNetworkError,
  isTimeoutError,
  formatErrorForDisplay,
  retryWithBackoff,
} from './apiError';

// Date/time utilities
export {
  formatDate,
  formatTime,
  formatDateTime,
  formatRelativeTime,
  isToday,
  isYesterday,
  startOfToday,
  endOfToday,
} from './dateTime';

// Array utilities
export {
  removeDuplicates,
  chunk,
  flatMap,
  findIndex,
  last,
  first,
  contains,
  count,
} from './array';

// Object utilities
export {
  deepClone,
  merge,
  deepMerge,
  getValue,
  setValue,
  pick,
  omit,
} from './object';

// String utilities
export {
  capitalize,
  toTitleCase,
  toKebabCase,
  toSnakeCase,
  truncate,
  repeat,
  padStart,
  countOccurrences,
  replaceAll,
} from './string';

// Number utilities
export {
  formatNumber,
  formatCurrency,
  formatPercentage,
  formatBytes,
  round,
  clamp,
  inRange,
} from './number';

// Performance utilities
export {
  debounce,
  throttle,
  memoize,
  idle,
  cancelIdle,
  sleep,
} from './performance';

// Text formatter (existing utility kept for backward compatibility)
export { formatAIResponse, formatForReadability, addContextIndicator } from './textFormatter';
