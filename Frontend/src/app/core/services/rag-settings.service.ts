import { Injectable, signal } from '@angular/core';
import { KnowledgeCollection, RagSettings } from '../models/rag.model';

@Injectable({
  providedIn: 'root',
})
export class RagSettingsService {
  readonly availableModels = [
    { id: 'gemini-2.5-flash', name: 'Gemini 2.5 Flash', provider: 'Google', contextWindow: '1M tokens' },
    { id: 'gemini-2.5-pro', name: 'Gemini 2.5 Pro', provider: 'Google', contextWindow: '2M tokens' },
    { id: 'claude-3-7-sonnet', name: 'Claude 3.7 Sonnet', provider: 'Anthropic', contextWindow: '200k tokens' },
    { id: 'gpt-4o', name: 'GPT-4o', provider: 'OpenAI', contextWindow: '128k tokens' },
    { id: 'deepseek-r1', name: 'DeepSeek R1 Distill', provider: 'Local / Ollama', contextWindow: '64k tokens' },
  ];

  readonly defaultCollections: KnowledgeCollection[] = [
    {
      id: 'all',
      name: 'All Knowledge Bases',
      description: 'Search across all indexed vectors',
      documentCount: 6,
      totalChunks: 840,
    },
    {
      id: 'engineering-specs',
      name: 'Engineering Specs',
      description: 'System architecture, API schemas, and deployment guides',
      documentCount: 3,
      totalChunks: 480,
    },
    {
      id: 'compliance-legal',
      name: 'Compliance & Legal',
      description: 'SOC2, GDPR, Privacy Policies, and Vendor Agreements',
      documentCount: 2,
      totalChunks: 260,
    },
    {
      id: 'product-kb',
      name: 'Product Knowledge Base',
      description: 'Product manuals, user workflows, and FAQ documents',
      documentCount: 1,
      totalChunks: 100,
    },
  ];

  readonly settings = signal<RagSettings>({
    model: 'gemini-2.5-pro',
    temperature: 0.2,
    retrievalTopK: 4,
    similarityCutoff: 0.72,
    searchType: 'hybrid',
    rerankerEnabled: true,
    systemPrompt:
      'You are ImAI, an enterprise RAG assistant. Ground all statements strictly in the retrieved context. Always include inline citation brackets [1], [2] when referencing source facts.',
    activeCollectionId: 'all',
  });

  updateSettings(partial: Partial<RagSettings>): void {
    this.settings.update((current) => ({ ...current, ...partial }));
  }

  resetToDefaults(): void {
    this.settings.set({
      model: 'gemini-2.5-pro',
      temperature: 0.2,
      retrievalTopK: 4,
      similarityCutoff: 0.72,
      searchType: 'hybrid',
      rerankerEnabled: true,
      systemPrompt:
        'You are ImAI, an enterprise RAG assistant. Ground all statements strictly in the retrieved context. Always include inline citation brackets [1], [2] when referencing source facts.',
      activeCollectionId: 'all',
    });
  }
}
