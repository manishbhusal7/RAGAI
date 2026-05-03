/**
 * API Service for communicating with the backend server
 * Handles all HTTP requests and responses with proper error handling
 */

import { ChatMessage } from '../types';
import { addContextIndicator, formatForReadability } from '../utils/textFormatter';
import { sanitizeText, isValidMessage, isErrorMessage } from '../utils/validation';
import { logger } from '../utils/logger';
import { BACKEND_SERVER_URL, API_ENDPOINTS, getEndpointUrl, API_ERROR_MESSAGES } from '../constants/api';
import { ChatApiResponse, ChatMessageRequest, ConversationHistoryItem, extractChatAnswer } from '../types/api';

const log = logger;

/**
 * Service class for API communications with backend
 * Encapsulates all HTTP requests and response handling
 */
class ApiService {
  /**
   * Gets the base URL of the backend server
   */
  getBaseUrl(): string {
    return BACKEND_SERVER_URL;
  }

  /**
   * Sends a user message to the AI and gets a response back
   * Main method for chat functionality
   *
   * @param userMessage - The question or message from the user
   * @param relevantDocumentIds - Array of document IDs that might help answer the question
   * @param conversationHistory - Previous messages in the conversation for context
   * @returns Promise with the AI's response text
   * @throws Error if message is invalid or API request fails
   */
  async sendChatMessage(
    userMessage: string,
    relevantDocumentIds: string[] = [],
    conversationHistory: ChatMessage[] = []
  ): Promise<string> {
    const userHasUploadedDocuments = relevantDocumentIds?.length > 0;

    try {
      // Validate message before sending
      if (!isValidMessage(userMessage)) {
        throw new Error('Message cannot be empty');
      }

      log.debug('Sending chat message', { messageLength: userMessage.length });

      // Prepare conversation history for backend (last 4 messages for context)
      const recentHistory = this.prepareConversationHistory(conversationHistory);

      // Create the request body with proper typing
      const requestBody: ChatMessageRequest = {
        Message: userMessage.trim(),
        ConversationHistory: recentHistory,
        DocumentIds: relevantDocumentIds,
      };

      // Make the HTTP request to backend
      const backendResponse = await fetch(getEndpointUrl('CHAT'), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
      });

      if (!backendResponse.ok) {
        const errorText = await backendResponse.text();
        log.error(`Chat API error: ${backendResponse.status}`, { errorText });
        throw new Error(
          `Backend error! Status: ${backendResponse.status}, Details: ${errorText}`
        );
      }

      // Parse the JSON response from backend
      const responseData: ChatApiResponse = await backendResponse.json();

      // Extract the AI's answer using helper
      const aiAnswer = extractChatAnswer(responseData);

      // Format response and add context information
      return addContextIndicator(aiAnswer, userHasUploadedDocuments);
    } catch (error) {
      return this.handleApiError(error, 'Failed to send chat message');
    }
  }

  /**
   * Prepares conversation history for API request
   * @param conversationHistory - Raw conversation history
   * @returns Formatted conversation history ready for API
   */
  private prepareConversationHistory(conversationHistory: ChatMessage[]): ConversationHistoryItem[] {
    return conversationHistory
      .slice(-4) // Keep last 4 messages for context
      .filter(msg => {
        // Filter out invalid or error messages
        return (
          msg &&
          typeof msg.content === 'string' &&
          msg.content.trim().length > 0 &&
          !isErrorMessage(msg.content)
        );
      })
      .map(msg => ({
        Content: sanitizeText(msg.content),
        IsUser: Boolean(msg.isUser),
        Timestamp: new Date().toISOString(),
      }));
  }

  /**
   * Uploads a file to the backend for processing
   *
   * @param fileToUpload - The file selected by the user
   * @returns Promise with file information (ID and name)
   * @throws Error if upload fails
   */
  async uploadFile(fileToUpload: File): Promise<{ fileId: string; fileName: string }> {
    try {
      log.info('Uploading file', { fileName: fileToUpload.name, size: fileToUpload.size });

      // Create form data to send the file
      const fileFormData = new FormData();
      fileFormData.append('file', fileToUpload);

      const uploadResponse = await fetch(getEndpointUrl('UPLOAD'), {
        method: 'POST',
        body: fileFormData,
      });

      if (!uploadResponse.ok) {
        const uploadErrorDetails = await uploadResponse.text();
        log.error(`File upload error: ${uploadResponse.status}`, { errorDetails: uploadErrorDetails });
        throw new Error(`Upload failed! Status: ${uploadResponse.status}`);
      }

      const uploadResponseData = await uploadResponse.json();

      log.info('File uploaded successfully', { fileName: uploadResponseData.fileName });

      // Backend returns { fileName, message }, map to expected format
      return {
        fileId: uploadResponseData.fileName, // Use fileName as unique ID
        fileName: uploadResponseData.fileName, // The actual file name
      };
    } catch (error) {
      return Promise.reject(
        this.handleApiError(error, 'Failed to upload file')
      );
    }
  }

  /**
   * Deletes a previously uploaded file from the backend
   *
   * @param fileNameToDelete - Name of the file to delete
   * @throws Error if deletion fails
   */
  async deleteFile(fileNameToDelete: string): Promise<void> {
    try {
      log.info('Deleting file', { fileName: fileNameToDelete });

      // Encode filename to handle special characters in URLs
      const encodedFileName = encodeURIComponent(fileNameToDelete);

      const deleteResponse = await fetch(
        `${getEndpointUrl('UPLOAD')}/${encodedFileName}`,
        { method: 'DELETE' }
      );

      if (!deleteResponse.ok) {
        const deleteErrorDetails = await deleteResponse.text();
        log.error(`File delete error: ${deleteResponse.status}`, { errorDetails: deleteErrorDetails });
        throw new Error(`Delete failed! Status: ${deleteResponse.status}`);
      }

      log.info('File deleted successfully', { fileName: fileNameToDelete });
    } catch (error) {
      throw this.handleApiError(error, 'Failed to delete file');
    }
  }

  /**
   * Gets a list of all files that have been uploaded to the backend
   *
   * @returns Promise with array of uploaded file information
   * @throws Error if request fails
   */
  async getUploadedFiles(): Promise<any[]> {
    try {
      log.debug('Fetching uploaded files');

      const filesResponse = await fetch(getEndpointUrl('UPLOAD'));

      if (!filesResponse.ok) {
        const filesErrorDetails = await filesResponse.text();
        log.error(`Get files error: ${filesResponse.status}`, { errorDetails: filesErrorDetails });
        throw new Error(`Failed to get files! Status: ${filesResponse.status}`);
      }

      const filesData = await filesResponse.json();
      log.debug('Uploaded files retrieved', { count: filesData.length });

      return filesData;
    } catch (error) {
      throw this.handleApiError(error, 'Failed to fetch uploaded files');
    }
  }

  /**
   * Centralized error handling for API errors
   * @param error - The error that occurred
   * @param defaultMessage - Default message if error can't be determined
   * @returns Formatted error message
   */
  private handleApiError(error: unknown, defaultMessage: string): string {
    if (error instanceof TypeError && error.message.includes('fetch')) {
      const message = `Cannot connect to backend at ${BACKEND_SERVER_URL}. ${API_ERROR_MESSAGES.CONNECTION_FAILED}`;
      log.error('Connection error', { message });
      return message;
    }

    if (error instanceof Error) {
      log.error(defaultMessage, { message: error.message });
      return error.message;
    }

    log.error(defaultMessage, { error: String(error) });
    return defaultMessage;
  }
}

/**
 * Single API service instance for the entire application
 * Ensures only one ApiService instance across the app
 */
export const apiService = new ApiService();
