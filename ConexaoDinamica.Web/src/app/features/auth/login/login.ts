import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { AuthService } from '../../../core/services/auth.service';

/**
 * Tela de login.
 *
 * Atende os dois fluxos do backend com um formulário só, alternados por um
 * toggle. Eles não são redundantes:
 *
 *   usuário  ->  /api/v1/login        exige banco configurado
 *   admin    ->  /api/v1/admin/login  funciona SEM banco
 *
 * O modo admin é o único caminho possível numa instalação nova, e também a
 * saída quando o Postgres fica indisponível.
 */
@Component({
  selector: 'app-login',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressBarModule,
    MatSlideToggleModule,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected identificador = '';
  protected senha = '';
  protected modoAdmin = signal(true);

  protected readonly carregando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected entrar(): void {
    if (this.carregando()) {
      return;
    }

    this.carregando.set(true);
    this.erro.set(null);

    // Tipado como Observable<unknown> porque os dois endpoints devolvem
    // formatos diferentes e aqui so interessa sucesso ou falha — o AuthService
    // ja normalizou a sessao internamente.
    const requisicao: Observable<unknown> = this.modoAdmin()
      ? this.auth.entrarComoAdmin({ login: this.identificador, senha: this.senha })
      : this.auth.entrar({ email: this.identificador, senha: this.senha });

    requisicao.subscribe({
      next: () => {
        this.carregando.set(false);
        this.router.navigate(['/admin']);
      },
      error: (falha: HttpErrorResponse) => {
        this.carregando.set(false);
        this.erro.set(this.descreverFalha(falha));
      },
    });
  }

  /**
   * Traduz a falha para algo acionável.
   *
   * O 400 do FluentValidation chega com os erros por campo em errors — repassar
   * "Requisição inválida" esconderia justamente o que o usuário precisa
   * corrigir.
   */
  private descreverFalha(falha: HttpErrorResponse): string {
    if (falha.status === 401) {
      return 'Credenciais inválidas.';
    }

    if (falha.status === 400 && falha.error?.errors) {
      const mensagens = Object.values(
        falha.error.errors as Record<string, string[]>,
      ).flat();

      return mensagens.join(' ');
    }

    if (falha.status === 0) {
      return 'Não foi possível falar com o servidor. Ele está em execução?';
    }

    return falha.error?.message ?? 'Não foi possível entrar. Tente novamente.';
  }
}
