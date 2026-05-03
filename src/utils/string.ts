/**
 * String formatting and manipulation utilities
 * Provides functions for common string operations
 */

/**
 * Capitalize first letter of string
 * @param str - The string to capitalize
 * @returns Capitalized string
 */
export const capitalize = (str: string): string => {
  if (!str) return str;
  return str.charAt(0).toUpperCase() + str.slice(1);
};

/**
 * Convert string to title case
 * @param str - The string to convert
 * @returns Title cased string
 */
export const toTitleCase = (str: string): string => {
  return str
    .split(' ')
    .map(word => capitalize(word))
    .join(' ');
};

/**
 * Convert string to kebab case
 * @param str - The string to convert
 * @returns Kebab cased string
 */
export const toKebabCase = (str: string): string => {
  return str
    .replace(/([a-z])([A-Z])/g, '$1-$2')
    .replace(/\s+/g, '-')
    .toLowerCase();
};

/**
 * Convert string to snake case
 * @param str - The string to convert
 * @returns Snake cased string
 */
export const toSnakeCase = (str: string): string => {
  return str
    .replace(/([a-z])([A-Z])/g, '$1_$2')
    .replace(/\s+/g, '_')
    .toLowerCase();
};

/**
 * Truncate string to max length
 * @param str - The string to truncate
 * @param maxLength - Maximum length
 * @param suffix - Suffix to add if truncated (default: '...'
 * @returns Truncated string
 */
export const truncate = (
  str: string,
  maxLength: number,
  suffix: string = '...'
): string => {
  if (str.length <= maxLength) return str;
  return str.slice(0, maxLength - suffix.length) + suffix;
};

/**
 * Repeat string
 * @param str - The string to repeat
 * @param count - Number of times to repeat
 * @returns Repeated string
 */
export const repeat = (str: string, count: number): string => {
  return str.repeat(count);
};

/**
 * Pad string with character
 * @param str - The string to pad
 * @param length - Desired length
 * @param padChar - Character to pad with
 * @returns Padded string
 */
export const padStart = (str: string, length: number, padChar: string = ' '): string => {
  return str.padStart(length, padChar);
};

/**
 * Count occurrences of substring
 * @param str - String to search in
 * @param substring - Substring to count
 * @returns Number of occurrences
 */
export const countOccurrences = (str: string, substring: string): number => {
  if (!substring) return 0;
  const regex = new RegExp(substring.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g');
  const matches = str.match(regex);
  return matches ? matches.length : 0;
};

/**
 * Replace all occurrences
 * @param str - String to process
 * @param search - Substring to find
 * @param replace - Replacement string
 * @returns String with replacements
 */
export const replaceAll = (str: string, search: string, replace: string): string => {
  const regex = new RegExp(search.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g');
  return str.replace(regex, replace);
};
