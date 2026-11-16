import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DocumentsComponent } from './documents.component';
import { AuthService } from '../../core/auth/auth.service';
import { DocumentService } from '../../core/services/document.service';

describe('Document permissions and file validation', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });
  afterEach(() => sessionStorage.clear());
  it('blocks guest submission and deletion', () => {
    const component = TestBed.createComponent(DocumentsComponent).componentInstance;
    const documents = TestBed.inject(DocumentService);
    const count = documents.documents().length;
    component.acceptFile(new File(['content'], 'notes.txt'));
    component.submitNewDocument();
    component.pendingDeleteId = documents.documents()[0].id;
    component.confirmDelete();
    expect(documents.documents().length).toBe(count);
  });
  it('uses the selected file metadata and rejects invalid files', () => {
    const component = TestBed.createComponent(DocumentsComponent).componentInstance;
    component.acceptFile(new File(['content'], 'research-notes.md'));
    expect(component.newDocType).toBe('markdown');
    expect(component.newDocTitle).toBe('research notes');
    expect(component.selectedFile?.size).toBe(7);
    component.acceptFile(new File(['content'], 'program.exe'));
    expect(component.selectedFile).toBeNull();
    expect(component.uploadError).toBeTruthy();
    component.acceptFile(new File([], 'empty.pdf'));
    expect(component.selectedFile).toBeNull();
  });
  it('allows a restored member to open upload and delete', () => {
    sessionStorage.setItem(
      'imai.demo-session',
      JSON.stringify({ email: 'member@example.com', expiresAt: Date.now() + 60000 }),
    );
    expect(TestBed.inject(AuthService).canManageDocuments()).toBe(true);
    const component = TestBed.createComponent(DocumentsComponent).componentInstance;
    component.openUploadModal();
    expect(component.isUploadModalOpen).toBe(true);
    const documents = TestBed.inject(DocumentService);
    const id = documents.documents()[0].id;
    component.deleteDocument(id);
    component.confirmDelete();
    expect(documents.documents().some((d) => d.id === id)).toBe(false);
  });
});
