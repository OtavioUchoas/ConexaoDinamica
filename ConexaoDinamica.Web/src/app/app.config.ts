import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { setupInterceptor } from './core/interceptors/setup.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),

    // A ordem importa: authInterceptor age na REQUISIÇÃO (anexa o token) e
    // setupInterceptor na RESPOSTA (trata 503 e 401). Declarando auth primeiro,
    // o token é adicionado antes de a chamada sair, e a resposta volta pelo
    // setup na ordem inversa — que é exatamente o desejado.
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor, setupInterceptor]),
    ),

    // Sem provideAnimations: a partir do Angular Material 21 as animações são
    // feitas em CSS nativo, e o pacote @angular/animations deixou de ser
    // necessário — por isso "ng add @angular/material" não o instala.
  ],
};
