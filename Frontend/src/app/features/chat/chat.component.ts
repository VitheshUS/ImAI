import { Component, inject, ViewChild, ElementRef, AfterViewChecked, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RagChatService } from '../../core/services/rag-chat.service';
import { Citation } from '../../core/models/rag.model';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss',
})
export class ChatComponent implements AfterViewChecked, OnInit {
  readonly chatService = inject(RagChatService);

  @ViewChild('messagesContainer') private messagesContainer!: ElementRef<HTMLDivElement>;
  @ViewChild('promptTextarea') private promptTextarea!: ElementRef<HTMLTextAreaElement>;

  ngOnInit(): void {
    void this.chatService.loadConversations();
  }

  userPrompt = '';
  private shouldScrollToBottom = false;

  readonly starterQuestions = [
    'Explain retrieval augmented generation in simple terms.',
    'How do embeddings represent meaning?',
    'Compare keyword and semantic search.',
    'Help me plan a document search application.',
  ];

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
      this.shouldScrollToBottom = false;
    }
  }

  async onSendMessage(): Promise<void> {
    if (!this.userPrompt.trim() || this.chatService.isGenerating()) return;

    const query = this.userPrompt;
    this.userPrompt = '';
    if (this.promptTextarea) this.promptTextarea.nativeElement.style.height = 'auto';
    this.shouldScrollToBottom = true;

    await this.chatService.sendMessage(query);
    this.shouldScrollToBottom = true;
  }

  resizeComposer(): void {
    const textarea = this.promptTextarea.nativeElement;
    textarea.style.height = 'auto';
    textarea.style.height = Math.min(textarea.scrollHeight, 160) + 'px';
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      this.onSendMessage();
    }
  }

  selectStarterQuestion(question: string): void {
    this.userPrompt = question;
    this.onSendMessage();
  }

  onInspectCitation(citation: Citation): void {
    this.chatService.openCitation(citation);
  }

  private scrollToBottom(): void {
    if (this.messagesContainer) {
      try {
        const el = this.messagesContainer.nativeElement;
        el.scrollTop = el.scrollHeight;
      } catch (err) {
        console.error('Scroll error:', err);
      }
    }
  }
}
