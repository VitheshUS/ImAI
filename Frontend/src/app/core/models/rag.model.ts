export type MessageRole = 'user' | 'assistant' | 'system';

export interface Citation {
  id: string;
  index: number;
  documentId: string;
  documentTitle: string;
  pageNumber?: number;
  chunkId: string;
  score: number;
  snippet: string;
  metadata?: Record<string, unknown>;
}

export interface ChatMessage {
  id: string;
  role: MessageRole;
  content: string;
  timestamp: Date;
  citations?: Citation[];
  isStreaming?: boolean;
  latencyMs?: number;
  retrievalLatencyMs?: number;
  tokenCount?: number;
  selectedCollection?: string;
  delivery?: 'sending' | 'sent' | 'failed';
}

export interface ConversationThread {
  conversationId?: string;
  messagesLoaded?: boolean;
  loading?: boolean;
  error?: string;
  id: string;
  title: string;
  createdAt: Date;
  updatedAt: Date;
  messages: ChatMessage[];
  pinned?: boolean;
}

export type DocumentStatus = 'indexed' | 'chunking' | 'embedding' | 'ready' | 'failed';

export interface KnowledgeDocument {
  id: string;
  title: string;
  filename: string;
  type: 'pdf' | 'docx' | 'markdown' | 'txt' | 'csv' | 'json';
  sizeBytes: number;
  uploadedAt: Date;
  chunksCount: number;
  status: DocumentStatus;
  collection: string;
  description: string;
}

export interface KnowledgeCollection {
  id: string;
  name: string;
  description: string;
  documentCount: number;
  totalChunks: number;
}

export interface RagSettings {
  model: string;
  temperature: number;
  retrievalTopK: number;
  similarityCutoff: number;
  searchType: 'hybrid' | 'dense_vector' | 'keyword_bm25';
  rerankerEnabled: boolean;
  systemPrompt: string;
  activeCollectionId: string;
}

export interface RetrievedChunkResult {
  chunkId: string;
  documentTitle: string;
  documentId: string;
  content: string;
  score: number;
  pageNumber?: number;
  collection: string;
}
