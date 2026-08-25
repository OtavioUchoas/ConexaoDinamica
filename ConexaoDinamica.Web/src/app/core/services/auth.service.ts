import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import {
  AdminLoginRequest,
  AdminLoginResponse,
  LoginRequest,
  LoginResponse,
  PERFIL_ADMINISTRADOR,
  Sessao,
} from '../models/auth.model';

/**
 * Autenticação da aplicação.
 *
 * Concentra os DOIS fluxos de login que o backend expõe:
 *
 *   /api/v1/login        usuário do banco
 *   /api/v1/admin/login  admin de bootstrap, que funciona SEM banco
 *
 * O segundo é o que destrava a configuração inicial e serve de recuperação se
 * o Postgres ficar indisponível. Ambos devolvem um JWT do mesmo emissor, com a
 * role dentro — então daqui para frente o resto da aplicação não precisa saber
 * por qual porta a pessoa entrou.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /**
   * localStorage e não sessionStorage: recarregar a página ou reabrir a aba
   * mantém a sessão, que é o comportamento esperado de um painel de trabalho.
   * A contrapartida é o token sobreviver ao fechamento do navegador — aceitável
   * aqui, onde o token expira em 2 horas.
   */
  private static readonly CHAVE = 'conexaodinamica.sessao';

  private readonly _sessao = signal<Sessao | null>(this.recuperar());

  /** Sessão atual, ou null. Somente leitura para quem consome. */
  readonly sessao = this._sessao.asReadonly();

  readonly autenticado = computed(() => this._sessao() !== null);

  readonly ehAdministrador = computed(
    () => this._sessao()?.perfil === PERFIL_ADMINISTRADOR,
  );

  /** Login de usuário comum. */
  entrar(credenciais: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/v1/login', credenciais)
      .pipe(
        tap((resposta) =>
          this.guardar({
            token: resposta.token,
            nome: resposta.nome,
            email: resposta.email,
            usuarioId: resposta.usuarioId,
            // O endpoint de usuário não devolve o perfil no corpo, mas ele está
            // no token — é de lá que vem, para não assumir "Comum" por engano.
            perfil: this.lerPerfilDoToken(resposta.token),
          }),
        ),
      );
  }

  /** Login do admin de bootstrap. Não depende do banco. */
  entrarComoAdmin(credenciais: AdminLoginRequest): Observable<AdminLoginResponse> {
    return this.http
      .post<AdminLoginResponse>('/api/v1/admin/login', credenciais)
      .pipe(
        tap((resposta) =>
          this.guardar({
            token: resposta.token,
            nome: resposta.nome,
            email: resposta.email,
            perfil: resposta.perfil,
          }),
        ),
      );
  }

  sair(): void {
    localStorage.removeItem(AuthService.CHAVE);
    this._sessao.set(null);
    this.router.navigate(['/login']);
  }

  obterToken(): string | null {
    return this._sessao()?.token ?? null;
  }

  private guardar(sessao: Sessao): void {
    localStorage.setItem(AuthService.CHAVE, JSON.stringify(sessao));
    this._sessao.set(sessao);
  }

  /**
   * Restaura a sessão ao carregar a aplicação.
   *
   * Um token expirado é descartado aqui mesmo: sem isso, a interface exibiria
   * o usuário como autenticado e só descobriria o contrário no primeiro 401 —
   * depois de já ter renderizado telas que ele não pode ver.
   */
  private recuperar(): Sessao | null {
    const bruto = localStorage.getItem(AuthService.CHAVE);

    if (!bruto) {
      return null;
    }

    try {
      const sessao = JSON.parse(bruto) as Sessao;

      if (this.tokenExpirado(sessao.token)) {
        localStorage.removeItem(AuthService.CHAVE);
        return null;
      }

      return sessao;
    } catch {
      // Conteúdo corrompido (edição manual, versão antiga do formato).
      localStorage.removeItem(AuthService.CHAVE);
      return null;
    }
  }

  private tokenExpirado(token: string): boolean {
    const payload = this.lerPayload(token);

    if (!payload?.['exp']) {
      return false;
    }

    // exp vem em segundos desde a época; Date.now() em milissegundos.
    return Number(payload['exp']) * 1000 <= Date.now();
  }

  private lerPerfilDoToken(token: string): string {
    const payload = this.lerPayload(token);

    // O .NET serializa a claim de role com a URI longa do WS-Federation. A
    // chave curta é verificada junto por segurança, caso o backend passe a
    // usar RoleClaimType simplificado.
    const chaveLonga =
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

    return (payload?.[chaveLonga] ?? payload?.['role'] ?? '') as string;
  }

  /**
   * Decodifica o payload do JWT sem validar assinatura.
   *
   * Validar é responsabilidade do servidor — no navegador seria teatro, já que
   * o próprio usuário controla o código. Aqui os dados servem só para a
   * interface decidir o que exibir; toda decisão real de acesso acontece no
   * backend.
   */
  private lerPayload(token: string): Record<string, unknown> | null {
    try {
      const partes = token.split('.');

      if (partes.length !== 3) {
        return null;
      }

      // JWT usa base64url: '-' e '_' no lugar de '+' e '/', e sem preenchimento.
      const base64 = partes[1].replace(/-/g, '+').replace(/_/g, '/');
      const preenchido = base64.padEnd(
        base64.length + ((4 - (base64.length % 4)) % 4),
        '=',
      );

      // decodeURIComponent/escape preserva acentos, que atob sozinho corromperia.
      const json = decodeURIComponent(
        Array.from(atob(preenchido))
          .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
          .join(''),
      );

      return JSON.parse(json) as Record<string, unknown>;
    } catch {
      return null;
    }
  }
}
