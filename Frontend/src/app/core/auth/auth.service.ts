import { Injectable, signal, computed } from '@angular/core';
import { UserProfile } from './user.model';
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly isAuthRequired = signal(false);
  private readonly sessionKey = 'imai.demo-session';
  private readonly _currentUser = signal<UserProfile | null>(this.restoreSession());
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);
  readonly canManageDocuments = computed(
    () => this.isAuthenticated() && this._currentUser()?.role !== 'viewer',
  );
  private restoreSession(): UserProfile | null {
    try {
      const stored = JSON.parse(sessionStorage.getItem(this.sessionKey) || 'null');
      if (
        stored &&
        stored.expiresAt > Date.now() &&
        typeof stored.email === 'string' &&
        /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(stored.email)
      ) {
        return {
          id: 'demo-member',
          name: stored.email.split('@')[0],
          email: stored.email,
          role: 'researcher',
          organization: 'Demo workspace',
        };
      }
    } catch {
      /* An unavailable or expired local session falls back to guest. */
    }
    return null;
  }
  async login(email: string, _password: string): Promise<boolean> {
    email = email.trim();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return false;
    await new Promise((resolve) => setTimeout(resolve, 350));
    this._currentUser.set({
      id: 'demo-member',
      name: email.split('@')[0],
      email,
      role: 'researcher',
      organization: 'Demo workspace',
    });
    try {
      sessionStorage.setItem(
        this.sessionKey,
        JSON.stringify({ email, expiresAt: Date.now() + 8 * 60 * 60 * 1000 }),
      );
    } catch {
      /* Keep in-memory access when storage is unavailable. */
    }
    return true;
  }
  logout(): void {
    this._currentUser.set(null);
    try {
      sessionStorage.removeItem(this.sessionKey);
    } catch {
      /* Storage may be unavailable. */
    }
  }
  getAuthToken(): string | null {
    return null;
  }
}
