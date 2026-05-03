/**
 * Hook for clipboard operations
 * Manages copying content with feedback
 */

import { useState, useCallback } from 'react';

interface UseClipboardOptions {
  /** Duration to show "copied" feedback in milliseconds */
  feedbackDuration?: number;
  /** Callback when copy succeeds */
  onSuccess?: () => void;
  /** Callback when copy fails */
  onError?: (error: Error) => void;
}

interface UseClipboardReturn {
  /** Current copy state */
  copied: boolean;
  /** Copy text to clipboard */
  copy: (text: string) => Promise<boolean>;
  /** Reset copied state */
  reset: () => void;
}

/**
 * Custom hook for clipboard text operations
 * @param options - Configuration options
 * @returns Object with copied state and copy function
 */
export const useClipboard = (options: UseClipboardOptions = {}): UseClipboardReturn => {
  const {
    feedbackDuration = 2000,
    onSuccess,
    onError,
  } = options;

  const [copied, setCopied] = useState(false);

  const copy = useCallback(async (text: string): Promise<boolean> => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      onSuccess?.();

      // Reset after feedback duration
      const timeoutId = setTimeout(() => {
        setCopied(false);
      }, feedbackDuration);

      return true;
    } catch (error) {
      const err = error instanceof Error ? error : new Error('Failed to copy to clipboard');
      console.error('Clipboard error:', err);
      onError?.(err);
      return false;
    }
  }, [feedbackDuration, onSuccess, onError]);

  const reset = useCallback(() => {
    setCopied(false);
  }, []);

  return { copied, copy, reset };
};
