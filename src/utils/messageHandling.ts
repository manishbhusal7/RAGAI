/**
 * Message handling utilities
 * Provides helper functions for processing chat messages
 */

import { ChatMessage } from '../types';
import { isValidChatMessage } from './validation';

/**
 * Message filtering configuration
 */
export const MESSAGE_FILTER_CONFIG = {
  /** Maximum length of a message */
  MAX_MESSAGE_LENGTH: 10000,

  /** Minimum length that's considered valid */
  MIN_MESSAGE_LENGTH: 1,

  /** Whether to preserve whitespace */
  PRESERVE_WHITESPACE: false,
} as const;

/**
 * Generate a unique message ID
 * @returns Unique message ID
 */
export const generateMessageId = (): string => {
  return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
};

/**
 * Create a new chat message object
 * @param content - Message content
 * @param isUser - Whether message is from user
 * @returns New ChatMessage object
 */
export const createChatMessage = (
  content: string,
  isUser: boolean
): ChatMessage => {
  return {
    id: generateMessageId(),
    content: content.trim(),
    isUser,
    timestamp: new Date(),
  };
};

/**
 * Filter messages by length
 * @param messages - Messages to filter
 * @returns Filtered messages
 */
export const filterMessagesByLength = (messages: ChatMessage[]): ChatMessage[] => {
  return messages.filter(msg => {
    const length = msg.content.length;
    return length >= MESSAGE_FILTER_CONFIG.MIN_MESSAGE_LENGTH &&
           length <= MESSAGE_FILTER_CONFIG.MAX_MESSAGE_LENGTH;
  });
};

/**
 * Get messages since a certain time
 * @param messages - Messages to filter
 * @param minutes - Number of minutes to look back
 * @returns Messages from the last N minutes
 */
export const getRecentMessages = (
  messages: ChatMessage[],
  minutes: number = 60
): ChatMessage[] => {
  const cutoffTime = new Date(Date.now() - minutes * 60 * 1000);
  return messages.filter(msg => msg.timestamp > cutoffTime);
};

/**
 * Group messages by time window
 * @param messages - Messages to group
 * @param windowMinutes - Time window size in minutes
 * @returns Messages grouped by time window
 */
export const groupMessagesByTime = (
  messages: ChatMessage[],
  windowMinutes: number = 30
): Map<number, ChatMessage[]> => {
  const groups = new Map<number, ChatMessage[]>();
  const windowMs = windowMinutes * 60 * 1000;

  messages.forEach(msg => {
    const windowIndex = Math.floor(msg.timestamp.getTime() / windowMs);
    if (!groups.has(windowIndex)) {
      groups.set(windowIndex, []);
    }
    groups.get(windowIndex)!.push(msg);
  });

  return groups;
};

/**
 * Count messages from user vs AI
 * @param messages - Messages to analyze
 * @returns Object with user and ai message counts
 */
export const countMessageTypes = (
  messages: ChatMessage[]
): { user: number; ai: number } => {
  return {
    user: messages.filter(m => m.isUser).length,
    ai: messages.filter(m => !m.isUser).length,
  };
};

/**
 * Get conversation summary
 * @param messages - Messages to summarize
 * @returns Summary object
 */
export const getConversationSummary = (messages: ChatMessage[]) => {
  const counts = countMessageTypes(messages);
  const firstMessage = messages[0];
  const lastMessage = messages[messages.length - 1];

  return {
    totalMessages: messages.length,
    userMessages: counts.user,
    aiMessages: counts.ai,
    startTime: firstMessage?.timestamp,
    endTime: lastMessage?.timestamp,
    duration: firstMessage && lastMessage
      ? lastMessage.timestamp.getTime() - firstMessage.timestamp.getTime()
      : 0,
  };
};
