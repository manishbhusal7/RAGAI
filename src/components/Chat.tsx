import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import { 
  IconButton, 
  Button, 
  Chip
} from '@mui/material';
import { 
  Add as AddIcon, 
  Chat as ChatIcon,
  Delete as DeleteIcon,
  Edit as EditIcon,
  Upload as UploadIcon,
  Close as CloseIcon,
  Menu as MenuIcon
} from '@mui/icons-material';
import './Chat.css';
import { useConversations } from '../hooks/useConversations';
import { useDocuments } from '../hooks/useDocuments';
import { ChatMessage, FileState } from '../types';
import { apiService } from '../services/apiService';
import MarkdownRenderer from './MarkdownRenderer';

interface ChatProps {
  isSidebarCollapsed?: boolean;
  onToggleSidebar?: () => void;
}

const getConversationListHeading = (searchText: string, matchingCount: number) => {
  if (!searchText) {
    return 'Recent Conversations';
  }

  return `Found ${matchingCount} chat${matchingCount !== 1 ? 's' : ''}`;
};

const Chat: React.FC<ChatProps> = ({ isSidebarCollapsed: propSidebarCollapsed, onToggleSidebar }) => {
  // State variables
  const [userTypingMessage, setUserTypingMessage] = useState('');
  const [isAiResponding, setIsAiResponding] = useState(false);
  const [localSidebarCollapsed, setLocalSidebarCollapsed] = useState(false);
  const [conversationSearchText, setConversationSearchText] = useState('');
  const [conversationBeingEdited, setConversationBeingEdited] = useState<string | null>(null);
  const [newConversationTitle, setNewConversationTitle] = useState('');
  
  // Refs
  const chatMessagesContainerRef = useRef<HTMLDivElement>(null);
  const fileUploadInputRef = useRef<HTMLInputElement>(null);

  // Custom hooks
  const {
    conversations,
    createNewConversation,
    switchConversation,
    getActiveConversation,
    addMessageToActiveConversation,
    deleteConversation,
    renameConversation
  } = useConversations();

  const {
    uploadedFiles,
    activeFileIds,
    uploadFiles,
    removeFile
  } = useDocuments();

  const currentActiveConversation = getActiveConversation();
  const isSidebarCollapsed = propSidebarCollapsed !== undefined ? propSidebarCollapsed : localSidebarCollapsed;

  // Auto-scroll effect
  useEffect(() => {
    if (chatMessagesContainerRef.current) {
      chatMessagesContainerRef.current.scrollTop = chatMessagesContainerRef.current.scrollHeight;
    }
  }, [currentActiveConversation?.messages, isAiResponding]);

  const handleSendMessage = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    
    const trimmedMessage = userTypingMessage.trim();
    if (!trimmedMessage || isAiResponding) {
      return;
    }

    let conversationToUse = currentActiveConversation;
    if (!conversationToUse) {
      createNewConversation();
      conversationToUse = getActiveConversation();
    }

    const messageFromUser: ChatMessage = {
      id: Date.now().toString(),
      content: trimmedMessage,
      isUser: true,
      timestamp: new Date()
    };

    addMessageToActiveConversation(messageFromUser);
    setUserTypingMessage('');
    setIsAiResponding(true);

    try {
      const conversationHistory = currentActiveConversation?.messages || [];
      const aiResponseText = await apiService.sendChatMessage(trimmedMessage, activeFileIds, conversationHistory);
      
      const messageFromAi: ChatMessage = {
        id: (Date.now() + 1).toString(),
        content: aiResponseText,
        isUser: false,
        timestamp: new Date()
      };

      addMessageToActiveConversation(messageFromAi);
      
    } catch (error) {
      let userFriendlyErrorMessage = 'Sorry, I encountered an error processing your request. Please try again.';
      
      if (error instanceof Error) {
        if (error.message.includes('Cannot connect to backend')) {
          userFriendlyErrorMessage = `Cannot connect to the backend server. Please ensure your .NET backend is running at ${apiService.getBaseUrl()}.`;
        } else if (error.message.includes('HTTP error')) {
          userFriendlyErrorMessage = `Server error: ${error.message}. Please check the backend logs.`;
        } else {
          userFriendlyErrorMessage = `Error: ${error.message}`;
        }
      }
      
      const errorMessage: ChatMessage = {
        id: (Date.now() + 1).toString(),
        content: userFriendlyErrorMessage,
        isUser: false,
        timestamp: new Date()
      };
      addMessageToActiveConversation(errorMessage);
    } finally {
      setIsAiResponding(false);
    }
  }, [userTypingMessage, isAiResponding, activeFileIds, addMessageToActiveConversation, currentActiveConversation, createNewConversation, getActiveConversation]);

  const handleFileUpload = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFiles = e.target.files;
    if (!selectedFiles || selectedFiles.length === 0) return;

    const filesAsArray = Array.from(selectedFiles);
    await uploadFiles(filesAsArray);
    
    if (fileUploadInputRef.current) {
      fileUploadInputRef.current.value = '';
    }
  }, [uploadFiles]);

  const handleStartNewChat = useCallback(() => {
    createNewConversation();
  }, [createNewConversation]);

  const handleSwitchConversation = useCallback((conversationId: string) => {
    switchConversation(conversationId);
  }, [switchConversation]);

  const handleDeleteConversation = useCallback((e: React.MouseEvent, conversationId: string) => {
    e.stopPropagation();
    deleteConversation(conversationId);
  }, [deleteConversation]);

  const handleStartEditConversation = useCallback((e: React.MouseEvent, conversationId: string, currentTitle: string) => {
    e.stopPropagation();
    setConversationBeingEdited(conversationId);
    setNewConversationTitle(currentTitle);
  }, []);

  const handleSaveConversationTitle = useCallback(() => {
    if (conversationBeingEdited && newConversationTitle.trim()) {
      renameConversation(conversationBeingEdited, newConversationTitle.trim());
      setConversationBeingEdited(null);
      setNewConversationTitle('');
    }
  }, [conversationBeingEdited, newConversationTitle, renameConversation]);

  const handleCancelEditConversation = useCallback(() => {
    setConversationBeingEdited(null);
    setNewConversationTitle('');
  }, []);

  const handleKeyPressEditTitle = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSaveConversationTitle();
    } else if (e.key === 'Escape') {
      handleCancelEditConversation();
    }
  }, [handleSaveConversationTitle, handleCancelEditConversation]);

  const conversationsMatchingSearch = useMemo(() => {
    const normalizedSearchText = conversationSearchText.toLowerCase();

    return conversations.filter(conversation =>
      conversation.title.toLowerCase().includes(normalizedSearchText)
    );
  }, [conversations, conversationSearchText]);

  const handleToggleSidebar = useCallback(() => {
    if (onToggleSidebar) {
      onToggleSidebar();
    } else {
      setLocalSidebarCollapsed(!localSidebarCollapsed);
    }
  }, [onToggleSidebar, localSidebarCollapsed]);

  return (
    <div className="chat-layout">
      {/* Left Sidebar */}
      <aside className={`side-panel ${isSidebarCollapsed ? 'collapsed' : ''}`}>
        <div className="side-panel-header">
          <div className="company-logo">
            <div className="company-icon">
              <ChatIcon />
            </div>
            <h3>Conversations</h3>
          </div>
          <IconButton 
            onClick={handleToggleSidebar} 
            title="Toggle sidebar"
            size="small"
            className="sidebar-toggle-button"
          >
            {isSidebarCollapsed ? <MenuIcon fontSize="small" /> : <CloseIcon fontSize="small" />}
          </IconButton>
        </div>
        
        {!isSidebarCollapsed && (
          <div className="side-panel-content">
            {/* Button to start a new conversation */}
            <div className="new-chat-section">
              <Button 
                variant="contained" 
                fullWidth
                startIcon={<AddIcon />}
                onClick={handleStartNewChat}
                className="new-chat-button"
              >
                New Chat
              </Button>
            </div>
            
            {/* Search bar for finding conversations */}
            {conversations.length > 0 && (
              <div className="search-section">
                <div className="search-input-wrapper">
                  <div className="search-icon"></div>
                  <input
                    type="text"
                    placeholder="Search conversations..."
                    value={conversationSearchText}
                    onChange={(e) => setConversationSearchText(e.target.value)}
                    className="search-input"
                  />
                  {conversationSearchText && (
                    <IconButton
                      size="small"
                      onClick={() => setConversationSearchText('')}
                      title="Clear search"
                      className="clear-search-button"
                    >
                      <CloseIcon fontSize="small" />
                    </IconButton>
                  )}
                </div>
              </div>
            )}
            
            {/* List of conversations */}
            {conversationsMatchingSearch.length > 0 && (
              <div className="conversations-list">
                <h4>
                  {getConversationListHeading(conversationSearchText, conversationsMatchingSearch.length)}
                </h4>
                {conversationsMatchingSearch.map(conversation => (
                  <div
                    key={conversation.id}
                    className={`conversation-item ${conversation.isActive ? 'active' : ''}`}
                    onClick={() => handleSwitchConversation(conversation.id)}
                  >
                    <div className="conversation-info">
                      {conversationBeingEdited === conversation.id ? (
                        <input
                          type="text"
                          value={newConversationTitle}
                          onChange={(e) => setNewConversationTitle(e.target.value)}
                          onBlur={handleSaveConversationTitle}
                          onKeyDown={handleKeyPressEditTitle}
                          className="conversation-title-input"
                          autoFocus
                          onClick={(e) => e.stopPropagation()}
                        />
                      ) : (
                        <span className="conversation-title">{conversation.title}</span>
                      )}
                      <span className="conversation-date">
                        {conversation.updatedAt.toLocaleDateString()}
                      </span>
                    </div>
                    <div className="conversation-actions">
                      {conversationBeingEdited !== conversation.id && (
                        <IconButton
                          size="small"
                          onClick={(e) => handleStartEditConversation(e, conversation.id, conversation.title)}
                          title="Rename conversation"
                          className={`edit-conversation-button ${conversation.isActive ? 'active' : ''}`}
                        >
                          <EditIcon fontSize="small" />
                        </IconButton>
                      )}
                      <IconButton
                        size="small"
                        onClick={(e) => handleDeleteConversation(e, conversation.id)}
                        title="Delete conversation"
                        className={`delete-conversation-button ${conversation.isActive ? 'active' : ''}`}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </div>
                  </div>
                ))}
              </div>
            )}
            
            {/* Message when search doesn't find any conversations */}
            {conversations.length > 0 && conversationsMatchingSearch.length === 0 && conversationSearchText && (
              <div className="no-conversations-found">
                <div className="no-results-icon"></div>
                <p>No conversations found</p>
                <span>Try a different search term</span>
              </div>
            )}
            
            {/* Message when no conversations exist yet */}
            {conversations.length === 0 && (
              <div className="empty-conversations">
                <div className="empty-icon"></div>
                <p>No conversations yet</p>
                <span>Start a new chat to begin</span>
              </div>
            )}
          </div>
        )}
      </aside>

      {/* Main Chat Area */}
      <main className="chat-main">
        {/* Chat messages display area */}
        <div className="chat-messages" ref={chatMessagesContainerRef}>
          {/* Show empty state if no active conversation */}
          {!currentActiveConversation && (
            <div className="chat-empty-state">
              <h3>Welcome to Personal Knowledge Assistant</h3>
              <p>
                Start a conversation by typing your question below. I can help you with company documents, 
                technical resources, and any other questions you might have.
              </p>
            </div>
          )}
          
          {/* Show empty state if active conversation has no messages */}
          {currentActiveConversation && currentActiveConversation.messages.length === 0 && (
            <div className="chat-empty-state">
              <h3>New Conversation</h3>
              <p>
                Start chatting by typing your question below. I'm here to help with any questions you might have.
              </p>
            </div>
          )}
          
          {currentActiveConversation?.messages.map(message => (
            <div
              key={message.id}
              className={`message-container ${message.isUser ? 'user-message' : 'ai-message'}`}
            >
              <div className="message">
                {/* Show AI avatar for AI messages */}
                {!message.isUser && (
                  <div className="message-avatar">
                    <div className="avatar-icon"></div>
                  </div>
                )}
                <div className="message-content">
                  <MarkdownRenderer content={message.content} />
                </div>
                <div className="message-meta">
                  {message.timestamp.toLocaleTimeString()}
                </div>
              </div>
            </div>
          ))}
          
          {/* Show enhanced typing indicator when AI is responding */}
          {isAiResponding && (
            <div className="message-container ai-message">
              <div className="message">
                <div className="message-avatar">
                  <div className="avatar-icon"></div>
                </div>
                <div className="thinking-indicator">
                  <div className="processing-spinner"></div>
                  <span className="thinking-text">AI is analyzing your request</span>
                </div>
              </div>
            </div>
          )}
        </div>
        
        {/* Message input area */}
        <div className="chat-input-container">
          <form onSubmit={handleSendMessage}>
            <div className="chat-input-wrapper">
              <input
                type="text"
                value={userTypingMessage}
                onChange={(e) => setUserTypingMessage(e.target.value)}
                placeholder="Ask about company documentation, processes, or any questions..."
                className="chat-input"
                disabled={isAiResponding}
              />
              <button
                type="submit"
                className="btn btn-primary send-button"
                disabled={!userTypingMessage.trim() || isAiResponding}
                title="Send message"
              >
                <div className="send-icon"></div>
              </button>
            </div>
          </form>
        </div>
      </main>

      {/* Right Sidebar - Document Management */}
      <aside className="document-sidebar">
        <div className="document-sidebar-header">
          <h3>Your Documents</h3>
          <Button 
            variant="outlined" 
            startIcon={<UploadIcon />}
            onClick={() => fileUploadInputRef.current?.click()}
            size="small"
            className="upload-button"
          >
            Upload
          </Button>
          {/* Hidden file input */}
          <input
            ref={fileUploadInputRef}
            type="file"
            multiple
            accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt"
            onChange={handleFileUpload}
            style={{ display: 'none' }}
          />
        </div>
        
        {/* Show uploaded files if any exist */}
        {uploadedFiles.length > 0 ? (
          <div className="document-list">
            {uploadedFiles.map(file => (
              <div key={file.fileId} className="document-item">
                <div className="document-info">
                  <span className="document-name" title={file.fileName}>{file.fileName}</span>
                  <Chip 
                    label={file.fileState === FileState.COMPLETED ? 'Ready' : 'Processing...'}
                    size="small"
                    color={file.fileState === FileState.COMPLETED ? 'success' : 'default'}
                    variant={file.fileState === FileState.COMPLETED ? 'filled' : 'outlined'}
                  />
                </div>
                <IconButton
                  size="small"
                  onClick={() => removeFile(file)}
                  title="Remove document"
                  className="remove-document-button"
                >
                  <CloseIcon fontSize="small" />
                </IconButton>
              </div>
            ))}
          </div>
        ) : (
          // Show professional empty state when no documents are uploaded
          <div className="no-documents-state">
            <div className="empty-documents-icon"></div>
            <div className="empty-documents-content">
              <h4>No documents uploaded</h4>
              <p>Upload your documents to get started with intelligent Q&A and document analysis.</p>
              <div className="supported-formats">
                <span className="formats-label">Supported formats:</span>
                <span className="formats-list">PDF, DOCX, XLS, XLSX, PPT, PPTX, TXT</span>
              </div>
            </div>
          </div>
        )}
      </aside>
    </div>
  );
};

export default Chat; 