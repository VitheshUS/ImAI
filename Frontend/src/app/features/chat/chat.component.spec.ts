import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ChatComponent } from './chat.component';
import { SessionService } from '../../core/services/session.service';

describe('Chat UI with backend responses', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [ChatComponent],
      providers: [
        provideRouter([]),
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
    }),
  );

  it('renders the real response and retains its conversation ID', async () => {
    const fixture = TestBed.createComponent(ChatComponent);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await Promise.resolve();
    await Promise.resolve();
    http.expectOne('/api/conversations').flush([]);
    await Promise.resolve();
    await Promise.resolve();
    fixture.componentInstance.userPrompt = 'My first prompt';
    const sent = fixture.componentInstance.onSendMessage();
    await Promise.resolve();
    await Promise.resolve();
    http
      .expectOne('/api/ai/chat')
      .flush({
        conversationId: 'saved-chat',
        messageId: 'saved-reply',
        result: 'Answer returned by the API',
        source: [],
      });
    await sent;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('My first prompt');
    expect(fixture.nativeElement.textContent).toContain('Answer returned by the API');
    expect(fixture.nativeElement.textContent).not.toContain('DEMO RESPONSE');
    expect(fixture.componentInstance.chatService.activeThread()?.conversationId).toBe('saved-chat');
    http.verify();
  });
});
