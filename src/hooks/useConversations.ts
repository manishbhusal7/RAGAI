import { useState, useEffect, useCallback } from 'react';
import { Conversation, ChatMessage } from '../types';
import { getFromLocalStorage, saveToLocalStorage, removeFromLocalStorage } from '../utils/localStorage';
import { logger, createLogger } from '../utils/logger';
import { isValidChatMessage } from '../utils/validation';

const STORAGE_KEYS = {
  CONVERSATIONS: 'conversations',
  ACTIVE_CONVERSATION_ID: 'activeConversationId',
} as const;

/**
 * Custom hook for managing conversation state and persistence
 * Handles loading, saving, and manipulating conversations with localStorage
 */
export const useConversations = () => {
  const log = createLogger('useConversations');
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null);

  // Load conversations from localStorage on mount
  useEffect(() => {
    loadConversations();
  }, []);

  /**
   * Load conversations from localStorage with error handling
   */
  const loadConversations = useCallback(() => {
    try {
      const savedConversations = getFromLocalStorage<any[]>(STORAGE_KEYS.CONVERSATIONS, []);
      const savedActiveId = getFromLocalStorage<string | null>(STORAGE_KEYS.ACTIVE_CONVERSATION_ID, null);

      if (savedConversations.length > 0) {
        // Deserialize and validate conversations
        const parsedConversations = savedConversations
          .map(conv => deserializeConversation(conv))
          .filter((conv): conv is Conversation => conv !== null);

        setConversations(parsedConversations);
        log.debug(`Loaded ${parsedConversations.length} conversations`);

        // Set active conversation if valid
        if (savedActiveId && parsedConversations.some(c => c.id === savedActiveId)) {
          setActiveConversationId(savedActiveId);
        } else if (parsedConversations.length > 0) {
          setActiveConversationId(parsedConversations[0].id);
        }
      }
    } catch (error) {
      log.error('Error loading conversations', error);
      // Clear corrupted data
      clearAllConversations();
    }
  }, [log]);

  /**
   * Deserialize conversation from storage format
   */
  const deserializeConversation = (conv: any): Conversation | null => {
    try {
      if (!conv || typeof conv !== 'object') {
        return null;
      }

      return {
        id: String(conv.id),
        title: String(conv.title || 'Untitled'),
        createdAt: new Date(conv.createdAt),
        updatedAt: new Date(conv.updatedAt),
        messages: Array.isArray(conv.messages)
          ? conv.messages
              .filter(msg => isValidChatMessage(msg))
              .map(msg => ({
                id: String(msg.id),
                content: String(msg.content).trim(),
                isUser: Boolean(msg.isUser),
                timestamp: new Date(msg.timestamp),
              }))
          : [],
        isActive: Boolean(conv.isActive),
      };
    } catch (error) {
      log.warn('Failed to deserialize conversation', error);
      return null;
    }
  };

  /**
   * Save conversations to localStorage
   */
  const persistConversations = useCallback((convs: Conversation[], activeId: string | null) => {
    const success1 = saveToLocalStorage(STORAGE_KEYS.CONVERSATIONS, convs);
    const success2 = saveToLocalStorage(STORAGE_KEYS.ACTIVE_CONVERSATION_ID, activeId);

    if (!success1 || !success2) {
      log.warn('Failed to persist conversations to localStorage');
    }
  }, [log]);

  const createNewConversation = useCallback((): string => {
    const newConversation: Conversation = {
      id: Date.now().toString(),
      title: 'New Chat',
      createdAt: new Date(),
      updatedAt: new Date(),
      messages: [],
      isActive: true
    };

    setConversations(prev => {
      const updatedConversations = prev.map(conv => ({
        ...conv,
        isActive: false
      }));
      const newConversations = [...updatedConversations, newConversation];
      persistConversations(newConversations, newConversation.id);
      return newConversations;
    });

    setActiveConversationId(newConversation.id);
    log.info(`Created new conversation: ${newConversation.id}`);
    return newConversation.id;
  }, [persistConversations, log]);

  const switchConversation = useCallback((conversationId: string) => {
    setConversations(prev => {
      const updated = prev.map(conv => ({
        ...conv,
        isActive: conv.id === conversationId
      }));
      persistConversations(updated, conversationId);
      return updated;
    });
    setActiveConversationId(conversationId);
    log.debug(`Switched to conversation: ${conversationId}`);
  }, [persistConversations, log]);

  const getActiveConversation = useCallback((): Conversation | null => {
    return conversations.find(conv => conv.isActive) || null;
  }, [conversations]);

  const addMessageToActiveConversation = useCallback((message: ChatMessage) => {
    setConversations(prev => {
      const updated = [...prev];
      const activeIndex = updated.findIndex(conv => conv.id === activeConversationId);
      
      if (activeIndex !== -1) {
        const conv = { ...updated[activeIndex] };
        conv.messages = [...conv.messages, { ...message }];
        conv.updatedAt = new Date();
        
        // Update title if first user message
        if (message.isUser && conv.messages.length === 1) {
          conv.title = message.content.substring(0, 50) + (message.content.length > 50 ? '...' : '');
        }
        
        updated[activeIndex] = conv;
        persistConversations(updated, activeConversationId);
      }
      
      return updated;
    });
  }, [activeConversationId, persistConversations]);

  const deleteConversation = useCallback((conversationId: string) => {
    setConversations(prev => {
      const updated = prev.filter(conv => conv.id !== conversationId);
      
      // If we're deleting the active conversation, switch to the first available one
      if (activeConversationId === conversationId) {
        const firstConversation = updated[0];
        const newActiveId = firstConversation ? firstConversation.id : null;
        
        if (firstConversation) {
          const updatedWithActive = updated.map(conv => ({
            ...conv,
            isActive: conv.id === newActiveId
          }));
          persistConversations(updatedWithActive, newActiveId);
          setActiveConversationId(newActiveId);
          log.info(`Deleted conversation, switched to: ${newActiveId}`);
          return updatedWithActive;
        } else {
          setActiveConversationId(null);
        }
      }
      
      persistConversations(updated, activeConversationId);
      log.info(`Deleted conversation: ${conversationId}`);
      return updated;
    });
  }, [activeConversationId, persistConversations, log]);

  const renameConversation = useCallback((conversationId: string, newTitle: string) => {
    setConversations(prev => {
      const updated = prev.map(conv => 
        conv.id === conversationId 
          ? { ...conv, title: newTitle.trim() || 'Untitled Chat', updatedAt: new Date() }
          : conv
      );
      persistConversations(updated, activeConversationId);
      log.debug(`Renamed conversation: ${conversationId}`);
      return updated;
    });
  }, [activeConversationId, persistConversations, log]);

  const clearAllConversations = useCallback(() => {
    setConversations([]);
    setActiveConversationId(null);
    removeFromLocalStorage(STORAGE_KEYS.CONVERSATIONS);
    removeFromLocalStorage(STORAGE_KEYS.ACTIVE_CONVERSATION_ID);
    log.info('Cleared all conversations');
  }, [log]);

  return {
    conversations,
    activeConversationId,
    createNewConversation,
    switchConversation,
    getActiveConversation,
    addMessageToActiveConversation,
    deleteConversation,
    renameConversation,
    clearAllConversations
  };
};
