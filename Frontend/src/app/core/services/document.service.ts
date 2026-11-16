import { Injectable, signal, computed } from '@angular/core';
import { KnowledgeDocument, DocumentStatus } from '../models/rag.model';

@Injectable({
  providedIn: 'root',
})
export class DocumentService {
  private readonly _documents = signal<KnowledgeDocument[]>([
    {
      id: 'doc-001',
      title: 'ImAI Architecture & Retrieval Pipeline Spec',
      filename: 'imai-architecture-pipeline.pdf',
      type: 'pdf',
      sizeBytes: 2450000,
      uploadedAt: new Date(Date.now() - 3600000 * 24 * 3),
      chunksCount: 184,
      status: 'ready',
      collection: 'engineering-specs',
      description: 'Covers HNSW vector index layout, cross-encoder reranking, and semantic chunking strategy.',
    },
    {
      id: 'doc-002',
      title: 'Enterprise Security & SOC2 Compliance Whitepaper',
      filename: 'security-soc2-compliance-2026.docx',
      type: 'docx',
      sizeBytes: 1350000,
      uploadedAt: new Date(Date.now() - 3600000 * 24 * 5),
      chunksCount: 142,
      status: 'ready',
      collection: 'compliance-legal',
      description: 'Audit guidelines, AES-256 data at rest encryption, and PII masking protocols.',
    },
    {
      id: 'doc-003',
      title: 'REST & gRPC RAG Ingestion API Reference',
      filename: 'rag-api-reference.markdown',
      type: 'markdown',
      sizeBytes: 420000,
      uploadedAt: new Date(Date.now() - 3600000 * 12),
      chunksCount: 96,
      status: 'ready',
      collection: 'engineering-specs',
      description: 'Endpoints for batch document ingestion, vector similarity search, and hybrid query filters.',
    },
    {
      id: 'doc-004',
      title: 'Global Privacy & GDPR Data Retention Policy',
      filename: 'gdpr-data-retention.pdf',
      type: 'pdf',
      sizeBytes: 1120000,
      uploadedAt: new Date(Date.now() - 3600000 * 24 * 8),
      chunksCount: 118,
      status: 'ready',
      collection: 'compliance-legal',
      description: 'Data subject rights, 30-day retention policies, and cross-border vector transmission rules.',
    },
    {
      id: 'doc-005',
      title: 'ImAI Operator User Manual & Query Guide',
      filename: 'imai-user-manual-v2.pdf',
      type: 'pdf',
      sizeBytes: 850000,
      uploadedAt: new Date(Date.now() - 3600000 * 2),
      chunksCount: 100,
      status: 'ready',
      collection: 'product-kb',
      description: 'Prompt formulation tips, citation verification steps, and collection management.',
    },
  ]);

  readonly documents = this._documents.asReadonly();

  readonly totalVectors = computed(() =>
    this._documents().reduce((acc, doc) => acc + doc.chunksCount, 0)
  );

  readonly readyDocumentsCount = computed(
    () => this._documents().filter((d) => d.status === 'ready').length
  );

  addDocument(doc: Omit<KnowledgeDocument, 'id' | 'uploadedAt' | 'status' | 'chunksCount'>): void {
    const newDoc: KnowledgeDocument = {
      ...doc,
      id: 'doc-' + Math.random().toString(36).substring(2, 8),
      uploadedAt: new Date(),
      status: 'chunking',
      chunksCount: 0,
    };

    this._documents.update((docs) => [newDoc, ...docs]);

    // Simulate async embedding and vector indexing pipeline
    setTimeout(() => {
      this.updateDocumentStatus(newDoc.id, 'embedding', 24);
    }, 1500);

    setTimeout(() => {
      this.updateDocumentStatus(newDoc.id, 'ready', Math.floor(newDoc.sizeBytes / 25000) + 12);
    }, 3500);
  }

  updateDocumentStatus(id: string, status: DocumentStatus, chunksCount?: number): void {
    this._documents.update((docs) =>
      docs.map((doc) => {
        if (doc.id === id) {
          return {
            ...doc,
            status,
            ...(chunksCount !== undefined ? { chunksCount } : {}),
          };
        }
        return doc;
      })
    );
  }

  deleteDocument(id: string): void {
    this._documents.update((docs) => docs.filter((d) => d.id !== id));
  }
}
