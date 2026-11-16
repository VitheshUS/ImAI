import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RagSettingsService } from '../../core/services/rag-settings.service';
import { DocumentService } from '../../core/services/document.service';
import { RetrievedChunkResult } from '../../core/models/rag.model';

@Component({
  selector: 'app-retrieval-explorer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './retrieval-explorer.component.html',
  styleUrl: './retrieval-explorer.component.scss',
})
export class RetrievalExplorerComponent {
  readonly settingsService = inject(RagSettingsService);
  readonly documentService = inject(DocumentService);

  searchQuery = 'Explain data encryption and security audit standards';
  topK = 5;
  threshold = 0.7;
  selectedCollection = 'all';
  isSearching = false;
  searchLatencyMs = 124;

  searchResults: RetrievedChunkResult[] = [
    {
      chunkId: 'chunk-sec-082',
      documentTitle: 'Enterprise Security & SOC2 Compliance Whitepaper',
      documentId: 'doc-002',
      pageNumber: 8,
      score: 0.942,
      collection: 'compliance-legal',
      content:
        'All vector embeddings, database records, and customer raw payloads are encrypted at rest using AES-256-GCM. Encryption keys are rotated automatically every 90 days via AWS KMS with HSM backing.',
    },
    {
      chunkId: 'chunk-sec-083',
      documentTitle: 'Enterprise Security & SOC2 Compliance Whitepaper',
      documentId: 'doc-002',
      pageNumber: 9,
      score: 0.887,
      collection: 'compliance-legal',
      content:
        'Audit logs for all retrieval transactions, vector similarity requests, and model invocations are preserved in immutable append-only storage for 365 days to meet SOC2 Type II requirements.',
    },
    {
      chunkId: 'chunk-arch-034',
      documentTitle: 'ImAI Architecture & Retrieval Pipeline Spec',
      documentId: 'doc-001',
      pageNumber: 11,
      score: 0.812,
      collection: 'engineering-specs',
      content:
        'Vector DB isolation: Tenant vector namespaces ensure strictly partitioned HNSW graphs. Metadata filtering enforces multi-tenant boundary checks prior to vector index traversal.',
    },
    {
      chunkId: 'chunk-gdpr-019',
      documentTitle: 'Global Privacy & GDPR Data Retention Policy',
      documentId: 'doc-004',
      pageNumber: 3,
      score: 0.768,
      collection: 'compliance-legal',
      content:
        'Data minimization principle: Unnecessary personally identifiable information (PII) is automatically redacted using named-entity recognition (NER) prior to embedding generation.',
    },
  ];

  async executeSearch(): Promise<void> {
    if (!this.searchQuery.trim() || this.isSearching) return;

    this.isSearching = true;
    const start = performance.now();

    await new Promise((res) => setTimeout(res, 350));
    this.searchLatencyMs = Math.round(performance.now() - start);

    // Mock search results generator based on query
    this.searchResults = [
      {
        chunkId: 'chunk-dyn-' + Math.floor(Math.random() * 899 + 100),
        documentTitle: 'ImAI Architecture & Retrieval Pipeline Spec',
        documentId: 'doc-001',
        pageNumber: 2,
        score: +(0.88 + Math.random() * 0.1).toFixed(3),
        collection: 'engineering-specs',
        content: `Semantic match for "${this.searchQuery}": Dense vector projection identified maximum cosine affinity in the top embedding cluster. Cross-encoder scores validated top rank.`,
      },
      {
        chunkId: 'chunk-dyn-' + Math.floor(Math.random() * 899 + 100),
        documentTitle: 'REST & gRPC RAG Ingestion API Reference',
        documentId: 'doc-003',
        pageNumber: 5,
        score: +(0.81 + Math.random() * 0.08).toFixed(3),
        collection: 'engineering-specs',
        content: `API Query parameters: "hybrid_alpha: 0.7, top_k: ${this.topK}, score_threshold: ${this.threshold}". Resulting chunk context formatted for direct LLM injection.`,
      },
      {
        chunkId: 'chunk-dyn-' + Math.floor(Math.random() * 899 + 100),
        documentTitle: 'Enterprise Security & SOC2 Compliance Whitepaper',
        documentId: 'doc-002',
        pageNumber: 14,
        score: +(0.72 + Math.random() * 0.08).toFixed(3),
        collection: 'compliance-legal',
        content: `Security controls governing vector index access: RBAC permissions verified before chunk tokens are returned to consumer gateway.`,
      },
    ];

    this.isSearching = false;
  }
}
