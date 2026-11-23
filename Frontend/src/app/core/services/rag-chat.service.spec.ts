import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RagChatService } from './rag-chat.service';
import { SessionService } from './session.service';

const reply = (id: string, text = 'Live AI answer') => ({
  conversationId: id,
  messageId: 'message-' + id,
  result: text,
  source: [],
});

describe('Live chat integration', () => {
  let http: HttpTestingController;
  let chat: RagChatService;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: SessionService,
          useValue: {
            initialize: () => Promise.resolve(),
            session: () => ({ sessionId: 'session' }),
          },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    chat = TestBed.inject(RagChatService);
  });
  afterEach(() => http.verify());

  it('posts the first query without a conversation ID and shows the API reply', async () => {
    const sent = chat.sendMessage('hello');
    await Promise.resolve();
    await Promise.resolve();
    const request = http.expectOne('/api/ai/chat');
    expect(request.request.body).toEqual({ query: 'hello' });
    expect(request.request.withCredentials).toBe(true);
    request.flush(reply('conversation-1'));
    await sent;
    expect(chat.activeThread()?.conversationId).toBe('conversation-1');
    expect(chat.activeThread()?.messages[1].content).toBe('Live AI answer');
    expect(chat.activeThread()?.messages[1].id).toBe('message-conversation-1');
    expect(chat.isGenerating()).toBe(false);
  });

  it('keeps conversation IDs separate when switching chats', async () => {
    const firstId = chat.activeThreadId();
    let sent = chat.sendMessage('first');
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne('/api/ai/chat').flush(reply('server-1'));
    await sent;
    const secondId = chat.createNewThread();
    sent = chat.sendMessage('second');
    await Promise.resolve();
    await Promise.resolve();
    const second = http.expectOne('/api/ai/chat');
    expect(second.request.body).toEqual({ query: 'second' });
    second.flush(reply('server-2'));
    await sent;
    chat.selectThread(firstId);
    sent = chat.sendMessage('follow up');
    await Promise.resolve();
    await Promise.resolve();
    const follow = http.expectOne('/api/ai/chat');
    expect(follow.request.body).toEqual({ query: 'follow up', conversationId: 'server-1' });
    follow.flush(reply('server-1', 'First conversation reply'));
    await sent;
    expect(chat.activeThread()?.messages.length).toBe(4);
    chat.selectThread(secondId);
    expect(chat.activeThread()?.conversationId).toBe('server-2');
    expect(chat.activeThread()?.messages.length).toBe(2);
  });

  it('binds a delayed response to its original chat', async () => {
    const id = chat.activeThreadId();
    const sent = chat.sendMessage('pending');
    await Promise.resolve();
    await Promise.resolve();
    const request = http.expectOne('/api/ai/chat');
    chat.createNewThread();
    request.flush(reply('original'));
    await sent;
    expect(chat.activeThread()?.messages).toEqual([]);
    expect(chat.threads().find((t) => t.id === id)?.conversationId).toBe('original');
  });

  it('preserves failed prompts and retries without duplicating the bubble', async () => {
    const sent = chat.sendMessage('retry me');
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne('/api/ai/chat').flush({}, { status: 503, statusText: 'Unavailable' });
    await sent;
    expect(chat.activeThread()?.messages[0].delivery).toBe('failed');
    expect(chat.activeThread()?.error).toBeTruthy();
    const retry = chat.retryMessage(chat.activeThreadId(), chat.activeThread()!.messages[0].id);
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne('/api/ai/chat').flush(reply('recovered'));
    await retry;
    expect(chat.activeThread()?.messages.length).toBe(2);
    expect(chat.activeThread()?.error).toBeUndefined();
  });

  it('loads saved conversations and their messages', async () => {
    const loaded = chat.loadConversations();
    await Promise.resolve();
    await Promise.resolve();
    const saved = {
      id: 'saved-1',
      title: 'Saved chat',
      createdAt: '2026-11-23T00:00:00Z',
      updatedAt: '2026-11-23T00:00:00Z',
    };
    http.expectOne('/api/conversations').flush([saved]);
    await loaded;
    const selected = chat.loadThread('saved-1');
    await Promise.resolve();
    await Promise.resolve();
    http
      .expectOne('/api/conversations/saved-1')
      .flush({
        ...saved,
        messages: [
          { id: 'stored', role: 'assistant', content: 'Stored answer', createdAt: saved.createdAt },
        ],
      });
    await selected;
    chat.selectThread('saved-1');
    expect(chat.activeThread()?.messages[0].content).toBe('Stored answer');
  });

  it('deletes persisted conversations through the API', async () => {
    let sent = chat.sendMessage('delete me');
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne('/api/ai/chat').flush(reply('delete-1'));
    await sent;
    const deleting = chat.deleteThread(chat.activeThreadId());
    await Promise.resolve();
    await Promise.resolve();
    const request = http.expectOne('/api/conversations/delete-1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await deleting;
    expect(chat.threads().some((t) => t.conversationId === 'delete-1')).toBe(false);
  });
});
