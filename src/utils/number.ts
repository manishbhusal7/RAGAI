/**
 * Number formatting and manipulation utilities
 * Provides functions for number operations and display
 */

/**
 * Format number with thousands separator
 * @param num - Number to format
 * @param locale - Locale for formatting (default: 'en-US')
 * @returns Formatted number string
 */
export const formatNumber = (num: number, locale: string = 'en-US'): string => {
  return new Intl.NumberFormat(locale).format(num);
};

/**
 * Format number as currency
 * @param num - Number to format
 * @param currency - Currency code (e.g., 'USD', 'EUR')
 * @param locale - Locale for formatting
 * @returns Formatted currency string
 */
export const formatCurrency = (
  num: number,
  currency: string = 'USD',
  locale: string = 'en-US'
): string => {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
  }).format(num);
};

/**
 * Format number as percentage
 * @param num - Number to format (0-1)
 * @param decimals - Number of decimal places
 * @returns Formatted percentage string
 */
export const formatPercentage = (num: number, decimals: number = 0): string => {
  return `${(num * 100).toFixed(decimals)}%`;
};

/**
 * Format bytes to human readable size
 * @param bytes - Number of bytes
 * @param decimals - Number of decimal places
 * @returns Formatted size string
 */
export const formatBytes = (bytes: number, decimals: number = 2): string => {
  if (bytes === 0) return '0 Bytes';

  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return (bytes / Math.pow(k, i)).toFixed(decimals) + ' ' + sizes[i];
};

/**
 * Round number to decimal places
 * @param num - Number to round
 * @param decimals - Number of decimal places
 * @returns Rounded number
 */
export const round = (num: number, decimals: number = 0): number => {
  return Math.round(num * Math.pow(10, decimals)) / Math.pow(10, decimals);
};

/**
 * Clamp number between min and max
 * @param num - Number to clamp
 * @param min - Minimum value
 * @param max - Maximum value
 * @returns Clamped number
 */
export const clamp = (num: number, min: number, max: number): number => {
  return Math.max(min, Math.min(max, num));
};

/**
 * Check if number is in range
 * @param num - Number to check
 * @param min - Minimum value (inclusive)
 * @param max - Maximum value (inclusive)
 * @returns true if number is in range
 */
export const inRange = (num: number, min: number, max: number): boolean => {
  return num >= min && num <= max;
};
