import { Component, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentService } from '../../core/services/document.service';
import { RagSettingsService } from '../../core/services/rag-settings.service';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { KnowledgeDocument } from '../../core/models/rag.model';

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss',
})
export class DocumentsComponent {
  readonly documentService = inject(DocumentService);
  readonly settingsService = inject(RagSettingsService);

  readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  selectedFile: File | null = null;
  uploadError = '';
  dragActive = false;
  pendingDeleteId: string | null = null;
  readonly collections = this.settingsService.defaultCollections.filter(
    (collection) => collection.id !== 'all',
  );
  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeUploadModal();
    this.pendingDeleteId = null;
  }
  requestAccess(): void {
    this.router.navigate(['/login'], { queryParams: { returnUrl: '/documents' } });
  }
  selectFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) this.acceptFile(file);
  }
  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragActive = false;
    const file = event.dataTransfer?.files[0];
    if (file) this.acceptFile(file);
  }
  acceptFile(file: File): void {
    this.uploadError = '';
    const ext = file.name.split('.').pop()?.toLowerCase();
    const types: Record<string, KnowledgeDocument['type']> = {
      pdf: 'pdf',
      docx: 'docx',
      md: 'markdown',
      markdown: 'markdown',
      txt: 'txt',
      csv: 'csv',
      json: 'json',
    };
    if (!ext || !types[ext]) {
      this.uploadError = 'Choose a PDF, DOCX, Markdown, TXT, CSV, or JSON file.';
      this.selectedFile = null;
      return;
    }
    if (!file.size || file.size > 50 * 1024 * 1024) {
      this.uploadError = 'Choose a non-empty file smaller than 50 MB.';
      this.selectedFile = null;
      return;
    }
    this.selectedFile = file;
    this.newDocFilename = file.name;
    this.newDocType = types[ext];
    this.newDocTitle = file.name.replace(/\.[^.]+$/, '').replace(/[-_]/g, ' ');
  }
  confirmDelete(): void {
    if (this.authService.canManageDocuments() && this.pendingDeleteId)
      this.documentService.deleteDocument(this.pendingDeleteId);
    this.pendingDeleteId = null;
  }
  searchQuery = '';
  selectedFilterCollection = 'all';

  // Upload modal state
  isUploadModalOpen = false;
  newDocTitle = '';
  newDocFilename = '';
  newDocCollection = 'engineering-specs';
  newDocType: KnowledgeDocument['type'] = 'pdf';
  newDocDescription = '';

  get filteredDocuments(): KnowledgeDocument[] {
    return this.documentService.documents().filter((doc) => {
      const matchesSearch =
        doc.title.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
        doc.filename.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
        doc.description.toLowerCase().includes(this.searchQuery.toLowerCase());

      const matchesCollection =
        this.selectedFilterCollection === 'all' || doc.collection === this.selectedFilterCollection;

      return matchesSearch && matchesCollection;
    });
  }

  openUploadModal(): void {
    if (!this.authService.canManageDocuments()) {
      this.requestAccess();
      return;
    }
    this.isUploadModalOpen = true;
  }

  closeUploadModal(): void {
    this.isUploadModalOpen = false;
    this.resetForm();
  }

  submitNewDocument(): void {
    if (!this.authService.canManageDocuments() || !this.selectedFile || !this.newDocTitle.trim())
      return;

    this.documentService.addDocument({
      title: this.newDocTitle.trim(),
      filename:
        this.newDocFilename.trim() ||
        `${this.newDocTitle.toLowerCase().replace(/\s+/g, '-')}.${this.newDocType}`,
      type: this.newDocType,
      sizeBytes: this.selectedFile.size,
      collection: this.newDocCollection,
      description:
        this.newDocDescription.trim() ||
        'Uploaded enterprise document ready for semantic indexing.',
    });

    this.closeUploadModal();
  }

  deleteDocument(id: string): void {
    if (!this.authService.canManageDocuments()) {
      this.requestAccess();
      return;
    }
    this.pendingDeleteId = id;
  }

  reindex(id: string): void {
    if (!this.authService.canManageDocuments()) {
      this.requestAccess();
      return;
    }
    this.documentService.updateDocumentStatus(id, 'embedding');
    setTimeout(() => {
      this.documentService.updateDocumentStatus(id, 'ready');
    }, 2000);
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / 1048576).toFixed(1) + ' MB';
  }

  private resetForm(): void {
    this.selectedFile = null;
    this.uploadError = '';
    this.newDocTitle = '';
    this.newDocFilename = '';
    this.newDocCollection = 'engineering-specs';
    this.newDocType = 'pdf';
    this.newDocDescription = '';
  }
}
