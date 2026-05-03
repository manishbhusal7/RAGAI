/**
 * Validation utilities for common data validation tasks
 * Ensures consistent validation patterns across the application
 */

/**
 * Sanitizes text content by removing special characters and emojis
 * @param text - The text to sanitize
 * @returns Sanitized text with only safe characters
 */
export const sanitizeText = (text: string): string => {
  if (!text || typeof text !== 'string') {
    return '';
  }

  return text
    .replace(/[\uD800-\uDBFF][\uDC00-\uDFFF]/g, '') // Remove emoji surrogate pairs
    .replace(/[^\x20-\x7E\n]/g, '') // Keep only printable ASCII and newlines
    .trim();
};

/**
 * Validates if a message is empty or contains only whitespace
 * @param message - The message to validate
 * @returns true if message is valid (non-empty)
 */
export const isValidMessage = (message: string): message is string => {
  return typeof message === 'string' && message.trim().length > 0;
};

/**
 * Checks if content appears to be an error message
 * @param content - The content to check
 * @returns true if content looks like an error message
 */
export const isErrorMessage = (content: string): boolean => {
  if (!content || typeof content !== 'string') {
    return false;
  }

  const errorPatterns = ['Error:', 'Backend error', 'Failed', 'Exception'];
  return errorPatterns.some(pattern => content.startsWith(pattern));
};

/**
 * Validates a message object structure
 * @param msg - The object to validate
 * @returns true if msg is a valid message structure
 */
export const isValidChatMessage = (msg: unknown): boolean => {
  if (!msg || typeof msg !== 'object') {
    return false;
  }

  const message = msg as Record<string, unknown>;
  return (
    typeof message.content === 'string' &&
    message.content.trim().length > 0 &&
    typeof message.isUser === 'boolean'
  );
};

/**
 * Validates an array of message objects
 * @param messages - Array of messages to validate
 * @returns Array of valid messages, filtering out invalid ones
 */
export const filterValidMessages = (messages: unknown[]): unknown[] => {
  if (!Array.isArray(messages)) {
    return [];
  }

  return messages.filter(msg => {
    try {
      return isValidChatMessage(msg);
    } catch {
      return false;
    }
  });
};

/**
 * Ensures value is of correct type with fallback
 * @param value - The value to type-assert
 * @param expectedType - The expected type name
 * @param fallback - The fallback value if type doesn't match
 * @returns The value if it matches type, otherwise fallback
 */
export const ensureType = <T>(value: unknown, expectedType: string, fallback: T): T => {
  if (typeof value === expectedType) {
    return value as T;
  }
  return fallback;
};
