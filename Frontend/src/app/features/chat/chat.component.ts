import { Component, inject, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { RagChatService } from '../../core/services/rag-chat.service';
import { RagSettingsService } from '../../core/services/rag-settings.service';
import { Citation } from '../../core/models/rag.model';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss',
})
export class ChatComponent implements AfterViewChecked {
  readonly chatService = inject(RagChatService);
  readonly settingsService = inject(RagSettingsService);

  @ViewChild('messagesContainer') private messagesContainer!: ElementRef<HTMLDivElement>;
  @ViewChild('promptTextarea') private promptTextarea!: ElementRef<HTMLTextAreaElement>;

  userPrompt = '';
  private shouldScrollToBottom = false;

  readonly starterQuestions = [
    'How does ImAI ensure accurate grounding and prevent hallucinations?',
    'What is the semantic chunking strategy used in the architecture?',
    'Explain the SOC2 compliance and data retention guidelines.',
    'How does hybrid retrieval combine BM25 and dense vector scoring?',
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
    this.shouldScrollToBottom = true;

    await this.chatService.sendMessage(query);
    this.shouldScrollToBottom = true;
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
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
