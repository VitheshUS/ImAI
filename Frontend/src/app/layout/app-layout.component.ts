import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { RagChatService } from '../core/services/rag-chat.service';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.scss',
})
export class AppLayoutComponent {
  readonly chatService = inject(RagChatService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  readonly showSidebar = signal<boolean>(false);

  toggleSidebarVisibility(): void {
    this.showSidebar.update((val) => !val);
  }

  startNewChat(): void {
    this.showSidebar.set(false);
    this.chatService.createNewThread();
    this.router.navigate(['/chat']);
  }

  onSelectThread(id: string): void {
    this.showSidebar.set(false);
    this.chatService.selectThread(id);
    this.router.navigate(['/chat']);
  }

  onDeleteThread(event: Event, id: string): void {
    event.stopPropagation();
    const thread = this.chatService.threads().find((t) => t.id === id);
    if (thread?.conversationId && !confirm('Delete this conversation and its saved messages?'))
      return;
    void this.chatService.deleteThread(id);
  }

  closeDrawer(): void {
    this.chatService.closeCitationDrawer();
  }
}
