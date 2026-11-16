import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

describe('Demo session access', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.resetTestingModule();
  });
  afterEach(() => sessionStorage.clear());
  it('starts with guest permissions', () => {
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.canManageDocuments()).toBe(false);
    expect(auth.getAuthToken()).toBeNull();
  });
  it('restores an unexpired demo session silently', () => {
    sessionStorage.setItem(
      'imai.demo-session',
      JSON.stringify({ email: 'member@example.com', expiresAt: Date.now() + 60000 }),
    );
    const auth = TestBed.inject(AuthService);
    expect(auth.canManageDocuments()).toBe(true);
    auth.logout();
    expect(auth.canManageDocuments()).toBe(false);
    expect(sessionStorage.getItem('imai.demo-session')).toBeNull();
  });
  it('rejects expired and malformed sessions', () => {
    sessionStorage.setItem(
      'imai.demo-session',
      JSON.stringify({ email: 'member@example.com', expiresAt: Date.now() - 1000 }),
    );
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
    TestBed.resetTestingModule();
    sessionStorage.setItem('imai.demo-session', '{broken');
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
  });
  it('rejects an invalid email', async () => {
    const auth = TestBed.inject(AuthService);
    expect(await auth.login('invalid', '')).toBe(false);
    expect(auth.canManageDocuments()).toBe(false);
  });
});
