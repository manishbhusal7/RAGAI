/**
 * UI String Constants
 * Centralized strings used throughout the application UI
 */

export const UI_STRINGS = {
  // Chat messages
  CHAT: {
    PLACEHOLDER: 'Ask me anything...',
    EMPTY_STATE_TITLE: 'Start a Conversation',
    EMPTY_STATE_DESC: 'Send a message to begin chatting with the AI assistant',
    SEND_BUTTON: 'Send',
    LOADING: 'Sending...',
    ERROR: 'Failed to send message',
    NEW_CHAT: 'New Chat',
    RENAME_PLACEHOLDER: 'Conversation title...',
  },

  // File upload
  UPLOAD: {
    TITLE: 'Upload Documents',
    DESC: 'Upload files for the AI to analyze',
    BUTTON: 'Choose File',
    UPLOADING: 'Uploading...',
    SUCCESS: 'File uploaded successfully',
    ERROR: 'Failed to upload file',
    DELETE_CONFIRM: 'Delete this file?',
  },

  // Sidebar
  SIDEBAR: {
    CONVERSATIONS: 'Conversations',
    DOCUMENTS: 'Documents',
    NEW_CHAT: 'New Chat',
    COLLAPSE: 'Collapse Sidebar',
    EXPAND: 'Expand Sidebar',
  },

  // General
  GENERAL: {
    LOADING: 'Loading...',
    ERROR: 'Error',
    SUCCESS: 'Success',
    DELETE: 'Delete',
    CANCEL: 'Cancel',
    CONFIRM: 'Confirm',
    CLOSE: 'Close',
  },
} as const;

/**
 * Type for accessing UI strings with type safety
 */
export type UIStringPath = typeof UI_STRINGS;

/**
 * Get a UI string by path
 * @param path - The path to the string (e.g., 'CHAT.PLACEHOLDER')
 * @returns The string value or the path if not found
 */
export const getUIString = (path: string): string => {
  const keys = path.split('.');
  let value: any = UI_STRINGS;

  for (const key of keys) {
    if (value && typeof value === 'object' && key in value) {
      value = value[key];
    } else {
      return path; // Return path if not found
    }
  }

  return typeof value === 'string' ? value : path;
};
