import { CanActivateFn } from '@angular/router';

/**
 * Route guard placeholder.
 * Open access enabled: anyone can access all routes without authentication.
 * Can be configured with AuthService when authentication is introduced later.
 */
export const authGuard: CanActivateFn = (_route, _state) => {
  return true;
};
