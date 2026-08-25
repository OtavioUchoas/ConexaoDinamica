import { Routes } from '@angular/router';

import { adminGuard } from './core/guards/autenticado.guard';

/**
 * Rotas com carregamento tardio (loadComponent).
 *
 * Cada tela vira um bundle proprio, baixado so quando acessada. Importa aqui
 * porque o assistente de configuracao precisa carregar rapido em uma instalacao
 * nova, sem arrastar junto o codigo das telas de negocio.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
    title: 'Entrar — ConexaoDinamica',
  },
  {
    // Sem guard de propósito: e para ca que o setupInterceptor traz o usuario
    // quando nao ha banco configurado, e nesse momento ainda nao existe sessao.
    // O proprio assistente cuida do login no primeiro passo, e os endpoints que
    // ele chama continuam exigindo role Administrador no backend.
    path: 'setup',
    loadComponent: () => import('./features/setup/setup').then((m) => m.Setup),
    title: 'Configuração inicial — ConexaoDinamica',
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/painel/painel').then((m) => m.Painel),
    title: 'AdminCenter — ConexaoDinamica',
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },

  // Qualquer rota desconhecida volta ao login. O servidor entrega o index.html
  // para qualquer caminho (MapFallbackToFile), entao quem decide o que e rota
  // valida e o Angular.
  { path: '**', redirectTo: 'login' },
];
