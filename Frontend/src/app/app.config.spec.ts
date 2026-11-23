import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { appConfig } from './app.config';
import { SessionService } from './core/services/session.service';

describe('Application session initializer', () => {
  it('calls the session endpoint during application initialization', async () => {
    TestBed.configureTestingModule({
      providers: [...appConfig.providers, provideHttpClientTesting()],
    });
    const initializer = TestBed.inject(ApplicationInitStatus);
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/sessions');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ sessionId: 'startup-session', expiresAt: '2026-11-30T00:00:00Z' });
    await initializer.donePromise;
    expect(TestBed.inject(SessionService).session()?.sessionId).toBe('startup-session');
    http.verify();
  });
});
