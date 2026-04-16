export interface Conversation {
  id: string;
  title: string;
  createdAt: Date;
  updatedAt: Date;
  messages: ChatMessage[];
  isActive: boolean;
}

export interface ChatMessage {
  id: string;
  content: string;
  isUser: boolean;
  timestamp: Date;
  documentReferences?: DocumentReference[];
}

export interface DocumentReference {
  documentId: string;
  documentName: string;
  pageNumber?: number;
  confidence: number;
}

export interface FileRecord {
  fileId: string;
  fileName: string;
  fileSize: number;
  fileType: string;
  createdAt: Date;
  fileState: FileState;
  progressPercent?: number;
}

export enum FileState {
  INITIATING = 'initiating',
  ANALYZING = 'analyzing',
  COMPLETED = 'completed',
  FAILED = 'failed'
} 