import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ChatMessage, Citation, ConversationThread } from '../models/rag.model';
import { API_BASE_URL } from '../config/api.config';
import { SessionService } from './session.service';

interface ChatReply {
  conversationId: string;
  messageId: string;
  result: string;
  source: string[];
}
interface SavedMessage {
  id: string;
  role: ChatMessage['role'];
  content: string;
  createdAt: string;
}
interface SavedConversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messages?: SavedMessage[];
}

@Injectable({ providedIn: 'root' })
export class RagChatService {
  private readonly http = inject(HttpClient);
  private readonly sessionService = inject(SessionService);
  private readonly apiUrl = inject(API_BASE_URL).replace(/\/+$/, '');
  private readonly _threads = signal<ConversationThread[]>([
    {
      id: 'thread-welcome',
      title: 'New conversation',
      createdAt: new Date(),
      updatedAt: new Date(),
      messages: [],
      messagesLoaded: true,
    },
  ]);
  private readonly pendingThreads = signal(new Set<string>());
  private historyLoaded = false;
  readonly threads = this._threads.asReadonly();
  readonly activeThreadId = signal('thread-welcome');
  readonly activeThread = computed(
    () => this._threads().find((t) => t.id === this.activeThreadId()) ?? null,
  );
  readonly isGenerating = computed(() => this.pendingThreads().has(this.activeThreadId()));
  readonly isLoading = computed(() => this.activeThread()?.loading ?? false);
  readonly historyLoading = signal(false);
  readonly historyError = signal<string | null>(null);
  readonly activeCitation = signal<Citation | null>(null);
  readonly isCitationDrawerOpen = signal(false);

  async loadConversations(): Promise<void> {
    if (this.historyLoaded || this.historyLoading()) return;
    this.historyLoading.set(true);
    this.historyError.set(null);
    try {
      await this.ensureSession();
      const saved = await firstValueFrom(
        this.http.get<SavedConversation[]>(this.apiUrl + '/conversations', {
          withCredentials: true,
        }),
      );
      this._threads.update((current) => [
        ...current,
        ...saved
          .filter((s) => !current.some((t) => t.conversationId === s.id))
          .map((s) => this.fromSaved(s)),
      ]);
      this.historyLoaded = true;
    } catch (error) {
      this.historyError.set(this.errorMessage(error));
    } finally {
      this.historyLoading.set(false);
    }
  }

  selectThread(id: string): void {
    this.activeThreadId.set(id);
    const thread = this._threads().find((t) => t.id === id);
    if (thread?.conversationId && !thread.messagesLoaded && !thread.loading)
      void this.loadThread(id);
  }

  async loadThread(id: string): Promise<void> {
    const thread = this._threads().find((t) => t.id === id);
    if (!thread?.conversationId || thread.loading || this.isThreadBusy(id)) return;
    this.updateThread(id, { loading: true, error: undefined });
    try {
      await this.ensureSession();
      const saved = await firstValueFrom(
        this.http.get<SavedConversation>(this.apiUrl + '/conversations/' + thread.conversationId, {
          withCredentials: true,
        }),
      );
      this.updateThread(id, { ...this.fromSaved(saved), id, loading: false });
    } catch (error) {
      this.updateThread(id, { loading: false, error: this.errorMessage(error) });
    }
  }

  createNewThread(title = 'New conversation'): string {
    const id = crypto.randomUUID();
    this._threads.update((list) => [
      {
        id,
        title,
        createdAt: new Date(),
        updatedAt: new Date(),
        messages: [],
        messagesLoaded: true,
      },
      ...list,
    ]);
    this.activeThreadId.set(id);
    return id;
  }

  async deleteThread(id: string): Promise<void> {
    const thread = this._threads().find((t) => t.id === id);
    if (!thread || this.isThreadBusy(id) || thread.loading) return;
    this.updateThread(id, { loading: true, error: undefined });
    try {
      if (thread.conversationId) {
        await this.ensureSession();
        await firstValueFrom(
          this.http.delete(this.apiUrl + '/conversations/' + thread.conversationId, {
            withCredentials: true,
          }),
        );
      }
      this._threads.update((list) => list.filter((t) => t.id !== id));
      if (this.activeThreadId() === id) {
        const next = this._threads()[0];
        if (next) this.selectThread(next.id);
        else this.createNewThread();
      }
    } catch (error) {
      this.updateThread(id, { loading: false, error: this.errorMessage(error) });
    }
  }

  isThreadBusy(id: string): boolean {
    return this.pendingThreads().has(id);
  }
  openCitation(citation: Citation): void {
    this.activeCitation.set(citation);
    this.isCitationDrawerOpen.set(true);
  }
  closeCitationDrawer(): void {
    this.isCitationDrawerOpen.set(false);
  }

  async sendMessage(query: string): Promise<void> {
    const thread = this.activeThread();
    if (
      !query.trim() ||
      !thread ||
      this.isThreadBusy(thread.id) ||
      thread.loading ||
      thread.messagesLoaded === false
    )
      return;
    const message: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: query.trim(),
      timestamp: new Date(),
      delivery: 'sending',
    };
    this.updateThread(thread.id, {
      messages: [...thread.messages, message],
      title: thread.messages.length ? thread.title : this.title(query),
      error: undefined,
    });
    await this.submit(thread.id, message);
  }

  async retryMessage(id: string, messageId: string): Promise<void> {
    const thread = this._threads().find((t) => t.id === id);
    const message = thread?.messages.find((m) => m.id === messageId && m.delivery === 'failed');
    if (!thread || !message || this.isThreadBusy(id) || thread.loading) return;
    this.updateMessage(id, messageId, { delivery: 'sending' });
    this.updateThread(id, { error: undefined });
    await this.submit(id, message);
  }

  private async submit(id: string, message: ChatMessage): Promise<void> {
    this.pendingThreads.update((ids) => new Set([...ids, id]));
    try {
      await this.ensureSession();
      const thread = this._threads().find((t) => t.id === id)!;
      const request = thread.conversationId
        ? { query: message.content, conversationId: thread.conversationId }
        : { query: message.content };
      const reply = await firstValueFrom(
        this.http.post<ChatReply>(this.apiUrl + '/ai/chat', request, { withCredentials: true }),
      );
      this._threads.update((list) =>
        list.map((t) =>
          t.id !== id
            ? t
            : {
                ...t,
                conversationId: reply.conversationId,
                updatedAt: new Date(),
                messagesLoaded: true,
                error: undefined,
                messages: [
                  ...t.messages.map((m) =>
                    m.id === message.id ? { ...m, delivery: 'sent' as const } : m,
                  ),
                  {
                    id: reply.messageId,
                    role: 'assistant',
                    content: reply.result,
                    timestamp: new Date(),
                    delivery: 'sent',
                  },
                ],
              },
        ),
      );
    } catch (error) {
      this.updateMessage(id, message.id, { delivery: 'failed' });
      this.updateThread(id, { error: this.errorMessage(error) });
    } finally {
      this.pendingThreads.update((ids) => {
        const next = new Set(ids);
        next.delete(id);
        return next;
      });
    }
  }

  private async ensureSession(): Promise<void> {
    await this.sessionService.initialize();
    if (!this.sessionService.session()) throw new Error('Session unavailable');
  }
  private fromSaved(saved: SavedConversation): ConversationThread {
    return {
      id: saved.id,
      conversationId: saved.id,
      title: this.title(saved.title),
      createdAt: new Date(saved.createdAt),
      updatedAt: new Date(saved.updatedAt),
      messagesLoaded: saved.messages !== undefined,
      messages: (saved.messages ?? []).map((m) => ({
        id: m.id,
        role: m.role,
        content: m.content,
        timestamp: new Date(m.createdAt),
        delivery: 'sent',
      })),
    };
  }
  private title(text: string): string {
    return text.length > 38 ? text.slice(0, 38) + '…' : text;
  }
  private updateThread(id: string, patch: Partial<ConversationThread>): void {
    this._threads.update((list) => list.map((t) => (t.id === id ? { ...t, ...patch } : t)));
  }
  private updateMessage(id: string, messageId: string, patch: Partial<ChatMessage>): void {
    this._threads.update((list) =>
      list.map((t) =>
        t.id === id
          ? { ...t, messages: t.messages.map((m) => (m.id === messageId ? { ...m, ...patch } : m)) }
          : t,
      ),
    );
  }
  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401)
        return 'Your session has expired. Reload ImAI to start a new session.';
      if (error.status === 404) return 'This conversation is no longer available in your session.';
      if (error.status === 409)
        return 'This conversation changed in another request. Reload its messages before retrying.';
      if (error.status === 0) return 'Could not reach ImAI. Check your connection and try again.';
    }
    return 'Could not complete the request. Please try again.';
  }
}
