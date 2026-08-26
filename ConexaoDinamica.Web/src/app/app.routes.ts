import { Routes } from '@angular/router';

import { adminGuard, autenticadoGuard } from './core/guards/autenticado.guard';

/**
 * Três áreas com propósitos distintos:
 *
 *   /login       aplicação, para o usuário comum
 *   /admin/...   AdminCenter, sob layout próprio e role Administrador
 *   /setup       assistente de configuração, sem layout
 *
 * O login administrativo fica FORA do layout do AdminCenter: ele existe
 * justamente para quem ainda não tem sessão, e envolvê-lo na moldura que exibe
 * usuário e menu não faria sentido.
 *
 * Todas as telas usam loadComponent. Cada uma vira um bundle próprio, o que
 * importa aqui porque o assistente precisa carregar rápido numa instalação
 * nova, sem arrastar o código das demais.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
    title: 'Entrar — ConexaoDinamica',
  },
  {
    path: 'admin/login',
    loadComponent: () =>
      import('./features/auth/login-admin/login-admin').then((m) => m.LoginAdmin),
    title: 'Acesso administrativo — ConexaoDinamica',
  },
  {
    // Sem guard de propósito: é para cá que o setupInterceptor traz o usuário
    // quando não há banco configurado, e nesse instante ainda não existe sessão.
    // O próprio assistente cuida do login no primeiro passo, e os endpoints que
    // ele chama continuam exigindo role Administrador no backend.
    path: 'setup',
    loadComponent: () => import('./features/setup/setup').then((m) => m.Setup),
    title: 'Configuração inicial — ConexaoDinamica',
  },
  {
    // Área da aplicação. Exige apenas sessão — qualquer perfil entra, incluindo
    // o administrador, que ganha um atalho para o AdminCenter na barra.
    path: 'app',
    canActivate: [autenticadoGuard],
    loadComponent: () =>
      import('./features/app/layout/layout-app').then((m) => m.LayoutApp),
    children: [
      {
        path: 'clientes',
        loadComponent: () =>
          import('./features/app/clientes/clientes').then((m) => m.Clientes),
        title: 'Clientes — ConexaoDinamica',
      },
      { path: '', pathMatch: 'full', redirectTo: 'clientes' },
    ],
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/layout/layout-admin').then((m) => m.LayoutAdmin),
    children: [
      {
        path: 'conexoes',
        loadComponent: () =>
          import('./features/admin/painel/painel').then((m) => m.Painel),
        title: 'Conexões — AdminCenter',
      },
      {
        path: 'auditoria',
        loadComponent: () =>
          import('./features/admin/auditoria/auditoria').then((m) => m.Auditoria),
        title: 'Auditoria — AdminCenter',
      },
      { path: '', pathMatch: 'full', redirectTo: 'conexoes' },
    ],
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },

  // Qualquer rota desconhecida volta ao login. O servidor entrega o index.html
  // para qualquer caminho (MapFallbackToFile), então quem decide o que é rota
  // válida é o Angular.
  { path: '**', redirectTo: 'login' },
];
