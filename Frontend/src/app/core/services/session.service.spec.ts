import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from '../auth/auth.interceptor';
import { SessionService } from './session.service';

const session = { sessionId: 'test-session-id', expiresAt: '2026-11-30T00:00:00Z' };

describe('Backend session startup', () => {
  let http: HttpTestingController;
  let service: SessionService;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(SessionService);
  });
  afterEach(() => http.verify());

  it('creates or reuses a backend session using browser credentials', async () => {
    const initialized = service.initialize();
    const request = http.expectOne('/api/sessions');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush(session);
    await initialized;
    expect(service.session()).toEqual(session);
    expect(service.error()).toBeNull();
    expect(service.initializing()).toBe(false);
  });

  it('shares an in-flight request and does not create another session after initialization', async () => {
    const first = service.initialize();
    const second = service.initialize();
    expect(first).toBe(second);
    http.expectOne('/api/sessions').flush(session);
    await first;
    await service.initialize();
    http.expectNone('/api/sessions');
  });

  it('allows the app to load after a failure and supports retry', async () => {
    const failed = service.initialize();
    http.expectOne('/api/sessions').flush({}, { status: 503, statusText: 'Unavailable' });
    await failed;
    expect(service.session()).toBeNull();
    expect(service.error()).toBeTruthy();
    expect(service.initializing()).toBe(false);
    const retry = service.initialize();
    http.expectOne('/api/sessions').flush(session);
    await retry;
    expect(service.session()).toEqual(session);
    expect(service.error()).toBeNull();
  });

  it('attaches credentials to later API requests', () => {
    TestBed.inject(HttpClient).post('/api/sessions/validate', null).subscribe();
    const request = http.expectOne('/api/sessions/validate');
    expect(request.request.withCredentials).toBe(true);
    request.flush(session);
  });

  it('does not attach credentials to other services or similarly named paths', () => {
    const client = TestBed.inject(HttpClient);
    for (const url of ['https://other.example/resource', '/api-other/resource']) {
      client.get(url).subscribe();
      const request = http.expectOne(url);
      expect(request.request.withCredentials).toBe(false);
      request.flush({});
    }
  });
});
