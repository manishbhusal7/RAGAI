/**
 * API response type definitions
 * Ensures type safety for backend API responses
 */

/**
 * Base structure for all API responses from the backend
 */
export interface ApiResponse<T = unknown> {
  /** The response data */
  data?: T;
  /** Success indicator */
  success?: boolean;
  /** Error message if applicable */
  error?: string;
  /** Response status code */
  status?: number;
}

/**
 * Chat API response structure
 */
export interface ChatApiResponse {
  /** The AI's response text */
  answer?: string;
  /** Camel case variant (for compatibility) */
  Answer?: string;
  /** References to documents used in response */
  documentReferences?: Array<{
    documentId: string;
    documentName: string;
    pageNumber?: number;
    confidence: number;
  }>;
  /** Error details if response failed */
  error?: string;
}

/**
 * Document upload response
 */
export interface DocumentUploadResponse {
  /** Unique identifier for the uploaded document */
  documentId: string;
  /** Name of the uploaded file */
  fileName: string;
  /** Size of the file in bytes */
  fileSize: number;
  /** Processing status */
  status: 'pending' | 'processing' | 'completed' | 'failed';
  /** Error message if upload failed */
  error?: string;
}

/**
 * Conversation history item structure for API
 */
export interface ConversationHistoryItem {
  /** Message content */
  Content: string;
  /** Whether message is from user */
  IsUser: boolean;
  /** Timestamp of the message */
  Timestamp: string;
}

/**
 * Chat message request payload
 */
export interface ChatMessageRequest {
  /** User's message */
  Message: string;
  /** Previous conversation messages for context */
  ConversationHistory: ConversationHistoryItem[];
  /** Optional document IDs to search for relevant content */
  DocumentIds?: string[];
}

/**
 * Type guard to check if response contains answer
 */
export const hasChatAnswer = (response: unknown): response is ChatApiResponse => {
  if (!response || typeof response !== 'object') {
    return false;
  }

  const chat = response as Record<string, unknown>;
  return typeof chat.answer === 'string' || typeof chat.Answer === 'string';
};

/**
 * Extract answer from chat response (handles both cases)
 */
export const extractChatAnswer = (response: ChatApiResponse): string => {
  return response.answer ?? response.Answer ?? 'No response received from AI';
};
