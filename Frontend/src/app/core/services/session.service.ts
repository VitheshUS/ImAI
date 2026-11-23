import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';

export interface AnonymousSession {
  sessionId: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL).replace(/\/+$/, '');
  private readonly currentSession = signal<AnonymousSession | null>(null);
  private readonly pending = signal(false);
  private readonly failure = signal<string | null>(null);
  private initialization?: Promise<void>;

  readonly session = this.currentSession.asReadonly();
  readonly initializing = this.pending.asReadonly();
  readonly error = this.failure.asReadonly();

  initialize(): Promise<void> {
    if (this.currentSession()) return Promise.resolve();
    return (this.initialization ??= this.createSession().finally(() => {
      this.initialization = undefined;
    }));
  }

  private async createSession(): Promise<void> {
    this.pending.set(true);
    this.failure.set(null);
    try {
      const session = await firstValueFrom(
        this.http
          .post<AnonymousSession>(this.apiUrl + '/sessions', null, { withCredentials: true })
          .pipe(timeout(8000)),
      );
      this.currentSession.set(session);
    } catch {
      this.failure.set('We couldn’t start your session. Please try again.');
    } finally {
      this.pending.set(false);
    }
  }
}
