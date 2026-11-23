import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../config/api.config';

// Only API calls receive credentials. The browser owns the HttpOnly token.
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const apiUrl = inject(API_BASE_URL).replace(/\/+$/, '');
  const isApiRequest = request.url === apiUrl || request.url.startsWith(apiUrl + '/');
  return next(isApiRequest ? request.clone({ withCredentials: true }) : request);
};
