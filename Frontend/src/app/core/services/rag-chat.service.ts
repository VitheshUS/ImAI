import { Injectable, signal, computed, inject } from '@angular/core';
import { ChatMessage, Citation, ConversationThread } from '../models/rag.model';
import { RagSettingsService } from './rag-settings.service';
import { DocumentService } from './document.service';

@Injectable({
  providedIn: 'root',
})
export class RagChatService {
  private readonly settingsService = inject(RagSettingsService);
  private readonly documentService = inject(DocumentService);

  private readonly _threads = signal<ConversationThread[]>([
    {
      id: 'thread-welcome',
      title: 'New conversation',
      createdAt: new Date(),
      updatedAt: new Date(),
      messages: [],
    },
  ]);

  readonly threads = this._threads.asReadonly();
  readonly activeThreadId = signal<string>('thread-welcome');
  readonly isGenerating = signal<boolean>(false);

  readonly activeThread = computed(() => {
    const list = this._threads();
    const id = this.activeThreadId();
    return list.find((t) => t.id === id) ?? list[0] ?? null;
  });

  // Citations Drawer State
  readonly activeCitation = signal<Citation | null>(null);
  readonly isCitationDrawerOpen = signal<boolean>(false);

  selectThread(threadId: string): void {
    this.activeThreadId.set(threadId);
  }

  createNewThread(title = 'New Investigation'): string {
    const newId = 'thread-' + Math.random().toString(36).substring(2, 9);
    const newThread: ConversationThread = {
      id: newId,
      title,
      createdAt: new Date(),
      updatedAt: new Date(),
      messages: [],
    };
    this._threads.update((list) => [newThread, ...list]);
    this.activeThreadId.set(newId);
    return newId;
  }

  deleteThread(threadId: string): void {
    this._threads.update((list) => list.filter((t) => t.id !== threadId));
    if (this.activeThreadId() === threadId) {
      const remaining = this._threads();
      if (remaining.length > 0) {
        this.activeThreadId.set(remaining[0].id);
      } else {
        this.createNewThread('New Conversation');
      }
    }
  }

  openCitation(citation: Citation): void {
    this.activeCitation.set(citation);
    this.isCitationDrawerOpen.set(true);
  }

  closeCitationDrawer(): void {
    this.isCitationDrawerOpen.set(false);
  }

  /**
   * Dispatches query to RAG pipeline.
   * Can be hooked to backend API via HttpClient:
   * this.http.post<RagResponse>('/api/rag/query', { query, collectionId, settings })
   */
  async sendMessage(query: string): Promise<void> {
    if (!query.trim() || this.isGenerating()) return;

    const currentThread = this.activeThread();
    if (!currentThread) return;

    const userMessage: ChatMessage = {
      id: 'msg-' + Math.random().toString(36).substring(2, 9),
      role: 'user',
      content: query.trim(),
      timestamp: new Date(),
    };

    // Append user message immediately
    this.appendMessage(currentThread.id, userMessage);

    // Update thread title if first message
    if (currentThread.messages.length === 0) {
      const snippet = query.length > 38 ? query.substring(0, 38) + '...' : query;
      this.updateThreadTitle(currentThread.id, snippet);
    }

    this.isGenerating.set(true);

    try {
      // Simulate retrieval + generation timing
      await new Promise((res) => setTimeout(res, 1200));

      const mockCitations = this.generateMockCitations(query);
      const assistantMessage: ChatMessage = {
        id: 'msg-' + Math.random().toString(36).substring(2, 9),
        role: 'assistant',
        content: this.formatSimulatedAnswer(query),
        timestamp: new Date(),
        latencyMs: 1180,
        retrievalLatencyMs: 230,
        tokenCount: 210,
        citations: mockCitations,
      };

      this.appendMessage(currentThread.id, assistantMessage);
    } finally {
      this.isGenerating.set(false);
    }
  }

  private appendMessage(threadId: string, message: ChatMessage): void {
    this._threads.update((list) =>
      list.map((t) => {
        if (t.id === threadId) {
          return {
            ...t,
            updatedAt: new Date(),
            messages: [...t.messages, message],
          };
        }
        return t;
      }),
    );
  }

  private updateThreadTitle(threadId: string, title: string): void {
    this._threads.update((list) => list.map((t) => (t.id === threadId ? { ...t, title } : t)));
  }

  private generateMockCitations(query: string): Citation[] {
    const collection = this.settingsService.settings().activeCollectionId;
    const docs = this.documentService
      .documents()
      .filter((d) => d.status === 'ready' && (collection === 'all' || d.collection === collection));
    if (!docs.length) return [];
    return docs.slice(0, 2).map((doc, index) => ({
      id: 'sample-' + doc.id,
      index: index + 1,
      documentId: doc.id,
      documentTitle: doc.title,
      pageNumber: 1,
      chunkId: 'sample-chunk-' + doc.id,
      score: 0,
      snippet: 'Sample document summary (not a retrieved passage): ' + doc.description,
    }));
  }

  private formatSimulatedAnswer(query: string): string {
    return `This is a preview response to: "${query}".

Live retrieval and an AI model are not connected yet. The sample source cards below demonstrate how you will be able to inspect supporting passages once the backend is connected.

Explore the document library, change the knowledge collection, or start another conversation.`;
  }
}
