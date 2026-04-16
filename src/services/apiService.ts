import { ChatMessage } from '../types';
import { addContextIndicator, formatForReadability } from '../utils/textFormatter';

// ===== CONFIGURATION SETTINGS =====

// The URL where our .NET backend server is running
const BACKEND_SERVER_URL = process.env.REACT_APP_API_BASE_URL || 'http://localhost:5169';

class ApiService {
  
  /**
   * Gets the base URL of the backend server
   * Useful for error messages and debugging
   */
  getBaseUrl(): string {
    return BACKEND_SERVER_URL;
  }

  /**
   * Sends a user message to the AI and gets a response back
   * This is the main method for chat functionality
   * 
   * @param userMessage - The question or message from the user
   * @param relevantDocumentIds - Array of document IDs that might help answer the question
   * @param conversationHistory - Previous messages in the conversation for context
   * @returns Promise with the AI's response text
   */
  async sendChatMessage(userMessage: string, relevantDocumentIds: string[], conversationHistory: ChatMessage[] = []): Promise<string> {
    const userHasUploadedDocuments = relevantDocumentIds && relevantDocumentIds.length > 0;

    try {
      // Validate message before sending
      const trimmedMessage = String(userMessage).trim();
      if (!trimmedMessage) {
        throw new Error('Message cannot be empty');
      }

      // Helper function to sanitize text content
      const sanitizeContent = (text: string) => {
        // Remove emojis and other special characters using a simpler approach
        return text.replace(/[\uD800-\uDBFF][\uDC00-\uDFFF]/g, '') // Remove emoji surrogate pairs
                  .replace(/[^\x20-\x7E]/g, '') // Keep only printable ASCII
                  .trim();
      };

      // Helper function to check if a message is an error message
      const isErrorMessage = (content: string) => {
        return content.startsWith('Error:') || content.includes('Backend error');
      };

      // Prepare conversation history for backend (last 4 messages for context to stay within token limits)
      const recentHistory = conversationHistory.slice(-4)
        .filter(msg => {
          // Filter out invalid messages and error messages
          return msg && 
                 typeof msg.content === 'string' && 
                 msg.content.trim().length > 0 && 
                 !isErrorMessage(msg.content);
        })
        .map(msg => ({
          Content: sanitizeContent(String(msg.content)),     // Sanitize and ensure content is always a string
          IsUser: Boolean(msg.isUser),             // Ensure boolean type
          Timestamp: new Date().toISOString()  // Use current time for consistency
        }));

      // Create the request body
      const requestBody = {
        Message: trimmedMessage,
        ConversationHistory: recentHistory || []
      };
      
      // Make the HTTP request to our backend
      const backendResponse = await fetch(`${BACKEND_SERVER_URL}/api/Chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
      });

      if (!backendResponse.ok) {
        const errorText = await backendResponse.text();
        throw new Error(`Backend error! Status: ${backendResponse.status}, Details: ${errorText}`);
      }

      // Parse the JSON response from the backend
      const responseData = await backendResponse.json();
      
      // Extract the AI's answer from the response (try both lowercase and uppercase property names)
      const aiAnswer = responseData.answer || responseData.Answer || 'No response received from AI';
      
      // Format the response nicely and add context information
      return addContextIndicator(aiAnswer, userHasUploadedDocuments);
      
    } catch (error) {
      // Provide helpful error messages based on the type of error
      if (error instanceof TypeError && error.message.includes('fetch')) {
        throw new Error(
          `Cannot connect to backend server at ${BACKEND_SERVER_URL}. ` +
          `Please ensure your .NET backend is running and accessible.`
        );
      }
      
      // Re-throw other errors as-is
      throw error;
    }
  }

  /**
   * Uploads a file to the backend for processing
   * The backend will extract text and make it searchable
   * 
   * @param fileToUpload - The file selected by the user
   * @returns Promise with file information (ID and name)
   */
  async uploadFile(fileToUpload: File): Promise<{ fileId: string; fileName: string }> {
    try {
      // Create form data to send the file
      const fileFormData = new FormData();
      fileFormData.append('file', fileToUpload);
      
      const uploadResponse = await fetch(`${BACKEND_SERVER_URL}/api/FileUpload`, {
        method: 'POST',
        body: fileFormData,
      });

      if (!uploadResponse.ok) {
        const uploadErrorDetails = await uploadResponse.text();
        throw new Error(`Upload failed! Status: ${uploadResponse.status}`);
      }

      const uploadResponseData = await uploadResponse.json();
      
      // Backend returns { fileName, message }, we need to map it to our expected format
      return {
        fileId: uploadResponseData.fileName,    // Use fileName as the unique ID
        fileName: uploadResponseData.fileName   // The actual file name
      };
      
    } catch (error) {
      throw error;
    }
  }

  /**
   * Deletes a previously uploaded file from the backend
   * This removes the file from storage and search index
   * 
   * @param fileNameToDelete - Name of the file to delete
   */
  async deleteFile(fileNameToDelete: string): Promise<void> {
    try {
      // Encode the filename to handle special characters in URLs
      const encodedFileName = encodeURIComponent(fileNameToDelete);
      
      const deleteResponse = await fetch(`${BACKEND_SERVER_URL}/api/FileUpload/${encodedFileName}`, {
        method: 'DELETE',
      });

      if (!deleteResponse.ok) {
        const deleteErrorDetails = await deleteResponse.text();
        throw new Error(`Delete failed! Status: ${deleteResponse.status}`);
      }
      
    } catch (error) {
      throw error;
    }
  }

  /**
   * Gets a list of all files that have been uploaded to the backend
   * This helps sync the frontend with what's actually stored on the server
   * 
   * @returns Promise with array of uploaded file information
   */
  async getUploadedFiles(): Promise<any[]> {
    try {
      const filesResponse = await fetch(`${BACKEND_SERVER_URL}/api/FileUpload`);

      if (!filesResponse.ok) {
        const filesErrorDetails = await filesResponse.text();
        throw new Error(`Failed to get files! Status: ${filesResponse.status}`);
      }

      const filesData = await filesResponse.json();
      return filesData;
      
    } catch (error) {
      throw error;
    }
  }
}

// Export a single instance that the entire app can use
// This ensures we only have one ApiService instance across the whole application
export const apiService = new ApiService(); 