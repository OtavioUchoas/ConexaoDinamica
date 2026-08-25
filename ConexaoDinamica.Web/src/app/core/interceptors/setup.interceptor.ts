import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { SetupPendente } from '../models/conexao.model';

/**
 * Leva ao assistente de configuracao quando o backend responde que ainda nao
 * ha bancos configurados.
 *
 * O ModoSetupMiddleware devolve 503 com { setupRequired: true, conexoes }. O
 * campo setupRequired e o que distingue esse caso de um 503 comum — sem ele,
 * qualquer indisponibilidade jogaria o usuario no assistente.
 *
 * O 401 tambem e tratado aqui: sessao expirada volta para o login em vez de
 * deixar a tela quebrada com dados que nunca chegam.
 */
export const setupInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((erro: HttpErrorResponse) => {
      if (erro.status === 503 && (erro.error as SetupPendente)?.setupRequired) {
        const conexoes = (erro.error as SetupPendente).conexoes;

        // O backend informa QUAL conexao falta, entao o assistente ja abre no
        // passo certo em vez de comecar do zero.
        router.navigate(['/setup'], {
          queryParams: {
            postgres: conexoes.postgres,
            mongo: conexoes.mongo,
          },
        });
      }

      if (erro.status === 401) {
        localStorage.removeItem('conexaodinamica.sessao');
        router.navigate(['/login']);
      }

      return throwError(() => erro);
    }),
  );
};
