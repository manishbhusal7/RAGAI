import { useState, useEffect, useCallback } from 'react';
import { Conversation, ChatMessage } from '../types';

export const useConversations = () => {
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null);

  // Load conversations from localStorage on mount
  useEffect(() => {
    try {
      const savedConversations = localStorage.getItem('conversations');
      const savedActiveId = localStorage.getItem('activeConversationId');

      if (savedConversations) {
        const parsedConversations = JSON.parse(savedConversations).map((conv: any) => ({
          ...conv,
          createdAt: new Date(conv.createdAt),
          updatedAt: new Date(conv.updatedAt),
          messages: conv.messages
            .filter((msg: any) => msg && typeof msg.content === 'string') // Filter out invalid messages
            .map((msg: any) => ({
              ...msg,
              content: String(msg.content).trim(), // Ensure content is string
              timestamp: new Date(msg.timestamp),
              isUser: Boolean(msg.isUser) // Ensure boolean type
            }))
        }));

        setConversations(parsedConversations);

        if (savedActiveId && parsedConversations.find((c: Conversation) => c.id === savedActiveId)) {
          setActiveConversationId(savedActiveId);
        } else if (parsedConversations.length > 0) {
          setActiveConversationId(parsedConversations[0].id);
        }
      }
    } catch (error) {
      console.error('Error loading conversations - clearing corrupted data:', error);
      // Clear corrupted data
      localStorage.removeItem('conversations');
      localStorage.removeItem('activeConversationId');
      setConversations([]);
      setActiveConversationId(null);
    }
  }, []);

  // Save conversations to localStorage whenever they change
  const saveConversations = useCallback((convs: Conversation[], activeId: string | null) => {
    localStorage.setItem('conversations', JSON.stringify(convs));
    localStorage.setItem('activeConversationId', activeId || '');
  }, []);

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
      saveConversations(newConversations, newConversation.id);
      return newConversations;
    });

    setActiveConversationId(newConversation.id);
    return newConversation.id;
  }, [saveConversations]);

  const switchConversation = useCallback((conversationId: string) => {
    setConversations(prev => {
      const updated = prev.map(conv => ({
        ...conv,
        isActive: conv.id === conversationId
      }));
      saveConversations(updated, conversationId);
      return updated;
    });
    setActiveConversationId(conversationId);
  }, [saveConversations]);

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
        saveConversations(updated, activeConversationId);
      }
      
      return updated;
    });
  }, [activeConversationId, saveConversations]);

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
          saveConversations(updatedWithActive, newActiveId);
          setActiveConversationId(newActiveId);
          return updatedWithActive;
        } else {
          setActiveConversationId(null);
        }
      }
      
      saveConversations(updated, activeConversationId);
      return updated;
    });
  }, [activeConversationId, saveConversations]);

  const renameConversation = useCallback((conversationId: string, newTitle: string) => {
    setConversations(prev => {
      const updated = prev.map(conv => 
        conv.id === conversationId 
          ? { ...conv, title: newTitle.trim() || 'Untitled Chat', updatedAt: new Date() }
          : conv
      );
      saveConversations(updated, activeConversationId);
      return updated;
    });
  }, [activeConversationId, saveConversations]);

  const clearAllConversations = useCallback(() => {
    setConversations([]);
    setActiveConversationId(null);
    localStorage.removeItem('conversations');
    localStorage.removeItem('activeConversationId');
  }, []);

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