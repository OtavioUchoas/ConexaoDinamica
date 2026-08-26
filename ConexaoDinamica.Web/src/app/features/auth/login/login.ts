import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { AuthService } from '../../../core/services/auth.service';

/**
 * Login do usuário da aplicação.
 *
 * O acesso administrativo mora em /admin/login, em tela separada — ver a
 * documentação de LoginAdmin para o motivo.
 */
@Component({
  selector: 'app-login',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected email = '';
  protected senha = '';

  protected readonly carregando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected entrar(): void {
    if (this.carregando()) {
      return;
    }

    this.carregando.set(true);
    this.erro.set(null);

    this.auth.entrar({ email: this.email, senha: this.senha }).subscribe({
      next: () => {
        this.carregando.set(false);
        this.router.navigate(['/app']);
      },
      error: (falha: HttpErrorResponse) => {
        this.carregando.set(false);
        this.erro.set(this.descreverFalha(falha));
      },
    });
  }

  /**
   * O 400 do FluentValidation traz os erros por campo em "errors" — repassar
   * "Requisição inválida" esconderia justamente o que precisa ser corrigido.
   */
  private descreverFalha(falha: HttpErrorResponse): string {
    if (falha.status === 401) {
      return 'E-mail ou senha inválidos.';
    }

    if (falha.status === 400 && falha.error?.errors) {
      return Object.values(falha.error.errors as Record<string, string[]>)
        .flat()
        .join(' ');
    }

    if (falha.status === 0) {
      return 'Não foi possível falar com o servidor.';
    }

    return falha.error?.message ?? 'Não foi possível entrar. Tente novamente.';
  }
}
