import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { AuthService } from '../services/auth.service';

/**
 * Anexa o token JWT nas chamadas para a propria API.
 *
 * A verificacao de prefixo evita vazar a credencial: sem ela, qualquer
 * requisicao para um dominio externo levaria junto o Authorization.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).obterToken();

  if (!token || !req.url.startsWith('/api')) {
    return next(req);
  }

  return next(
    req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }),
  );
};
