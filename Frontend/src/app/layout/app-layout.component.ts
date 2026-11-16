import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { RagChatService } from '../core/services/rag-chat.service';
import { RagSettingsService } from '../core/services/rag-settings.service';
import { DocumentService } from '../core/services/document.service';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './app-layout.component.html',
  styleUrl: './app-layout.component.scss',
})
export class AppLayoutComponent {
  readonly chatService = inject(RagChatService);
  readonly settingsService = inject(RagSettingsService);
  readonly documentService = inject(DocumentService);
  readonly authService = inject(AuthService);
  readonly router = inject(Router);

  /**
   * Sidebar visibility scope:
   * Defaulted to false for a clean, distraction-free Chat UI.
   * Toggle to true whenever you wish to expose the sidebar and multi-page navigation.
   */
  readonly showSidebar = signal<boolean>(false);
  isSidebarCollapsed = false;

  toggleSidebarVisibility(): void {
    this.showSidebar.update((val) => !val);
  }

  toggleSidebarCollapse(): void {
    this.isSidebarCollapsed = !this.isSidebarCollapsed;
  }

  startNewChat(): void {
    this.chatService.createNewThread();
    this.router.navigate(['/chat']);
  }

  onSelectThread(id: string): void {
    this.chatService.selectThread(id);
    this.router.navigate(['/chat']);
  }

  onDeleteThread(event: Event, id: string): void {
    event.stopPropagation();
    this.chatService.deleteThread(id);
  }

  onCollectionChange(collectionId: string): void {
    this.settingsService.updateSettings({ activeCollectionId: collectionId });
  }

  closeDrawer(): void {
    this.chatService.closeCitationDrawer();
  }
}
