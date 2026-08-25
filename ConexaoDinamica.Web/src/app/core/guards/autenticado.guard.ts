import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';

import { AuthService } from '../services/auth.service';
import { PERFIL_ADMINISTRADOR } from '../models/auth.model';

/** Exige sessao ativa. */
export const autenticadoGuard: CanActivateFn = (_rota, estado) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.autenticado()) {
    return true;
  }

  // Guarda o destino para retomar apos o login, em vez de sempre cair na home.
  return router.createUrlTree(['/login'], {
    queryParams: { retorno: estado.url },
  });
};

/**
 * Exige role Administrador.
 *
 * E conveniencia de interface, nao seguranca: o backend valida a role em toda
 * requisicao. Este guard so evita mostrar uma tela que carregaria vazia e
 * terminaria em 403.
 */
export const adminGuard: CanActivateFn = (rota, estado) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const autenticacao = autenticadoGuard(rota, estado);

  if (autenticacao !== true) {
    return autenticacao;
  }

  return auth.sessao()?.perfil === PERFIL_ADMINISTRADOR
    ? true
    : router.createUrlTree(['/login'], { queryParams: { semPermissao: true } });
};
